using Typographer.Rules;

namespace Typographer.Internal.Bind;

/// <summary>Отбивка знаков номера, параграфа и абзаца от того, что за ними следует.</summary>
/// <remarks>
/// Соглашение о СИМВОЛЬНОМ правиле фазы Bind: правило получает документ целиком и позицию в
/// нём, пишет в буфер само и сообщает через <see cref="BindState.Skip"/>, сколько
/// дополнительных символов документа проглотило. Токеном такие правила не выражаются: знак
/// номера токен не начинает, а решение зависит от правого контекста, которого у токена нет.
/// Правило ВСТАВЛЯЕТ символ, которого во входе не было. Идемпотентность держится на том, что
/// уже стоящий неразрывный пробел (обычный или узкий) распознаётся как отбивка и
/// проглатывается: второй прогон пишет ровно то же.
/// </remarks>
internal static class MarkRules
{
    /// <summary>Отбивает знак, если на позиции <paramref name="index"/> он и стоит.</summary>
    /// <param name="document">Документ после фазы Scan.</param>
    /// <param name="index">Позиция разбираемого символа.</param>
    /// <param name="end">Граница текущего текстового сегмента.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние фазы.</param>
    /// <param name="buffer">Буфер вывода.</param>
    /// <returns>Знак записан правилом; диспетчеру писать нечего.</returns>
    public static bool TryApply(
        ReadOnlySpan<char> document, int index, int end,
        RuleSet rules, ref BindState state, ref CharBuffer buffer)
    {
        char mark = document[index];
        char space = mark switch
        {
            Chars.Numero when rules.Contains(RuleId.Ru.Nbsp.AfterNumberSign) => Chars.NarrowNbsp,
            Chars.Section when rules.Contains(RuleId.Common.Nbsp.AfterSectionMark) => Chars.NarrowNbsp,
            Chars.Pilcrow when rules.Contains(RuleId.Common.Nbsp.AfterParagraphMark) => Chars.Nbsp,
            _ => '\0',
        };

        if (space == '\0' || index + 1 >= end)
        {
            return false;
        }

        // Отбивка ставится только перед номером или словом. Пробел, уже стоящий во входе,
        // проглатывается — его место занимает неразрывный.
        int next = index + 1;
        int skip = 0;
        if (document[next] is ' ' or Chars.Nbsp or Chars.NarrowNbsp)
        {
            skip = 1;
            next++;
        }

        if (next >= end || !char.IsLetterOrDigit(document[next]))
        {
            return false;
        }

        buffer.Write(mark);
        buffer.Write(space);
        state.Skip = skip;
        return true;
    }
}
