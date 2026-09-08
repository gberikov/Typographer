namespace Typographer.Internal;

/// <summary>Фаза Emit: кодирование типографских символов по выбранному режиму.</summary>
internal static class Emitter
{
    public static void Encode(ReadOnlySpan<char> source, EntityMode mode, ref CharBuffer destination)
    {
        if (mode == EntityMode.Symbols)
        {
            destination.Write(source);
            return;
        }

        foreach (char c in source)
        {
            string? name = EntityTable.NameOf(c);
            bool encode = name is not null
                && (mode != EntityMode.Mixed || EntityTable.IsInvisible(c));

            if (!encode)
            {
                destination.Write(c);
                continue;
            }

            destination.Write('&');
            if (mode == EntityMode.Numeric)
            {
                destination.Write('#');
                WriteDecimal(ref destination, c);
            }
            else
            {
                destination.Write(name.AsSpan());
            }

            destination.Write(';');
        }
    }

    /// <summary>
    /// Пишет код символа десятичными цифрами прямо в буфер. Промежуточной строки нет:
    /// путь записи в приёмник обязан не аллоцировать ни в одном режиме кодирования.
    /// Код символа не превышает пяти десятичных цифр (65535).
    /// </summary>
    private static void WriteDecimal(ref CharBuffer destination, int value)
    {
        Span<char> digits = stackalloc char[5];
        int position = digits.Length;
        do
        {
            digits[--position] = (char)('0' + (value % 10));
            value /= 10;
        }
        while (value > 0);

        destination.Write(digits.Slice(position));
    }
}
