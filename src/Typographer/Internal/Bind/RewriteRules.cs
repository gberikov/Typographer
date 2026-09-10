using Typographer.Rules;

namespace Typographer.Internal.Bind;

/// <summary>Правила фазы Bind, которые заменяют записанный токен другим.</summary>
/// <remarks>
/// Соглашение о правиле-перезаписи: правило усекает буфер до <see cref="BindState.TokenStart"/>
/// (или до <see cref="BindState.PrevTokenStart"/>, когда два токена сливаются в один) и пишет
/// замену, а ТАКЖЕ приводит в соответствие сам <c>token</c> и
/// <see cref="BindState.TokenLength"/> — правила-склейки спрашиваются после и обязаны видеть
/// новый токен.
/// Переписывать разрешено только правее <see cref="BindState.SafeFrom"/>: левее лежит уже
/// скопированная разметка, и усечение съело бы её байты вопреки гарантии 3.
/// Замена не бывает длиннее стекового буфера токена — иначе правило отказывается: буфер
/// фиксированной длины, и обрезанный токен соврал бы правилам-склейкам.
/// </remarks>
internal static class RewriteRules
{
    /// <summary>Постскриптум с неразрывным пробелом внутри.</summary>
    private const string PostScriptum = "P.\u00A0S.";

    /// <summary>Второй постскриптум с неразрывными пробелами внутри.</summary>
    private const string PostPostScriptum = "P.\u00A0P.\u00A0S.";

    /// <summary>Квадратный метр надстрочным знаком.</summary>
    private const string SquareMeter = "м\u00B2";

    /// <summary>Кубический метр надстрочным знаком.</summary>
    private const string CubicMeter = "м\u00B3";

    /// <summary>Заменяет записанный токен, если какое-нибудь правило этого требует.</summary>
    /// <param name="token">Текущий токен; правило обязано обновить его вместе с буфером.</param>
    /// <param name="previous">Предыдущий токен.</param>
    /// <param name="boundary">Символ, закрывший токен; ноль — конец сегмента или документа.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние фазы.</param>
    /// <param name="buffer">Буфер вывода.</param>
    public static void TryRewrite(
        Span<char> token, ReadOnlySpan<char> previous, char boundary,
        RuleSet rules, ref BindState state, ref CharBuffer buffer)
    {
        _ = boundary;

        if (state.TokenOverflow)
        {
            return;
        }

        if (rules.Contains(RuleId.Ru.Nbsp.Years)
            && TryDoubleLetter(token, previous, "г.", "г.г.", "гг.", ref state, ref buffer))
        {
            return;
        }

        if (rules.Contains(RuleId.Ru.Nbsp.Centuries)
            && TryDoubleLetter(token, previous, "в.", "в.в.", "вв.", ref state, ref buffer))
        {
            return;
        }

        if (rules.Contains(RuleId.Ru.Nbsp.Ps) && TryPostScriptum(token, previous, ref state, ref buffer))
        {
            return;
        }

        if (rules.Contains(RuleId.Ru.Nbsp.M) && TryMeters(token, ref state, ref buffer))
        {
            return;
        }

        if (rules.Contains(RuleId.Common.Other.RepeatWord)
            && TryRepeatWord(token, previous, ref state, ref buffer))
        {
            return;
        }

        if (rules.Contains(RuleId.Ru.Other.Accent))
        {
            TryAccent(token, ref state, ref buffer);
        }
    }

    /// <summary>
    /// Повтор слова: второе из двух одинаковых подряд стирается вместе с пробелом перед ним.
    /// Остаётся ПЕРВОЕ — вместе с его регистром: «Повтор повтор» даёт «Повтор».
    /// </summary>
    private static bool TryRepeatWord(
        Span<char> token, ReadOnlySpan<char> previous, ref BindState state, ref CharBuffer buffer)
    {
        return state.Kind == TokenKind.Word
            && state.PrevKind == TokenKind.Word
            && state.PrevLength > 0
            && Same(token.Slice(0, state.TokenLength), previous)
            && Merge(previous, token, ref state, ref buffer);
    }

