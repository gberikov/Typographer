namespace Typographer.Internal;

/// <summary>Фаза Emit: кодирование типографских символов по выбранному режиму.</summary>
internal static class Emitter
{
    /// <summary>
    /// Фаза Emit по документу: кодируются только текстовые узлы.
    /// </summary>
    /// <remarks>
    /// Разметка не кодируется ни в одном режиме. Кавычка в значении атрибута и неразрывный
    /// пробел внутри &lt;code&gt; — часть разметки и защищённой зоны, а гарантия 3 обещает их
    /// байт в байт.
    /// </remarks>
    /// <param name="html">Документ после фазы Layout.</param>
    /// <param name="mode">Режим вывода сущностей.</param>
    /// <param name="destination">Приёмник.</param>
    public static void EncodeDocument(ReadOnlySpan<char> html, EntityMode mode, ref CharBuffer destination)
    {
        if (mode == EntityMode.Symbols)
        {
            destination.Write(html);
            return;
        }

        var scanner = new MarkupScanner(html);
        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = html.Slice(segment.Start, segment.Length);
            if (segment.Kind == SegmentKind.Text)
            {
                Encode(slice, mode, ref destination);
            }
            else
            {
                destination.Write(slice);
            }
        }
    }

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
            bool encode = EntityTable.IsEncodable(c)
                && (mode != EntityMode.Mixed || EntityTable.IsInvisible(c));

            if (!encode)
            {
                destination.Write(c);
                continue;
            }

            destination.Write('&');
            if (mode == EntityMode.Numeric || name is null)
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
