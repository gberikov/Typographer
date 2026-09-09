using System.Buffers;
using Typographer.Internal;
using Typographer.Internal.Scan;

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

    private void Run(ReadOnlySpan<char> source, ref CharBuffer output)
    {
        // Каждая фаза — один проход по ДОКУМЕНТУ: она сама ходит по сегментам и копирует
        // разметку байт в байт. Иначе состояние фазы не переживает тег: для словарных
        // правил «<b>сло</b>во» распадалось на два слова, и «во» получало неразрывный
        // пробел как короткое слово. Ради этого буферов ровно три — на документ, а не на
        // каждый текстовый узел.
        var prepared = new CharBuffer(source.Length + (source.Length >> 2));
        var scanned = new CharBuffer(source.Length + (source.Length >> 2));
        var laidOut = new CharBuffer(source.Length + (source.Length >> 2));
        try
        {
            Preparer.RunDocument(source, _options.Rules, ref prepared);
            bool canWrapParagraphs = TextScanner.RunDocument(prepared.AsSpan(), _options.Rules, ref scanned);
            WordBinder.RunDocument(ref scanned, _options.Rules);
            // Нормализация пробельного письма — отдельный документный проход, и он стоит
            // ещё одного буфера. Поэтому запускается, только если хоть одно её правило
            // включено: все они вне Default, и обычный вызов за них не платит.
            if (DocumentSpaceRules.IsEnabled(_options.Rules))
            {
                var normalized = new CharBuffer(scanned.Length + 8);
                try
                {
                    DocumentSpaceRules.RunDocument(scanned.AsSpan(), _options.Rules, ref normalized);
                    LayoutWriter.Run(normalized.AsSpan(), _options, canWrapParagraphs, ref laidOut);
                }
                finally
                {
                    normalized.Dispose();
                }
            }
            else
            {
                LayoutWriter.Run(scanned.AsSpan(), _options, canWrapParagraphs, ref laidOut);
            }
            Emitter.EncodeDocument(laidOut.AsSpan(), _options.Entities, ref output);
        }
        finally
        {
            laidOut.Dispose();
            scanned.Dispose();
            prepared.Dispose();
        }
    }
}
