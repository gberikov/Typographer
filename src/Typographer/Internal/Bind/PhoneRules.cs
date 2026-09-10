using Typographer.Rules;

namespace Typographer.Internal.Bind;

/// <summary>Форматирование телефонных номеров. Символьное правило фазы Bind.</summary>
/// <remarks>
/// Правило самое рискованное в наборе: оно входит в <see cref="RuleSet.Default"/> и
/// переписывает цифры. Поэтому предикат узкий: префикс «+7» или «8», РОВНО одиннадцать цифр,
/// разделители только пробел, неразрывный пробел, дефис и круглые скобки. Двенадцатая цифра
/// сразу за одиннадцатой отменяет разбор — артикул, номер счёта и версию правило не трогает.
/// Вывод — канонический «+7 999 123-45-67» с неразрывными пробелами. Оборачивать номер в
/// &lt;nobr&gt;, как делает веб-сервис Лебедева, нельзя: правило <see cref="RuleSet.Default"/>
/// не создаёт разметку из текста (гарантия 4).
/// </remarks>
internal static class PhoneRules
{
    /// <summary>Сколько цифр в российском номере вместе с кодом страны.</summary>
    private const int DigitCount = 11;

    /// <summary>Разделитель внутри номера.</summary>
    private static bool IsSeparator(char c)
        => c is ' ' or '-' or '(' or ')' or Chars.Nbsp or Chars.NarrowNbsp;

    /// <summary>Форматирует номер, если он начинается на позиции <paramref name="index"/>.</summary>
    /// <param name="document">Документ после фазы Scan.</param>
    /// <param name="index">Позиция разбираемого символа: «+» или первая цифра номера.</param>
    /// <param name="end">Граница текущего текстового сегмента.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние фазы.</param>
    /// <param name="buffer">Буфер вывода.</param>
    /// <returns>Номер записан правилом; диспетчеру писать нечего.</returns>
    public static bool TryApply(
        ReadOnlySpan<char> document, int index, int end,
        RuleSet rules, ref BindState state, ref CharBuffer buffer)
    {
        if (!rules.Contains(RuleId.Ru.Other.PhoneNumber))
        {
            return false;
        }

        char first = document[index];
        bool plus = first == '+';
        if (!plus && first != '8')
        {
            return false;
        }

        Span<char> digits = stackalloc char[DigitCount];
        int count = 0;
        int last = -1;

        for (int i = plus ? index + 1 : index; i < end; i++)
        {
            char c = document[i];
            if (char.IsDigit(c))
            {
                // Двенадцатая цифра подряд: это не номер, а число, в котором номер померещился.
                if (count == DigitCount)
                {
                    return false;
                }

                digits[count++] = c;
                last = i;
                continue;
            }

            if (!IsSeparator(c) || count == 0 || count == DigitCount)
            {
                break;
            }
        }

        if (count != DigitCount || digits[0] != (plus ? '7' : '8'))
        {
            return false;
        }

        char space = state.NoWrap ? ' ' : Chars.Nbsp;
        if (plus)
        {
            buffer.Write('+');
        }

        buffer.Write(digits[0]);
        buffer.Write(space);
        buffer.Write(digits.Slice(1, 3));
        buffer.Write(space);
        buffer.Write(digits.Slice(4, 3));
        buffer.Write('-');
        buffer.Write(digits.Slice(7, 2));
        buffer.Write('-');
        buffer.Write(digits.Slice(9, 2));

        state.Skip = last - index;
        return true;
    }
}
