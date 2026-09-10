using Typographer.Rules;

namespace Typographer.Internal.Bind;

/// <summary>Правила неразрывных пробелов фазы Bind.</summary>
/// <remarks>
/// Соглашение о правиле-склейке: решение принимается по ДВУМ токенам — текущему и
/// предыдущему — и по символу границы. Склейка ВПЕРЁД выражается через
/// <see cref="BindState.GlueForward"/>: пробел ещё не записан, патчить нечего, и диспетчер
/// напишет неразрывный вместо обычного. Склейка НАЗАД — патчем позиции
/// <see cref="BindState.SpaceIndex"/>: этот пробел проход записал сам, и патч разрешён на
/// любой его позиции, в том числе левее <see cref="BindState.SafeFrom"/> — пробел это текст,
/// а не разметка. Благодаря этому «Пушкин &lt;b&gt;А.&lt;/b&gt;» связывается через тег.
/// Переписывать буфер правило-склейка не имеет права вовсе.
/// </remarks>
internal static class NbspRules
{
    /// <summary>Решает, каким пробелом окружён текущий токен.</summary>
    /// <param name="token">Текущий токен.</param>
    /// <param name="previous">Предыдущий токен.</param>
    /// <param name="boundary">Символ, закрывший токен; ноль — конец сегмента или документа.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние фазы.</param>
    /// <param name="buffer">Буфер вывода.</param>
    public static void TryGlue(
        ReadOnlySpan<char> token, ReadOnlySpan<char> previous, char boundary,
        RuleSet rules, ref BindState state, ref CharBuffer buffer)
    {
        _ = previous;

        bool hasDot = token[token.Length - 1] == '.';
        ReadOnlySpan<char> letters = hasDot ? token.Slice(0, token.Length - 1) : token;

        // Склейка НАЗАД по предыдущему токену: число слева, не число справа. Три правила
        // делают одно действие и различаются только предикатом, поэтому спрашиваются
        // подряд, а не через выбор первого сработавшего. Правое условие «не число» нужно,
        // чтобы «2026 2027» осталось с обычным пробелом: перечисление чисел рвать можно.
        if (state.SpaceIndex >= 0 && state.PrevLength > 0
            && state.PrevKind == TokenKind.Number && state.Kind != TokenKind.Number
            && !state.TokenOverflow
            && (rules.Contains(RuleId.Common.Nbsp.AfterNumber)
                || (rules.Contains(RuleId.Ru.Nbsp.DayMonth) && Dictionaries.IsMonth(letters))
                || (rules.Contains(RuleId.Ru.Nbsp.Year) && Dictionaries.IsYearAbbreviation(token))
                || (rules.Contains(RuleId.Ru.Nbsp.Mln) && Dictionaries.IsMagnitude(token))
                || (rules.Contains(RuleId.Ru.Nbsp.RubleKopek) && Dictionaries.IsMoneyAbbreviation(token))
                || (rules.Contains(RuleId.Common.Nbsp.Dpi) && Dictionaries.IsResolution(token))))
        {
            buffer.PatchAt(state.SpaceIndex, Chars.Nbsp);
        }
        bool initial = rules.Contains(RuleId.Ru.Nbsp.Initials) && !state.TokenOverflow && IsInitial(token);

        // Инициал связывает себя не только со следующим словом, но и с предыдущим —
        // «Пушкин А.» нуждается в неразрывном пробеле по обе стороны от «А.».
        if (initial && state.SpaceIndex >= 0)
        {
            buffer.PatchAt(state.SpaceIndex, Chars.Nbsp);
        }

        bool glue = !state.TokenOverflow && boundary is ' ' or Chars.Nbsp
            && ((rules.Contains(RuleId.Common.Nbsp.AfterShortWord)
                    && !hasDot && state.Kind == TokenKind.Word && Dictionaries.IsShortWord(letters))
                || (rules.Contains(RuleId.Ru.Nbsp.Abbr)
                    && hasDot && Dictionaries.IsAbbreviationPart(letters))
                || initial);

        if (glue)
        {
            state.GlueForward = true;
        }
    }

    /// <summary>Инициал — одна прописная буква с точкой: «А.».</summary>
    private static bool IsInitial(ReadOnlySpan<char> token)
        => token.Length == 2 && token[1] == '.' && char.IsUpper(token[0]);
}