    /// <summary>
    /// Ударение: единственная прописная буква ВНУТРИ слова становится строчной, и за ней
    /// ставится комбинирующий акут. Буква обязана быть гласной — ударение на согласной
    /// бессмысленно, а прописная согласная внутри слова бывает в сокращениях и марках.
    /// </summary>
    private static bool TryAccent(Span<char> token, ref BindState state, ref CharBuffer buffer)
    {
        ReadOnlySpan<char> current = token.Slice(0, state.TokenLength);
        if (state.Kind != TokenKind.Word || current.Length < 2 || current.Length + 1 > token.Length)
        {
            return false;
        }

        int stressed = -1;
        for (int i = 0; i < current.Length; i++)
        {
            if (!char.IsUpper(current[i]))
            {
                continue;
            }

            // Прописная первая — начало предложения; вторая прописная — аббревиатура.
            if (i == 0 || stressed >= 0)
            {
                return false;
            }

            stressed = i;
        }

        if (stressed < 0 || !IsVowel(current[stressed]))
        {
            return false;
        }

        Span<char> accented = stackalloc char[current.Length + 1];
        current.Slice(0, stressed).CopyTo(accented);
        accented[stressed] = char.ToLowerInvariant(current[stressed]);
        accented[stressed + 1] = Chars.Acute;
        current.Slice(stressed + 1).CopyTo(accented.Slice(stressed + 2));

        return Replace(accented, token, state.TokenStart, ref state, ref buffer);
    }

    /// <summary>Буква — гласная русского алфавита.</summary>
    private static bool IsVowel(char c)
        => char.ToLowerInvariant(c) is 'а' or 'е' or 'ё' or 'и' or 'о' or 'у' or 'ы' or 'э' or 'ю' or 'я';

