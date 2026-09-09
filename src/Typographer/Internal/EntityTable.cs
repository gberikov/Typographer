namespace Typographer.Internal;

/// <summary>Декодирование и кодирование типографских HTML-сущностей.</summary>
/// <remarks>
/// Сущности разметки (&amp;amp;, &amp;lt;, &amp;gt;, &amp;quot;) сюда не входят:
/// они несут смысл разметки и проходят насквозь.
/// </remarks>
internal static class EntityTable
{
    private static readonly (string Name, char Value)[] Entries =
    [
        ("nbsp", Chars.Nbsp),
        ("thinsp", Chars.ThinSpace),
        ("laquo", Chars.Laquo),
        ("raquo", Chars.Raquo),
        ("bdquo", Chars.Bdquo),
        ("ldquo", Chars.Ldquo),
        ("lsquo", Chars.Lsquo),
        ("rsquo", Chars.Rsquo),
        ("mdash", Chars.MDash),
        ("ndash", Chars.NDash),
        ("hellip", Chars.Hellip),
        ("numero", Chars.Numero),
        ("times", Chars.Times),
        ("deg", Chars.Degree),
        ("copy", '©'),
        ("reg", '®'),
        ("trade", '™'),
        ("sect", '§'),
        ("para", '¶'),
        ("plusmn", '±'),
        ("ne", '≠'),
        ("le", '≤'),
        ("ge", '≥'),
        ("larr", '←'),
        ("rarr", '→'),
        ("sup2", '²'),
        ("sup3", '³'),
        ("frac12", '½'),
        ("frac14", '¼'),
        ("frac34", '¾'),
    ];

    public static bool TryDecode(ReadOnlySpan<char> source, out char value, out int consumed)
    {
        value = default;
        consumed = 0;

        if (source.Length < 4 || source[0] != '&')
        {
            return false;
        }

        int end = source.Slice(0, Math.Min(source.Length, 12)).IndexOf(';');
        if (end < 0)
        {
            return false;
        }

        ReadOnlySpan<char> body = source.Slice(1, end - 1);

        if (body.Length > 1 && body[0] == '#')
        {
            ReadOnlySpan<char> digits = body.Slice(1);
            bool hex = digits[0] is 'x' or 'X';
            if (hex)
            {
                digits = digits.Slice(1);
            }

            if (digits.IsEmpty || !TryParseCode(digits, hex, out int code) || code is < 0 or > 0xFFFF)
            {
                return false;
            }

            char decoded = (char)code;
            if (NameOf(decoded) is null)
            {
                return false;
            }

            value = decoded;
            consumed = end + 1;
            return true;
        }

        foreach ((string name, char entityValue) in Entries)
        {
            if (body.SequenceEqual(name.AsSpan()))
            {
                value = entityValue;
                consumed = end + 1;
                return true;
            }
        }

        return false;
    }

    public static string? NameOf(char value)
    {
        foreach ((string name, char entityValue) in Entries)
        {
            if (entityValue == value)
            {
                return name;
            }
        }

        return null;
    }

    public static bool IsInvisible(char value)
        => value is Chars.Nbsp or Chars.NarrowNbsp or Chars.ThinSpace;

    private static bool TryParseCode(ReadOnlySpan<char> digits, bool hex, out int code)
    {
        code = 0;
        int radix = hex ? 16 : 10;
        foreach (char c in digits)
        {
            int digit = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' when hex => c - 'a' + 10,
                >= 'A' and <= 'F' when hex => c - 'A' + 10,
                _ => -1,
            };

            if (digit < 0 || code > (int.MaxValue - digit) / radix)
            {
                code = 0;
                return false;
            }

            code = (code * radix) + digit;
        }

        return true;
    }
}
