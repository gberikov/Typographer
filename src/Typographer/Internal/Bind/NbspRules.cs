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
/// Правила одного направления перечислены в одном предикате и спрашиваются подряд: действие
/// у них одно, и «первое сработавшее» тут не имеет смысла — важно только, склеивать или нет.
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
        // Предыдущий токен этому набору правил не нужен: всё, что о нём важно, лежит в
        // BindState — вид, длина и позиция пробела перед ним. Параметр остаётся ради
        // единообразия соглашения о правиле-склейке.
        _ = previous;

        // Внутри nobr и nowrap перенос уже запрещён тегом: склеивать нечего.
        if (state.NoWrap)
        {
            return;
        }

        bool hasDot = token[token.Length - 1] == '.';
        ReadOnlySpan<char> letters = hasDot ? token.Slice(0, token.Length - 1) : token;
        bool initial = rules.Contains(RuleId.Ru.Nbsp.Initials) && !state.TokenOverflow && IsInitial(token);

        if (state.SpaceIndex >= 0
            && (initial || BindsBackward(token, letters, hasDot, boundary, rules, ref state)))
        {
            buffer.PatchAt(state.SpaceIndex, Chars.Nbsp);
        }

        if (boundary is ' ' or Chars.Nbsp
            && (initial || BindsForward(token, letters, hasDot, rules, ref state)))
        {
            state.GlueForward = true;
        }
    }

    /// <summary>
    /// Пробел ПЕРЕД токеном становится неразрывным. Инициал сюда не входит: он связывается
    /// в обе стороны и решается отдельно.
    /// </summary>
    private static bool BindsBackward(
        ReadOnlySpan<char> token, ReadOnlySpan<char> letters, bool hasDot, char boundary,
        RuleSet rules, ref BindState state)
    {
        if (state.TokenOverflow)
        {
            return false;
        }

        // Последнее слово предложения не отрывается от предпоследнего. Точка входит в сам
        // токен, поэтому «там.» — признак конца предложения, а запятая — нет.
        if (IsSentenceEnd(boundary, hasDot))
        {
            if (rules.Contains(RuleId.Common.Nbsp.BeforeShortLastWord)
                && state.Kind == TokenKind.Word && Dictionaries.IsShortWord(letters))
            {
                return true;
            }

            if (rules.Contains(RuleId.Common.Nbsp.BeforeShortLastNumber)
                && state.Kind == TokenKind.Number && IsShortNumber(letters))
            {
                return true;
            }
        }

        // Частица не отрывается от предшествующего слова: «так ли», «он же», «если бы».
        if (rules.Contains(RuleId.Ru.Nbsp.BeforeParticle)
            && state.Kind == TokenKind.Word && Dictionaries.IsParticle(token))
        {
            return true;
        }

        // Разряды числа, разбитые пробелами автором: «1 000 000». Число внутри себя рвать
        // нельзя (ГОСТ 9.5), а разбивать его заново правилу разбиения уже нечего — остаётся
        // сделать неразрывными пробелы, которые в тексте стоят. Признак разряда строгий:
        // ровно три цифры справа и число слева, иначе «в 1941 1945» слиплось бы в одно.
        if (rules.Contains(RuleId.Common.Number.DigitGrouping)
            && state.PrevKind == TokenKind.Number && state.PrevLength > 0
            && state.Kind == TokenKind.Number && IsDigitGroup(token))
        {
            return true;
        }

        // Число слева, не число справа. «2026 2027» остаётся с обычным пробелом:
        // перечисление чисел рвать можно, а число и слово — нет (ГОСТ 9.4).
        if (state.PrevLength == 0 || state.PrevKind != TokenKind.Number || state.Kind == TokenKind.Number)
        {
            return false;
        }

        return rules.Contains(RuleId.Common.Nbsp.AfterNumber)
            || (rules.Contains(RuleId.Ru.Nbsp.DayMonth) && Dictionaries.IsMonth(letters))
            || (rules.Contains(RuleId.Ru.Nbsp.Year) && Dictionaries.IsYearAbbreviation(token))
            || (rules.Contains(RuleId.Ru.Nbsp.Mln) && Dictionaries.IsMagnitude(token))
            || (rules.Contains(RuleId.Ru.Nbsp.RubleKopek) && Dictionaries.IsMoneyAbbreviation(token))
            || (rules.Contains(RuleId.Common.Nbsp.Dpi) && Dictionaries.IsResolution(token));
    }

    /// <summary>Токен — разряд числа: ровно три цифры и ничего кроме них.</summary>
    private static bool IsDigitGroup(ReadOnlySpan<char> token)
    {
        if (token.Length != 3)
        {
            return false;
        }

        foreach (char c in token)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Пробел ПОСЛЕ токена становится неразрывным.</summary>
    private static bool BindsForward(
        ReadOnlySpan<char> token, ReadOnlySpan<char> letters, bool hasDot,
        RuleSet rules, ref BindState state)
    {
        if (state.TokenOverflow)
        {
            return false;
        }

        return (rules.Contains(RuleId.Ru.Nbsp.Addr) && Dictionaries.IsAddressAbbreviation(token))
            || (rules.Contains(RuleId.Ru.Nbsp.Page) && Dictionaries.IsPageAbbreviation(token))
            || (rules.Contains(RuleId.Ru.Nbsp.See) && Dictionaries.IsReferenceAbbreviation(token))
            || (rules.Contains(RuleId.Ru.Nbsp.Ooo) && Dictionaries.IsOrganization(token))
            || (rules.Contains(RuleId.Common.Nbsp.AfterShortWordByList)
                && state.Kind == TokenKind.Word && Dictionaries.IsFunctionWord(token))
            || (rules.Contains(RuleId.Common.Nbsp.AfterShortWord)
                && !hasDot && state.Kind == TokenKind.Word && Dictionaries.IsShortWord(letters))
            || (rules.Contains(RuleId.Ru.Nbsp.Abbr)
                && hasDot && Dictionaries.IsAbbreviationPart(letters));
    }

    /// <summary>
    /// Токен закрыт концом предложения: знаком «!», «?», многоточием, точкой внутри самого
    /// токена или концом документа. Запятая и точка с запятой границей предложения не являются.
    /// </summary>
    private static bool IsSentenceEnd(char boundary, bool hasDot)
        => hasDot || boundary is '!' or '?' or Chars.Hellip or '\0';

    /// <summary>Число не длиннее двух цифр.</summary>
    private static bool IsShortNumber(ReadOnlySpan<char> word)
    {
        if (word.Length is 0 or > 2)
        {
            return false;
        }

        foreach (char c in word)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Инициал — одна прописная буква с точкой: «А.». Связывается в обе стороны:
    /// «Пушкин А.» нуждается в неразрывном пробеле и слева от инициала, и справа.
    /// </summary>
    private static bool IsInitial(ReadOnlySpan<char> token)
        => token.Length == 2 && token[1] == '.' && char.IsUpper(token[0]);
}
