using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>
/// Правила нормализации пробельного письма: края документа, отступы строк, табуляция,
/// повторные переводы строки.
/// </summary>
/// <remarks>
/// Эти правила не помещаются в посимвольный сканер: они смотрят на строку и на документ
/// целиком, а не на соседний символ. Поэтому они живут отдельным проходом, как переносы и
/// абзацы, и применяются только к текстовым узлам — перевод строки внутри
/// <c>&lt;pre&gt;</c> или значения атрибута трогать нельзя (гарантия 3).
/// Все они выключены в <see cref="RuleSet.Default"/>: они меняют текст за пределами
/// оформления. Пока ни одно не включено, проход не запускается вовсе и не стоит ни буфера,
/// ни копии документа — см. <see cref="IsEnabled"/>.
/// </remarks>
internal static class DocumentSpaceRules
{
    /// <summary>Ширина, на которую разворачивается табуляция.</summary>
    private const int TabWidth = 4;

    /// <summary>Хоть одно правило нормализации включено, и проход имеет смысл запускать.</summary>
    public static bool IsEnabled(RuleSet rules)
        => rules.Contains(RuleId.Common.Space.TrimLeft)
           || rules.Contains(RuleId.Common.Space.TrimRight)
           || rules.Contains(RuleId.Common.Space.DelLeadingBlanks)
           || rules.Contains(RuleId.Common.Space.DelTrailingBlanks)
           || rules.Contains(RuleId.Common.Space.DelRepeatN)
           || rules.Contains(RuleId.Common.Space.ReplaceTab)
           || rules.Contains(RuleId.Common.Space.InsertFinalNewline);

    /// <summary>Нормализация обычного текста: сегментов нет, весь вход — один узел.</summary>
    /// <param name="source">Исходный текст.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="buffer">Приёмник.</param>
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)
        => WriteNode(source, rules, atDocumentStart: true, atDocumentEnd: true, ref buffer);

    /// <summary>
    /// Нормализация документа: правила применяются к текстовым узлам, разметка и защищённые
    /// зоны копируются байт в байт.
    /// </summary>
    /// <param name="html">Документ после фаз Scan и Bind.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="buffer">Приёмник.</param>
    public static void RunDocument(ReadOnlySpan<char> html, RuleSet rules, ref CharBuffer buffer)
    {
        var scanner = new MarkupScanner(html);
        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = html.Slice(segment.Start, segment.Length);
            if (segment.Kind != SegmentKind.Text)
            {
                buffer.Write(slice);
                continue;
            }

            // Край документа — это край ДОКУМЕНТА, а не узла: обрезать текст после
            // закрывающего тега значило бы съесть пробел, который автор поставил между
            // разметкой и словом.
            WriteNode(
                slice,
                rules,
                atDocumentStart: segment.Start == 0,
                atDocumentEnd: segment.Start + segment.Length == html.Length,
                ref buffer);
        }
    }

    private static void WriteNode(
        ReadOnlySpan<char> source, RuleSet rules, bool atDocumentStart, bool atDocumentEnd,
        ref CharBuffer buffer)
    {
        int start = 0;
        int end = source.Length;

        if (atDocumentStart && rules.Contains(RuleId.Common.Space.TrimLeft))
        {
            while (start < end && char.IsWhiteSpace(source[start]))
            {
                start++;
            }
        }

        if (atDocumentEnd && rules.Contains(RuleId.Common.Space.TrimRight))
        {
            while (end > start && char.IsWhiteSpace(source[end - 1]))
            {
                end--;
            }
        }

        bool delLeading = rules.Contains(RuleId.Common.Space.DelLeadingBlanks);
        bool delTrailing = rules.Contains(RuleId.Common.Space.DelTrailingBlanks);
        bool delRepeatN = rules.Contains(RuleId.Common.Space.DelRepeatN);
        bool replaceTab = rules.Contains(RuleId.Common.Space.ReplaceTab);

        // Число переводов строки подряд, уже записанных этим узлом. Двух хватает на пустую
        // строку между абзацами; третий и дальше — повтор, который схлопывает delRepeatN.
        int newlines = 0;
        bool atLineStart = start == 0 || (start > 0 && source[start - 1] == '\n');

        for (int i = start; i < end; i++)
        {
            char c = source[i];

            if (c == '\n')
            {
                if (delTrailing)
                {
                    TrimWrittenLineEnd(ref buffer);
                }

                newlines++;
                if (delRepeatN && newlines > 2)
                {
                    continue;
                }

                buffer.Write(c);
                atLineStart = true;
                continue;
            }

            if (c != '\r')
            {
                newlines = 0;
            }

            if (atLineStart && delLeading && (c == ' ' || c == '\t'))
            {
                continue;
            }

            if (c == '\t' && replaceTab)
            {
                for (int k = 0; k < TabWidth; k++)
                {
                    buffer.Write(' ');
                }

                atLineStart = false;
                continue;
            }

            if (!char.IsWhiteSpace(c))
            {
                atLineStart = false;
            }

            buffer.Write(c);
        }

        if (atDocumentEnd && delTrailing)
        {
            TrimWrittenLineEnd(ref buffer);
        }

        if (atDocumentEnd && rules.Contains(RuleId.Common.Space.InsertFinalNewline)
            && (buffer.Length == 0 || buffer.CharAt(buffer.Length - 1) != '\n'))
        {
            buffer.Write('\n');
        }
    }

    /// <summary>
    /// Убирает пробелы и табуляцию, уже записанные в конец текущей строки буфера.
    /// Решение принимается задним числом — в момент записи пробела ещё не известно, что за
    /// ним конец строки, а не следующее слово.
    /// </summary>
    private static void TrimWrittenLineEnd(ref CharBuffer buffer)
    {
        int length = buffer.Length;
        while (length > 0 && buffer.CharAt(length - 1) is ' ' or '\t')
        {
            length--;
        }

        buffer.Truncate(length);
    }
}
