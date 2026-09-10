using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>
/// Фаза Prepare: приводит вход к одному представлению до того, как правила начнут читать
/// соседние символы.
/// </summary>
/// <remarks>
/// Правила фазы Scan решают по КЛАССУ соседнего символа: пробел слева, знак препинания
/// справа. Пока один и тот же символ может быть записан двумя способами — символом
/// (<c> </c>) и сущностью (<c>&amp;nbsp;</c>) — каждое правило вынуждено знать оба
/// написания, и любое, которое знает только одно, молча не срабатывает. Хуже того, вывод
/// типографа в режиме сущностей сам записан вторым способом, поэтому повторный прогон видел
/// «амперсанд» там, где стоял неразрывный пробел, и нарушал идемпотентность (гарантия 5).
/// Декодирование здесь оставляет правилам ровно одно представление; обратно в сущности текст
/// переводит фаза Emit.
/// </remarks>
internal static class Preparer
{
    /// <summary>Ширина табулятора в пробелах.</summary>
    private const int TabWidth = 4;

    /// <param name="source">Текстовый узел.</param>
    /// <param name="decodeEntities">
    /// Декодировать типографские сущности. Для HTML — да; в обычном тексте
    /// <c>&amp;nbsp;</c> сущностью не является и остаётся набором символов.
    /// </param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="buffer">Приёмник.</param>
    /// <param name="isDocumentStart">Узел начинается в начале документа, где допустима метка порядка байт.</param>
    public static void Run(
        ReadOnlySpan<char> source, bool decodeEntities, RuleSet rules, ref CharBuffer buffer, bool isDocumentStart)
    {
        // Фаза существует ради правил. Если правил нет, ей некому готовить вход, а типограф
        // обязан вернуть его байт в байт: без этого «&nbsp;» превращался бы в символ даже
        // при пустом наборе правил.
        if (rules.Count == 0)
        {
            buffer.Write(source);
            return;
        }

        // BOM удаляется только в начале документа. Внутренний U+FEFF может разделять
        // символы, которые после удаления станут HTML-тегом или сущностью: <\uFEFFb>, &\uFEFFnbsp;.
        int start = 0;
        if (isDocumentStart && rules.Contains(RuleId.Common.Other.DelBom))
        {
            while (start < source.Length && source[start] == Chars.Bom)
            {
                start++;
            }
        }

        // Снятие неразрывных пробелов — обратная операция к тому, что делает фаза Bind:
        // авторские неразрывные пробелы становятся обычными, чтобы правила расставили свои.
        // Без правил фазы Bind текст останется вовсе без неразрывных пробелов, и это не
        // ошибка, а прямое следствие включения правила в одиночку.
        bool replaceNbsp = rules.Contains(RuleId.Common.Nbsp.ReplaceNbsp);

        // Сущность прямой кавычки декодируется отдельным правилом и только здесь. В таблицу
        // типографских сущностей она не входит намеренно: иначе фаза Emit начала бы кодировать
        // обратно КАЖДУЮ прямую кавычку в тексте, чего не просил никто.
        bool quot = decodeEntities && rules.Contains(RuleId.Common.Html.Quot);

        // Табуляция разворачивается здесь, а не в фазе нормализации пробелов в конце
        // конвейера: получившиеся пробелы обязаны попасть на глаза правилам, которые
        // читают соседний символ. Иначе первый прогон оставляет табуляцию невидимой для
        // фаз Scan и Bind, второй видит на её месте пробел — и результат меняется,
        // нарушая гарантию 5.
        bool replaceTab = rules.Contains(RuleId.Common.Space.ReplaceTab);

        // Фазе интересны три символа из всего текста. Между ними — сплошной кусок, который
        // копируется целиком, а не по символу: поиск в спане векторизован, а посимвольный
        // цикл нет. На обычном тексте таких символов единицы на тысячу.
        for (int i = start; i < source.Length; i++)
        {
            int next = IndexOfInteresting(source.Slice(i), decodeEntities, replaceNbsp, replaceTab);
            if (next < 0)
            {
                buffer.Write(source.Slice(i));
                return;
            }

            if (next > 0)
            {
                buffer.Write(source.Slice(i, next));
                i += next;
            }

            char c = source[i];

            if (replaceTab && c == '\t')
            {
                for (int k = 0; k < TabWidth; k++)
                {
                    buffer.Write(' ');
                }

                continue;
            }

            if (quot && c == '&' && TryDecodeQuot(source.Slice(i), out int quotLength))
            {
                buffer.Write('"');
                i += quotLength - 1;
                continue;
            }

            if (decodeEntities && c == '&'
                && EntityTable.TryDecode(source.Slice(i), out char value, out int consumed))
            {
                buffer.Write(replaceNbsp && value == Chars.Nbsp ? ' ' : value);
                i += consumed - 1;
                continue;
            }

            buffer.Write(replaceNbsp && c == Chars.Nbsp ? ' ' : c);
        }
    }

    /// <summary>
    /// Ближайший символ, который фазе есть смысл рассматривать: амперсанд (сущность),
    /// табуляция и неразрывный пробел. Набор зависит от включённых правил: искать то,
    /// что всё равно не будет тронуто, значит найти лишнюю границу и потерять кусок.
    /// </summary>
    private static int IndexOfInteresting(
        ReadOnlySpan<char> source, bool decodeEntities, bool replaceNbsp, bool replaceTab)
    {
        if (decodeEntities)
        {
            return replaceNbsp
                ? replaceTab ? source.IndexOfAny('&', Chars.Nbsp, '\t') : source.IndexOfAny('&', Chars.Nbsp)
                : replaceTab ? source.IndexOfAny('&', '\t') : source.IndexOf('&');
        }

        return replaceNbsp
            ? replaceTab ? source.IndexOfAny(Chars.Nbsp, '\t') : source.IndexOf(Chars.Nbsp)
            : replaceTab ? source.IndexOf('\t') : -1;
    }

    /// <summary>
    /// Сущность прямой кавычки: «&amp;quot;», «&amp;#34;» и «&amp;#x22;». Остальные сущности
    /// разметки (&amp;amp;, &amp;lt;, &amp;gt;) не декодируются никогда: они несут смысл
    /// разметки, и раскодировать их значило бы сделать текст разметкой.
    /// </summary>
    private static bool TryDecodeQuot(ReadOnlySpan<char> source, out int length)
    {
        if (source.StartsWith("&quot;".AsSpan(), StringComparison.Ordinal))
        {
            length = 6;
            return true;
        }

        if (source.StartsWith("&#".AsSpan(), StringComparison.Ordinal))
        {
            int end = source.Slice(0, Math.Min(source.Length, 8)).IndexOf(';');
            if (end > 2)
            {
                ReadOnlySpan<char> digits = source.Slice(2, end - 2);
                if (digits.SequenceEqual("34".AsSpan())
                    || digits.SequenceEqual("x22".AsSpan())
                    || digits.SequenceEqual("X22".AsSpan()))
                {
                    length = end + 1;
                    return true;
                }
            }
        }

        length = 0;
        return false;
    }

    /// <summary>
    /// Фаза Prepare по документу: текстовые узлы приводятся к одному представлению,
    /// разметка и защищённые зоны копируются байт в байт.
    /// </summary>
    /// <param name="html">Исходный документ.</param>
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

            // Начало документа — свойство ДОКУМЕНТА, а не сегмента: метка порядка байт
            // допустима только в самом начале входа, внутри текста она остаётся символом.
            Run(slice, decodeEntities: true, rules, ref buffer, isDocumentStart: segment.Start == 0);
        }
    }
}
