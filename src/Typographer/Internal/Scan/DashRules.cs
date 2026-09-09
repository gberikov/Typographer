using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>Правила тире фазы Scan.</summary>
internal static class DashRules
{
    /// <summary>Длина года в цифрах — правило диапазона годов работает только с ней.</summary>
    internal const int YearDigits = 4;

    public static bool TryApply(
        ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
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

        // Тире между словами: пробел с обеих сторон, справа не число. Слева годится и
        // неразрывный пробел: в буфере он мог оказаться от предыдущего правила, и по
        // классу это тот же пробел — иначе тире молча не ставится.
        if (rules.Contains(RuleId.Ru.Dash.Main)
            && (previous is ' ' or Chars.Nbsp) && next == ' ' && !IsNumberAhead(source, index + 2))
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
}
