using System.Buffers;
using Typographer.Internal;

namespace Typographer;

/// <summary>Типограф для HTML-фрагментов. Иммутабелен и потокобезопасен.</summary>
public sealed class HtmlTypograf
{
    private readonly HtmlOptions _options;

    /// <summary>Создаёт типограф с указанными настройками.</summary>
    /// <param name="options">Настройки; null — настройки по умолчанию.</param>
    public HtmlTypograf(HtmlOptions? options = null) => _options = options ?? HtmlOptions.Default;

    /// <summary>Типограф с настройками по умолчанию.</summary>
    public static HtmlTypograf Default { get; } = new();

    /// <summary>Типографирует HTML-фрагмент.</summary>
    /// <param name="html">Исходный фрагмент.</param>
    /// <returns>Обработанный фрагмент. Если правок нет — тот же экземпляр строки.</returns>
    public string Process(string html)
    {
        Throw.IfNull(html, nameof(html));

        var buffer = new CharBuffer(html.Length + (html.Length >> 2));
        try
        {
            Run(html.AsSpan(), ref buffer);
            ReadOnlySpan<char> result = buffer.AsSpan();
            Throw.IfTooLong(result.Length, _options.MaxOutputLength);
            return result.SequenceEqual(html.AsSpan()) ? html : result.ToString();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Типографирует HTML-фрагмент, записывая результат в приёмник без промежуточной строки.</summary>
    /// <param name="html">Исходный фрагмент.</param>
    /// <param name="destination">Приёмник результата.</param>
    public void Process(ReadOnlySpan<char> html, IBufferWriter<char> destination)
    {
        Throw.IfNull(destination, nameof(destination));

        var buffer = new CharBuffer(html.Length + (html.Length >> 2));
        try
        {
            Run(html, ref buffer);
            ReadOnlySpan<char> result = buffer.AsSpan();

            // Предел проверяется ДО записи в приёмник: приёмник не должен получить
            // половину результата, за которой следует исключение.
            Throw.IfTooLong(result.Length, _options.MaxOutputLength);
            result.CopyTo(destination.GetSpan(result.Length));
            destination.Advance(result.Length);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    private void Run(ReadOnlySpan<char> source, ref CharBuffer buffer)
    {
        // Prepare идёт по ДОКУМЕНТУ отдельным проходом: тогда декодирование сущностей
        // не зависит от номера текстового узла, а метка порядка байт снимается ровно
        // один раз — в начале входа, а не в начале каждого сегмента.
        var prepared = new CharBuffer(source.Length + (source.Length >> 2));
        try
        {
            Preparer.RunDocument(source, _options.Rules, ref prepared);
            RunPipeline(prepared.AsSpan(), ref buffer);
        }
        finally
        {
            prepared.Dispose();
        }
    }

    // Временная форма: тело прежнего Run до переноса фазы Prepare на документный проход.
    // Следующая задача плана 2a переводит на документные проходы остальные фазы и этот
    // метод исчезает — RunSegments перестанет быть единственным, кто ходит по сегментам.
    private void RunPipeline(ReadOnlySpan<char> source, ref CharBuffer buffer)
    {
        // Переносы строк и абзацы расставляются по ГОТОВОМУ телу документа, а не по каждому
        // текстовому узлу: тег абзаца блочный, и обёртка вокруг узла клала бы его внутрь
        // <b> и делала абзац из пробела между двумя тегами. Ради этого нужен ещё один
        // буфер — но только когда хотя бы одна из двух опций включена.
        if (!_options.UseBr && !_options.UseP)
        {
            RunSegments(source, ref buffer);
            return;
        }

        var body = new CharBuffer(source.Length + (source.Length >> 2));
        try
        {
            bool canWrapParagraphs = RunSegments(source, ref body);

            // Там, где блочная разметка уже есть, абзацы не расставляются: <p> вокруг <ul>
            // — невалидный HTML, и границы абзацев в таком документе задаёт сама разметка,
            // а не пустые строки. Незакрытая разметка тоже запрещает обёртку: её </p>
            // иначе окажется внутри незакрытого атрибута, комментария или script.
            LayoutWriter.WriteBreaks(
                body.AsSpan(), _options.UseBr, _options.UseP && canWrapParagraphs, ref buffer);
        }
        finally
        {
            body.Dispose();
        }
    }

    /// <summary>
    /// Прогоняет текстовые узлы через конвейер, копируя разметку как есть.
    /// </summary>
    /// <returns>Можно ли оборачивать документ в абзацы: нет блочной или незакрытой разметки.</returns>
    private bool RunSegments(ReadOnlySpan<char> source, ref CharBuffer buffer)
    {
        var scanner = new MarkupScanner(source);
        bool hasBlockMarkup = false;

        // Состояние сканера создаётся ОДИН раз на документ и протягивается через все
        // текстовые сегменты: тег внутри предложения не должен обнулять разбор кавычек
        // и не должен выглядеть для правил как начало строки.
        var state = new ScanState();
        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = source.Slice(segment.Start, segment.Length);
            if (segment.Kind != SegmentKind.Text)
            {
                // Блочный тег предложение как раз разрывает. За </p> или <br> начинается
                // новая строка, и правила обязаны видеть её начало, а не последний символ
                // прошлого абзаца: иначе в свёрнутом HTML кавычка в начале абзаца выходит
                // закрывающей, а дефис не становится тире прямой речи.
                if (segment.IsBlock)
                {
                    state.Last = '\n';
                    state.TrailingDigits = 0;
                }
                else if (segment.Kind == SegmentKind.Protected && segment.Length > 0)
                {
                    // Содержимое защищённого элемента не анализируется, поэтому оно не
                    // может прозрачно соединять числовой контекст по обе стороны зоны.
                    state.TrailingDigits = 0;
                }

                hasBlockMarkup |= segment.PreventsParagraphWrapping;

                buffer.Write(slice);
                continue;
            }

            var scanned = new CharBuffer(slice.Length + 8);
            var laidOut = new CharBuffer(slice.Length + 8);
            try
            {
                // Фаза Prepare уже отработала по ДОКУМЕНТУ в Run: этот сегмент вырезан
                // из её результата, а не из исходного текста, и готовить его второй раз
                // незачем — сущности здесь уже раскодированы.
                TextScanner.Run(slice, _options.Rules, ref state, ref scanned);
                WordBinder.Run(ref scanned, _options.Rules);
                LayoutWriter.Run(scanned.AsSpan(), _options, ref laidOut);
                Emitter.Encode(laidOut.AsSpan(), _options.Entities, ref buffer);
            }
            finally
            {
                scanned.Dispose();
                laidOut.Dispose();
            }
        }

        return !hasBlockMarkup && !scanner.HasUnclosedMarkup;
    }
}
