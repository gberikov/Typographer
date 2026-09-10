using Typographer.Rules;

namespace Typographer.Internal.Bind;

/// <summary>Правила даты фазы Bind.</summary>
/// <remarks>
/// Оба правила — перезаписи по соглашению <see cref="RewriteRules"/>: они меняют записанный
/// токен и приводят к нему стековую копию.
/// Правило ISO-формата меняет ДАННЫЕ, а не оформление, и на техническом тексте это вред.
/// Поэтому предикат сужен до безусловно датного вида: ровно «дддд-дд-дд», месяц не больше
/// двенадцати, день не больше тридцати одного. Число из пяти цифр, адрес и диапазон лет
/// правилом не затрагиваются по построению — токен собирает соседние цифры целиком, и лишняя
/// цифра ломает длину.
/// </remarks>
internal static class DateRules
{
    /// <summary>Длина ISO-даты без хвостовых точек: «2018-10-10».</summary>
    private const int IsoLength = 10;

    /// <summary>Заменяет токен, если этого требует правило даты.</summary>
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
        if (state.TokenOverflow)
        {
            return;
        }

        if (rules.Contains(RuleId.Ru.Date.FromIso) && TryIsoDate(token, ref state, ref buffer))
        {
            return;
        }

        if (rules.Contains(RuleId.Ru.Date.Weekday))
        {
            TryLowercase(token, previous, boundary, ref state, ref buffer);
        }
    }

    /// <summary>«2018-10-10» становится «10.10.2018». Хвостовые точки остаются на месте.</summary>
    private static bool TryIsoDate(Span<char> token, ref BindState state, ref CharBuffer buffer)
    {
        ReadOnlySpan<char> current = token.Slice(0, state.TokenLength);

        // Точка в конце — конец предложения, а не часть даты: «Дата 2018-10-10.».
        int end = current.Length;
        while (end > 0 && current[end - 1] == '.')
        {
            end--;
        }

        ReadOnlySpan<char> core = current.Slice(0, end);
        if (!IsIsoDate(core))
        {
            return false;
        }

        Span<char> result = stackalloc char[current.Length];
        core.Slice(8, 2).CopyTo(result);
        result[2] = '.';
        core.Slice(5, 2).CopyTo(result.Slice(3));
        result[5] = '.';
        core.Slice(0, 4).CopyTo(result.Slice(6));
        current.Slice(end).CopyTo(result.Slice(IsoLength));

        return RewriteRules.Replace(result, token, state.TokenStart, ref state, ref buffer);
    }

    /// <summary>Токен — дата вида «дддд-дд-дд» с осмысленным месяцем и днём.</summary>
    private static bool IsIsoDate(ReadOnlySpan<char> core)
    {
        if (core.Length != IsoLength || core[4] != '-' || core[7] != '-')
        {
            return false;
        }

        for (int i = 0; i < IsoLength; i++)
        {
            if (i is 4 or 7)
            {
                continue;
            }

            if (!char.IsDigit(core[i]))
            {
                return false;
            }
        }

        int month = ((core[5] - '0') * 10) + (core[6] - '0');
        int day = ((core[8] - '0') * 10) + (core[9] - '0');
        return month is >= 1 and <= 12 && day is >= 1 and <= 31;
    }

    /// <summary>
    /// Название месяца или дня недели с прописной буквы становится строчным.
    /// Месяц — когда слева стоит число: «2 Мая». День недели — когда справа запятая или
    /// слева уже стоит месяц: «Понедельник, 9 сентября», «9 сентября, Понедельник».
    /// Иначе слово может быть именем собственным или началом предложения.
    /// </summary>
    private static bool TryLowercase(
        Span<char> token, ReadOnlySpan<char> previous, char boundary,
        ref BindState state, ref CharBuffer buffer)
    {
        ReadOnlySpan<char> current = token.Slice(0, state.TokenLength);
        if (!IsCapitalized(current))
        {
            return false;
        }

        bool month = Dictionaries.IsMonth(current) && state.PrevKind == TokenKind.Number && state.PrevLength > 0;
        bool weekday = Dictionaries.IsWeekday(current)
            && (boundary == ',' || Dictionaries.IsMonth(previous));

        if (!month && !weekday)
        {
            return false;
        }

        Span<char> lower = stackalloc char[current.Length];
        current.CopyTo(lower);
        lower[0] = char.ToLowerInvariant(lower[0]);
        return RewriteRules.Replace(lower, token, state.TokenStart, ref state, ref buffer);
    }

    /// <summary>
    /// Первая буква прописная, остальные строчные. Слово, набранное прописными целиком, —
    /// заголовок, а не ошибка регистра, и трогать его нельзя.
    /// </summary>
    private static bool IsCapitalized(ReadOnlySpan<char> token)
    {
        if (token.Length < 2 || !char.IsUpper(token[0]))
        {
            return false;
        }

        for (int i = 1; i < token.Length; i++)
        {
            if (char.IsUpper(token[i]))
            {
                return false;
            }
        }

        return true;
    }
}
