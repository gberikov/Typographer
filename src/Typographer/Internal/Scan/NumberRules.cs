using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>Правила чисел и знаков сравнения фазы Scan.</summary>
internal static class NumberRules
{
    /// <summary>С этой длины число разбивается по разрядам: «1000» ещё читается, «10000» уже нет.</summary>
    private const int GroupingThreshold = 5;

    public static bool TryApply(
        ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        char c = source[index];

        if (rules.Contains(RuleId.Common.Number.MathSigns) && index + 1 < source.Length)
        {
            char sign = source[index + 1] == '=' ? Compare(c) : c == '+' && source[index + 1] == '-' ? Chars.PlusMinus : '\0';
            if (sign != '\0')
            {
                buffer.Write(sign);
                state.Skip = 1;
                return true;
            }
        }

        if (c is '1' or '3' && rules.Contains(RuleId.Common.Number.Fraction)
            && TryWriteFraction(source, index, previous, ref state, ref buffer))
        {
            return true;
        }

        if (c == 'x' && rules.Contains(RuleId.Common.Number.Times)
            && TryWriteTimes(source, index, ref buffer, floor, ref state))
        {
            return true;
        }

        if (char.IsDigit(c) && rules.Contains(RuleId.Common.Number.DigitGrouping)
            && TryWriteGrouped(source, index, previous, ref state, ref buffer))
        {
            return true;
        }

        return false;
    }

    private static char Compare(char c) => c switch
    {
        '!' => Chars.NotEqual,
        '<' => Chars.LessOrEqual,
        '>' => Chars.GreaterOrEqual,
        '~' => Chars.ApproxEqual,
        _ => '\0',
    };

    /// <summary>
    /// Половина, четверть и три четверти. Другие дроби не собираются: в Юникоде их набор
    /// неполон, и оставить «5/7» как есть честнее, чем выдумывать замену.
    /// </summary>
    private static bool TryWriteFraction(
        ReadOnlySpan<char> source, int index, char previous, ref ScanState state, ref CharBuffer buffer)
    {
        // Дробь — самостоятельное число, а не хвост другого: «01/02» — это дата.
        if (char.IsDigit(previous) || index + 2 >= source.Length || source[index + 1] != '/')
        {
            return false;
        }

        if (index + 3 < source.Length && char.IsDigit(source[index + 3]))
        {
            return false;
        }

        char denominator = source[index + 2];
        char fraction = (source[index], denominator) switch
        {
            ('1', '2') => Chars.Half,
            ('1', '4') => Chars.Quarter,
            ('3', '4') => Chars.ThreeQuarters,
            _ => '\0',
        };

        if (fraction == '\0')
        {
            return false;
        }

        buffer.Write(fraction);
        state.Skip = 2;
        return true;
    }

    /// <summary>
    /// «10 x 5» становится «10×5»: знак умножения не отбивается пробелами. Пробел слева уже
    /// в буфере — он усекается; пробел справа съедается через <see cref="ScanState.Skip"/>.
    /// </summary>
    private static bool TryWriteTimes(
        ReadOnlySpan<char> source, int index, ref CharBuffer buffer, int floor, ref ScanState state)
    {
        if (buffer.Length - floor < 2 || index + 2 >= source.Length)
        {
            return false;
        }

        if (buffer.CharAt(buffer.Length - 1) != ' ' || !char.IsDigit(buffer.CharAt(buffer.Length - 2)))
        {
            return false;
        }

        if (source[index + 1] != ' ' || !char.IsDigit(source[index + 2]))
        {
            return false;
        }

        buffer.Truncate(buffer.Length - 1);
        buffer.Write(Chars.Times);
        state.Skip = 1;
        return true;
    }

    /// <summary>
    /// Разбивает длинное целое по три цифры справа налево, разделяя неразрывным пробелом.
    /// Дробная часть не трогается: разряды считают у целого, а «3.14159» — одно число.
    /// </summary>
    private static bool TryWriteGrouped(
        ReadOnlySpan<char> source, int index, char previous, ref ScanState state, ref CharBuffer buffer)
    {
        if (char.IsDigit(previous) || previous is '.' or ',')
        {
            return false;
        }

        int digits = 0;
        while (index + digits < source.Length && char.IsDigit(source[index + digits]))
        {
            digits++;
        }

        if (digits < GroupingThreshold)
        {
            return false;
        }

        for (int i = 0; i < digits; i++)
        {
            int fromRight = digits - i;
            if (i > 0 && fromRight % 3 == 0)
            {
                buffer.Write(Chars.Nbsp);
            }

            buffer.Write(source[index + i]);
        }

        state.Skip = digits - 1;
        return true;
    }
}
