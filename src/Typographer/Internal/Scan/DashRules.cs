using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>Правила тире фазы Scan.</summary>
internal static class DashRules
{
    /// <summary>Длина года в цифрах — правило диапазона годов работает только с ней.</summary>
    internal const int YearDigits = 4;

    /// <summary>Самая длинная частица — «нибудь», шесть букв. Восемь взяты с запасом.</summary>
    private const int MaxParticle = 8;

    /// <summary>Самое длинное словарное слово — «понедельник», одиннадцать букв.</summary>
    private const int MaxDictionaryWord = 16;

    public static bool TryApply(
        ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        // Пробел попадает сюда ради частиц, которые автор набрал через пробел вместо дефиса:
        // «кое что», «из под». Правило почти всегда отказывается, и символ достаётся
        // правилу пробелов.
        if (source[index] == ' ')
        {
            return TryHyphenateParticle(source, index, floor, rules, ref buffer);
        }

        char next = index + 1 < source.Length ? source[index + 1] : '\0';

        // Тире прямой речи: дефис в начале документа или строки, за ним пробел.
        if (rules.Contains(RuleId.Ru.Dash.DirectSpeech) && next == ' ' && previous is '\0' or '\n')
        {
            buffer.Write(Chars.MDash);
            return true;
        }

        // Диапазон годов: ровно по четыре цифры с каждой стороны и ни одной лишней
        // цифры рядом. Без счёта цифр правило срабатывало на любой паре «цифра —
        // дефис — цифра» и рвало телефоны (+7-999-123-45-67), даты (01-01-2020) и
        // пути в ссылках (/2024-01-15/).
        if (rules.Contains(RuleId.Ru.Dash.Years)
            && IsYearBefore(ref buffer, floor, state.TrailingDigits) && IsYearAfter(source, index + 1))
        {
            buffer.Write(Chars.MDash);
            return true;
        }

        // Века римскими цифрами: «XIX-XX вв.». Тире длинное и без отбивки — как и в
        // диапазоне годов; разнобой между интервалами хуже, чем строгость ГОСТа.
        if (rules.Contains(RuleId.Ru.Dash.Centuries)
            && IsRomanBefore(ref buffer, floor) && IsRomanAfter(source, index + 1))
        {
            buffer.Write(Chars.MDash);
            return true;
        }

        // Десятилетия: «80-90-е гг.». Признак — цифры с обеих сторон и наращение через
        // дефис за правым числом, иначе это диапазон чисел, а не десятилетий.
        if (rules.Contains(RuleId.Ru.Dash.Decade)
            && char.IsDigit(previous) && IsDecadeAfter(source, index + 1))
        {
            buffer.Write(Chars.MDash);
            return true;
        }

        // Интервалы времени: «10:00-11:00». С обеих сторон часы с минутами.
        if (rules.Contains(RuleId.Ru.Dash.Time)
            && IsTimeBefore(ref buffer, floor) && IsTimeAfter(source, index + 1))
        {
            buffer.Write(Chars.MDash);
            return true;
        }

        // Дни одного месяца: «5-10 января». Справа число, а за ним название месяца —
        // без него это диапазон чисел, который трогать нельзя.
        if (rules.Contains(RuleId.Ru.Dash.DaysMonth)
            && char.IsDigit(previous) && IsDayMonthAfter(source, index + 1))
        {
            buffer.Write(Chars.MDash);
            return true;
        }

        // Месяцы и дни недели: «январь-февраль», «понедельник-среда». Слово читается слева
        // из буфера и справа из исходной строки, оба сверяются со словарём.
        if (rules.Contains(RuleId.Ru.Dash.Month) && IsDictionaryPair(source, index, floor, ref buffer, months: true))
        {
            buffer.Write(Chars.MDash);
            return true;
        }

        if (rules.Contains(RuleId.Ru.Dash.Weekday) && IsDictionaryPair(source, index, floor, ref buffer, months: false))
        {
            buffer.Write(Chars.MDash);
            return true;
        }

        // Тире между словами: пробел с обеих сторон, справа не число. Слева годится и
        // неразрывный пробел: в буфере он мог оказаться от предыдущего правила, и по
        // классу это тот же пробел — иначе тире молча не ставится. Справа годится и
        // перевод строки: в тексте, свёрстанном по ширине, тире регулярно оказывается
        // последним символом строки, и это то же самое тире.
        if (rules.Contains(RuleId.Ru.Dash.Main)
            && (previous is ' ' or Chars.Nbsp) && next is ' ' or '\n' or '\r'
            && !IsNumberAhead(source, index + 2))
        {
            // Патчится ровно та позиция, по которой принято решение. Если сегмент
            // пуст, пробел уехал в вывод с предыдущим сегментом и патчу недоступен.
            if (buffer.Length > floor)
            {
                buffer.PatchAt(buffer.Length - 1, Chars.Nbsp);
            }

            buffer.Write(Chars.MDash);
            return true;
        }

        // Английское тире. Спрашивается ПОСЛЕ русского: если включены оба, побеждает
        // русское — язык библиотеки русский, а эти правила берут осознанно.
        if ((previous is ' ' or Chars.Nbsp) && next == ' ')
        {
            // Британская традиция: короткое тире с отбивкой пробелами.
            if (rules.Contains(RuleId.EnGb.Dash.Main))
            {
                buffer.Write(Chars.NDash);
                return true;
            }

            // Американская: длинное тире вплотную к словам. Пробел слева уже в буфере —
            // он усекается, пробел справа съедается через Skip.
            if (rules.Contains(RuleId.EnUs.Dash.Main))
            {
                if (buffer.Length > floor)
                {
                    buffer.Truncate(buffer.Length - 1);
                }

                buffer.Write(Chars.MDash);
                state.Skip = 1;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Первый непробельный символ справа — цифра.
    /// </summary>
    /// <remarks>
    /// Пробелы пропускаются, а не проверяется одна фиксированная позиция. Правило «удалить
    /// повторный пробел» схлопнет их позже, и решение по дефису обязано быть одинаковым для
    /// «текст - 5» и «текст -  5»: иначе лишний пробел во входе молча превращал минус перед
    /// числом в тире.
    /// </remarks>
    private static bool IsNumberAhead(ReadOnlySpan<char> source, int index)
    {
        while (index < source.Length && source[index] == ' ')
        {
            index++;
        }

        return index < source.Length && char.IsDigit(source[index]);
    }

    /// <summary>Четыре цифры подряд перед дефисом и ни одной пятой — это год.</summary>
    private static bool IsYearBefore(ref CharBuffer buffer, int floor, int beforeBuffer)
    {
        int written = buffer.Length - floor;
        int digits = 0;
        while (digits < written && digits <= YearDigits
               && char.IsDigit(buffer.CharAt(buffer.Length - digits - 1)))
        {
            digits++;
        }

        if (digits == written)
        {
            digits = Math.Min(YearDigits + 1, digits + beforeBuffer);
        }

        return digits == YearDigits;
    }

    /// <summary>Четыре цифры подряд после дефиса и ни одной пятой — это год.</summary>
    private static bool IsYearAfter(ReadOnlySpan<char> source, int start)
    {
        if (start + YearDigits > source.Length)
        {
            return false;
        }

        for (int k = 0; k < YearDigits; k++)
        {
            if (!char.IsDigit(source[start + k]))
            {
                return false;
            }
        }

        return start + YearDigits == source.Length || !char.IsDigit(source[start + YearDigits]);
    }

    /// <summary>
    /// Частицы, которые пишутся через дефис, а автор набрал их через пробел. Правило не
    /// ставит тире, а наоборот — не даёт пробелу дожить до правила тире и заодно чинит
    /// орфографию.
    /// </summary>
    /// <remarks>
    /// Слово слева читается из буфера, слово справа — из исходной строки; и то и другое
    /// ограничено восемью буквами, длиннее ни одна частица не бывает.
    /// </remarks>
    private static bool TryHyphenateParticle(
        ReadOnlySpan<char> source, int index, int floor, RuleSet rules, ref CharBuffer buffer)
    {
        Span<char> right = stackalloc char[MaxParticle];
        int rightLength = ReadWordForward(source, index + 1, right);
        if (rightLength == 0)
        {
            return false;
        }

        Span<char> left = stackalloc char[MaxParticle];
        int leftLength = ReadWordBackward(ref buffer, floor, left);
        if (leftLength == 0)
        {
            return false;
        }

        ReadOnlySpan<char> after = right.Slice(0, rightLength);
        ReadOnlySpan<char> before = left.Slice(0, leftLength);

        bool hyphen =
            (rules.Contains(RuleId.Ru.Dash.To) && (Is(after, "то") || Is(after, "либо") || Is(after, "нибудь")))
            || (rules.Contains(RuleId.Ru.Dash.Ka) && (Is(after, "ка") || Is(after, "кась")))
            || (rules.Contains(RuleId.Ru.Dash.Taki) && Is(after, "таки"))
            || (rules.Contains(RuleId.Ru.Dash.Koe) && (Is(before, "кое") || Is(before, "кой")))
            || (rules.Contains(RuleId.Ru.Dash.Izpod) && Is(before, "из") && Is(after, "под"))
            || (rules.Contains(RuleId.Ru.Dash.Izza) && Is(before, "из") && Is(after, "за"))
            || (rules.Contains(RuleId.Ru.Dash.KakTo) && Is(before, "как") && Is(after, "то"))
            || (rules.Contains(RuleId.Ru.Dash.De) && Is(after, "де"));

        if (!hyphen)
        {
            return false;
        }

        buffer.Write('-');
        return true;
    }

    /// <summary>Слово справа от позиции: буквы до первого небуквенного символа.</summary>
    private static int ReadWordForward(ReadOnlySpan<char> source, int start, Span<char> word)
    {
        int length = 0;
        while (start + length < source.Length && char.IsLetter(source[start + length]))
        {
            if (length == word.Length)
            {
                return 0;
            }

            word[length] = char.ToLowerInvariant(source[start + length]);
            length++;
        }

        // Слово, дошедшее до конца узла, считается целым: в обычном тексте это конец
        // ввода, а в разметке — половина слова, которая всё равно не совпадёт ни с одной
        // частицей, и правило откажется само.
        return length;
    }

    /// <summary>Слово слева от конца буфера: буквы назад до floor или небуквенного символа.</summary>
    private static int ReadWordBackward(ref CharBuffer buffer, int floor, Span<char> word)
    {
        int end = buffer.Length;
        int length = 0;
        while (end - length > floor && char.IsLetter(buffer.CharAt(end - length - 1)))
        {
            if (length == word.Length)
            {
                return 0;
            }

            length++;
        }

        for (int i = 0; i < length; i++)
        {
            word[i] = char.ToLowerInvariant(buffer.CharAt(end - length + i));
        }

        return length;
    }

    private static bool Is(ReadOnlySpan<char> word, string particle)
    {
        if (word.Length != particle.Length)
        {
            return false;
        }

        for (int i = 0; i < particle.Length; i++)
        {
            if (word[i] != particle[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Символ — римская цифра.</summary>
    private static bool IsRoman(char c) => c is 'I' or 'V' or 'X' or 'L' or 'C' or 'D' or 'M';

    /// <summary>Слева от дефиса римское число.</summary>
    private static bool IsRomanBefore(ref CharBuffer buffer, int floor)
        => buffer.Length > floor && IsRoman(buffer.CharAt(buffer.Length - 1));

    /// <summary>Справа от дефиса римское число, а за ним не буква кириллицы.</summary>
    private static bool IsRomanAfter(ReadOnlySpan<char> source, int start)
    {
        int length = 0;
        while (start + length < source.Length && IsRoman(source[start + length]))
        {
            length++;
        }

        return length > 0;
    }

    /// <summary>Справа от дефиса число с наращением: «90-е», «90-х».</summary>
    private static bool IsDecadeAfter(ReadOnlySpan<char> source, int start)
    {
        int digits = 0;
        while (start + digits < source.Length && char.IsDigit(source[start + digits]))
        {
            digits++;
        }

        if (digits == 0)
        {
            return false;
        }

        int tail = start + digits;
        return tail + 1 < source.Length && source[tail] == '-' && char.IsLetter(source[tail + 1]);
    }

    /// <summary>Слева от дефиса время вида «10:00»: минуты, двоеточие, часы.</summary>
    private static bool IsTimeBefore(ref CharBuffer buffer, int floor)
    {
        int written = buffer.Length - floor;
        if (written < 4)
        {
            return false;
        }

        int last = buffer.Length - 1;
        return char.IsDigit(buffer.CharAt(last))
               && char.IsDigit(buffer.CharAt(last - 1))
               && buffer.CharAt(last - 2) == ':'
               && char.IsDigit(buffer.CharAt(last - 3));
    }

    /// <summary>Справа от дефиса время вида «11:00».</summary>
    private static bool IsTimeAfter(ReadOnlySpan<char> source, int start)
    {
        int digits = 0;
        while (start + digits < source.Length && char.IsDigit(source[start + digits]))
        {
            digits++;
        }

        if (digits is 0 or > 2)
        {
            return false;
        }

        int colon = start + digits;
        return colon + 2 < source.Length && source[colon] == ':'
               && char.IsDigit(source[colon + 1]) && char.IsDigit(source[colon + 2]);
    }

    /// <summary>Справа от дефиса число, а за ним название месяца: «5-10 января».</summary>
    private static bool IsDayMonthAfter(ReadOnlySpan<char> source, int start)
    {
        int digits = 0;
        while (start + digits < source.Length && char.IsDigit(source[start + digits]))
        {
            digits++;
        }

        if (digits is 0 or > 2 || start + digits >= source.Length || source[start + digits] != ' ')
        {
            return false;
        }

        Span<char> month = stackalloc char[MaxDictionaryWord];
        int length = ReadWordForward(source, start + digits + 1, month);
        return length > 0 && Dictionaries.IsMonth(month.Slice(0, length));
    }

    /// <summary>
    /// По обе стороны дефиса слово из одного словаря: два месяца или два дня недели.
    /// </summary>
    private static bool IsDictionaryPair(
        ReadOnlySpan<char> source, int index, int floor, ref CharBuffer buffer, bool months)
    {
        Span<char> right = stackalloc char[MaxDictionaryWord];
        int rightLength = ReadWordForward(source, index + 1, right);
        if (rightLength == 0)
        {
            return false;
        }

        Span<char> left = stackalloc char[MaxDictionaryWord];
        int leftLength = ReadWordBackward(ref buffer, floor, left);
        if (leftLength == 0)
        {
            return false;
        }

        ReadOnlySpan<char> before = left.Slice(0, leftLength);
        ReadOnlySpan<char> after = right.Slice(0, rightLength);

        return months
            ? Dictionaries.IsMonth(before) && Dictionaries.IsMonth(after)
            : Dictionaries.IsWeekday(before) && Dictionaries.IsWeekday(after);
    }
}
