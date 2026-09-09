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
        if (isDocumentStart)
        {
            while (start < source.Length && source[start] == Chars.Bom)
            {
                start++;
            }
        }

        for (int i = start; i < source.Length; i++)
        {
            char c = source[i];

            if (decodeEntities && c == '&'
                && EntityTable.TryDecode(source.Slice(i), out char value, out int consumed))
            {
                buffer.Write(value);
                i += consumed - 1;
                continue;
            }

            buffer.Write(c);
        }
    }
}
