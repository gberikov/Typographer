using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>Правила типографских символов фазы Scan.</summary>
/// <remarks>
/// Здесь собраны правила, которые собирают ОДИН знак из нескольких символов входа. Такое
/// правило записывает знак и выставляет <see cref="ScanState.Skip"/> — число съеденных
/// символов сверх текущего; курсор двигает диспетчер. Читать буфер, чтобы понять, что
/// предыдущий символ уже был частью знака, для этого не годится: во входе бывает и готовый
/// знак («©cat»), и правило съело бы букву за хвост образца.
/// </remarks>
internal static class SymbolRules
{
    public static bool TryApply(
        ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        char c = source[index];

        if (c == '(' && rules.Contains(RuleId.Common.Symbols.Copy)
            && TryWriteSign(source, index, ref state, ref buffer))
        {
            return true;
        }

        // Стрелка собирается из двух символов, и первый из них — дефис или угловая скобка,
        // за которые борются другие правила. Диспетчер спрашивает стрелку ПЕРВОЙ: «->» не
        // тире, а «<-» не начало тега.
        if (rules.Contains(RuleId.Common.Symbols.Arrow) && index + 1 < source.Length)
        {
            if (c == '-' && source[index + 1] == '>')
            {
                buffer.Write(Chars.ArrowRight);
                state.Skip = 1;
                return true;
            }

            if (c == '<' && source[index + 1] == '-')
            {
                buffer.Write(Chars.ArrowLeft);
                state.Skip = 1;
                return true;
            }
        }

        if (c is 'C' or 'F' && rules.Contains(RuleId.Common.Symbols.Cf)
            && IsTemperature(source, index, ref buffer, floor))
        {
            buffer.Write(Chars.Degree);
            buffer.Write(c);
            return true;
        }

        // Сдвоенный знак номера: второй подряд не пишется. Проверка по левому контексту
        // здесь безопасна — знак номера не бывает хвостом другого образца.
        if (c == Chars.Numero && previous == Chars.Numero && rules.Contains(RuleId.Ru.Symbols.NN))
        {
            return true;
        }

        return false;
    }

    /// <summary>Скобочная запись знака: «(c)», «(r)», «(tm)» и те же в верхнем регистре.</summary>
    private static bool TryWriteSign(
        ReadOnlySpan<char> source, int index, ref ScanState state, ref CharBuffer buffer)
    {
        ReadOnlySpan<char> rest = source.Slice(index);

        if (StartsWith(rest, "(c)"))
        {
            buffer.Write(Chars.Copyright);
            state.Skip = 2;
            return true;
        }

        if (StartsWith(rest, "(r)"))
        {
            buffer.Write(Chars.Registered);
            state.Skip = 2;
            return true;
        }

        if (StartsWith(rest, "(tm)"))
        {
            buffer.Write(Chars.Trademark);
            state.Skip = 3;
            return true;
        }

        return false;
    }

    /// <summary>Сравнение без учёта регистра и без аллокаций: образцы — только латиница.</summary>
    private static bool StartsWith(ReadOnlySpan<char> source, string pattern)
    {
        if (source.Length < pattern.Length)
        {
            return false;
        }

        for (int i = 0; i < pattern.Length; i++)
        {
            char a = source[i];
            char b = pattern[i];
            if (a != b && (char)(a | 0x20) != b)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// «25 C» — температура: слева от буквы пробел, а за ним цифра, справа буквы нет.
    /// Без проверки справа «10 Cm» превратилось бы в «10 °Cm».
    /// </summary>
    private static bool IsTemperature(ReadOnlySpan<char> source, int index, ref CharBuffer buffer, int floor)
    {
        if (index + 1 < source.Length && char.IsLetter(source[index + 1]))
        {
            return false;
        }

        if (buffer.Length - floor < 2)
        {
            return false;
        }

        char space = buffer.CharAt(buffer.Length - 1);
        char digit = buffer.CharAt(buffer.Length - 2);
        return space is ' ' or Chars.Nbsp && char.IsDigit(digit);
    }
}