    /// <summary>
    /// Сдвоенное сокращение: «г.г.» и «г. г.» сводятся к «гг.», «в.в.» и «в. в.» — к «вв.».
    /// </summary>
    /// <param name="token">Текущий токен.</param>
    /// <param name="previous">Предыдущий токен.</param>
    /// <param name="single">Сокращение в одиночной форме: «г.».</param>
    /// <param name="glued">Две одиночных формы подряд без пробела: «г.г.».</param>
    /// <param name="doubled">Сокращение в сдвоенной форме: «гг.».</param>
    /// <param name="state">Состояние фазы.</param>
    /// <param name="buffer">Буфер вывода.</param>
    private static bool TryDoubleLetter(
        Span<char> token, ReadOnlySpan<char> previous, string single, string glued, string doubled,
        ref BindState state, ref CharBuffer buffer)
    {
        ReadOnlySpan<char> current = token.Slice(0, state.TokenLength);

        // Одним токеном: «г.г.» — точка внутри токена, разбирать нечего.
        if (Same(current, glued))
        {
            if (!Replace(doubled, token, state.TokenStart, ref state, ref buffer))
            {
                return false;
            }
        }
        else if (Same(current, single) && Same(previous, single))
        {
            // Двумя токенами через пробел: «г. г.». Пробел между ними исчезает, поэтому
            // склеивать назад надо уже пробел ПЕРЕД первым из них — иначе неразрывный
            // появится только на втором прогоне, а это разрыв идемпотентности.
            if (!Merge(doubled, token, ref state, ref buffer))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (state.SpaceIndex >= 0)
        {
            buffer.PatchAt(state.SpaceIndex, Chars.Nbsp);
        }

        return true;
    }

    /// <summary>
    /// Постскриптум: «P.S.» и «P. S.» сводятся к «P.&#160;S.», «P.P.S.» — к «P.&#160;P.&#160;S.».
    /// Сравнение с латиницей и по коду символа: кириллические «Р.» и «С.» — инициалы, а не
    /// постскриптум.
    /// </summary>
    private static bool TryPostScriptum(
        Span<char> token, ReadOnlySpan<char> previous, ref BindState state, ref CharBuffer buffer)
    {
        ReadOnlySpan<char> current = token.Slice(0, state.TokenLength);

        if (Same(current, "P.S."))
        {
            return Replace(PostScriptum, token, state.TokenStart, ref state, ref buffer);
        }

        if (Same(current, "P.P.S."))
        {
            return Replace(PostPostScriptum, token, state.TokenStart, ref state, ref buffer);
        }

        // Форма через пробел собирается по одному звену: «P. P. S.» проходит здесь дважды,
        // и на втором разе слева стоит уже склеенное «P.<nbsp>P.».
        if ((Same(current, "S.") || Same(current, "P.")) && EndsWithLatinP(previous))
        {
            Span<char> merged = stackalloc char[previous.Length + 1 + current.Length];
            previous.CopyTo(merged);
            merged[previous.Length] = Chars.Nbsp;
            current.CopyTo(merged.Slice(previous.Length + 1));
            return Merge(merged, token, ref state, ref buffer);
        }

        return false;
    }

    /// <summary>Квадратные и кубические метры: «м2» и «м3» с надстрочным знаком.</summary>
    private static bool TryMeters(Span<char> token, ref BindState state, ref CharBuffer buffer)
    {
        ReadOnlySpan<char> current = token.Slice(0, state.TokenLength);
        string? replacement = Same(current, "м2") ? SquareMeter
            : Same(current, "м3") ? CubicMeter
            : null;

        if (replacement is null || !Replace(replacement, token, state.TokenStart, ref state, ref buffer))
        {
            return false;
        }

        // ГОСТ 16.1 требует и надстрочный знак, и неразрывный пробел между числом и единицей.
        if (state.SpaceIndex >= 0)
        {
            buffer.PatchAt(state.SpaceIndex, Chars.Nbsp);
        }

        return true;
    }

    /// <summary>
    /// Заменяет хвост буфера начиная с <paramref name="from"/> на замену и приводит к ней
    /// стековую копию токена. Общий помощник всех правил-перезаписей, включая
    /// <see cref="DateRules"/>.
    /// </summary>
    /// <param name="replacement">Новое содержимое токена.</param>
    /// <param name="token">Стековая копия токена.</param>
    /// <param name="from">Позиция в буфере, с которой начинается заменяемый отрезок.</param>
    /// <param name="state">Состояние фазы.</param>
    /// <param name="buffer">Буфер вывода.</param>
    public static bool Replace(
        ReadOnlySpan<char> replacement, Span<char> token, int from,
        ref BindState state, ref CharBuffer buffer)
    {
        if (from < state.SafeFrom || replacement.Length > token.Length)
        {
            return false;
        }

        buffer.Truncate(from);
        buffer.Write(replacement);
        replacement.CopyTo(token);
        state.TokenStart = from;
        state.TokenLength = replacement.Length;
        return true;
    }

    /// <summary>
    /// Сливает предыдущий токен, пробел между токенами и текущий токен в один.
    /// Требует, чтобы между токенами был ровно один пробел и чтобы весь отрезок был
    /// записан этим проходом: иначе между ними лежит разметка или знак препинания.
    /// </summary>
    private static bool Merge(
        ReadOnlySpan<char> replacement, Span<char> token, ref BindState state, ref CharBuffer buffer)
    {
        bool contiguous = state.SpaceIndex >= 0
            && state.SpaceIndex == state.TokenStart - 1
            && state.PrevTokenStart + state.PrevLength == state.SpaceIndex;

        if (!contiguous || !Replace(replacement, token, state.PrevTokenStart, ref state, ref buffer))
        {
            return false;
        }

        // Пробел перед слитым токеном — это пробел, который стоял перед ПЕРВЫМ из двух.
        state.SpaceIndex = state.PrevSpaceIndex;
        return true;
    }

    /// <summary>Токен оканчивается латинским «P.» — звеном постскриптума.</summary>
    private static bool EndsWithLatinP(ReadOnlySpan<char> token)
        => token.Length >= 2 && token[token.Length - 1] == '.'
            && token[token.Length - 2] is 'P' or 'p';

    /// <summary>
    /// Сравнение с образцом без учёта регистра латиницы и кириллицы, по коду символа.
    /// </summary>
    private static bool Same(ReadOnlySpan<char> token, ReadOnlySpan<char> sample)
    {
        if (token.Length != sample.Length)
        {
            return false;
        }

        for (int i = 0; i < sample.Length; i++)
        {
            if (char.ToLowerInvariant(token[i]) != char.ToLowerInvariant(sample[i]))
            {
                return false;
            }
        }

        return true;
    }
}
