namespace Typographer.Internal;

/// <summary>Символы, которыми оперирует типограф.</summary>
internal static class Chars
{
    public const char Bom = '\uFEFF';
    public const char Nbsp = '\u00A0';
    public const char NarrowNbsp = '\u202F';
    public const char ThinSpace = '\u2009';
    public const char Laquo = '\u00AB';
    public const char Raquo = '\u00BB';
    public const char Bdquo = '\u201E';
    public const char Ldquo = '\u201C';

    /// <summary>Английская закрывающая кавычка: типограф её не ставит, но во входе она встречается.</summary>
    public const char Rdquo = '\u201D';
    public const char Lsquo = '\u2018';
    public const char Rsquo = '\u2019';
    public const char MDash = '\u2014';
    public const char NDash = '\u2013';
    public const char Hellip = '\u2026';

    /// <summary>Комбинирующий акут: ставится ПОСЛЕ ударной гласной.</summary>
    public const char Acute = '\u0301';
    public const char Numero = '\u2116';
    public const char Section = '\u00A7';
    public const char Pilcrow = '\u00B6';
    public const char Minus = '\u2212';
    public const char Times = '\u00D7';
    public const char Degree = '\u00B0';
    public const char Copyright = '\u00A9';
    public const char Registered = '\u00AE';
    public const char Trademark = '\u2122';
    public const char ArrowRight = '\u2192';
    public const char ArrowLeft = '\u2190';
    public const char NotEqual = '\u2260';
    public const char LessOrEqual = '\u2264';
    public const char GreaterOrEqual = '\u2265';
    public const char ApproxEqual = '\u2245';
    public const char PlusMinus = '\u00B1';
    public const char Half = '\u00BD';
    public const char Quarter = '\u00BC';
    public const char ThreeQuarters = '\u00BE';
}
