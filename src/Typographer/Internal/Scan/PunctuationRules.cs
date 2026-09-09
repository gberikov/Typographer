using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>
/// Правила знаков препинания фазы Scan.
/// </summary>
/// <remarks>
/// Сигнатура <see cref="TryApply"/> — образец для всех правил фазы Scan (в том числе для
/// правил планов 2b и 2c): <c>true</c> означает «символ обработан и записан в буфер»,
/// <c>false</c> — «правило не применилось, символ пишет диспетчер». Параметр
/// <c>floor</c> — позиция в буфере, с которой начинаются записи ТЕКУЩЕГО текстового узла:
/// буфер общий на весь документ, и левее floor лежат чужие символы, в том числе разметка.
/// Правило не имеет права патчить или усекать буфер левее floor — иначе оно переписало бы
/// символ внутри тега и сломало бы гарантию «разметка байт в байт».
/// </remarks>
internal static class PunctuationRules
{
    public static bool TryApply(
        ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        char c = source[index];

        // Многоточие схлопывается ЗАДНИМ ЧИСЛОМ: третья подряд точка в буфере забирает
        // две предыдущие. По исходной строке это не решается — точки становятся соседями
        // уже в буфере, после того как удалён пробел между ними («текст. ..»).
        if (rules.Contains(RuleId.Common.Punctuation.Hellip) && c == '.'
            && (index + 1 >= source.Length || source[index + 1] != '.')
            && ClosesEllipsis(ref buffer, floor, state.Last))
        {
            buffer.Truncate(buffer.Length - 2);
            buffer.Write(Chars.Hellip);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Буфер оканчивается ровно двумя точками, то есть текущая точка — третья и последняя.
    /// Третий с конца символ проверяется по КЛАССУ: готовое многоточие — те же точки,
    /// иначе «……» получалось бы из шести точек за два прогона.
    /// </summary>
    /// <param name="buffer">Буфер документа.</param>
    /// <param name="floor">Позиция в буфере, с которой начинается текущий сегмент.</param>
    /// <param name="beforeBuffer">Символ слева от сегмента — последний символ прошлого сегмента.</param>
    private static bool ClosesEllipsis(ref CharBuffer buffer, int floor, char beforeBuffer)
    {
        int written = buffer.Length - floor;
        if (written < 2 || buffer.CharAt(buffer.Length - 1) != '.' || buffer.CharAt(buffer.Length - 2) != '.')
        {
            return false;
        }

        char third = written >= 3 ? buffer.CharAt(buffer.Length - 3) : beforeBuffer;
        return third is not ('.' or Chars.Hellip);
    }
}
