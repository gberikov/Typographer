namespace Typographer.Internal;

/// <summary>Границы веб-адреса, одни на три фазы: Scan, Bind и автоссылку фазы Layout.</summary>
/// <remarks>
/// Адрес — машинный идентификатор, а не текст: любая правка меняет, куда он ведёт. Пока
/// каждая фаза держала свою копию границ, они разъезжались: фаза Scan завершала адрес на
/// закрывающей кавычке, а автоссылка уносила эту кавычку в href.
/// </remarks>
internal static class Url
{
    /// <summary>На позиции <paramref name="index"/> начинается «://».</summary>
    public static bool Starts(ReadOnlySpan<char> source, int index)
        => source[index] == ':' && index + 2 < source.Length
            && source[index + 1] == '/' && source[index + 2] == '/';

    /// <summary>
    /// Символ на позиции <paramref name="index"/> адрес завершает: пробельный, угловая
    /// скобка, прямая или закрывающая типографская кавычка — «»», «“» и английская «”».
    /// Правая одинарная кавычка между буквами — апостроф, часть адреса. Прямой апостроф
    /// адрес не завершает вовсе: он законен внутри адреса (RFC 3986), и «?q='x y'» рвать
    /// нельзя; с хвоста его отрезает автоссылка.
    /// </summary>
    /// <param name="source">Разбираемый текст.</param>
    /// <param name="index">Позиция символа.</param>
    /// <param name="previous">Символ слева; фаза Scan читает его из буфера, а не из текста.</param>
    public static bool Ends(ReadOnlySpan<char> source, int index, char previous)
    {
        char c = source[index];
        return char.IsWhiteSpace(c)
            || c is '<' or '>' or '"' or Chars.Raquo or Chars.Ldquo or Chars.Rdquo
            || (c == Chars.Rsquo
                && !(char.IsLetter(previous)
                     && index + 1 < source.Length && char.IsLetter(source[index + 1])));
    }
}
