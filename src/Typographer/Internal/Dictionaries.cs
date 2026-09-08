namespace Typographer.Internal;

/// <summary>Словари, по которым фаза Bind принимает решения.</summary>
internal static class Dictionaries
{
    /// <summary>
    /// Короткое слово — предлог, союз или частица длиной до трёх букв,
    /// после которого перенос строки нежелателен. ГОСТ Р 7.0.110-2025, 9.4.
    /// </summary>
    public static bool IsShortWord(ReadOnlySpan<char> word) => word.Length is > 0 and <= 3;

    /// <summary>
    /// Проверяет, что слово может быть частью устойчивого сокращения:
    /// «т. д.», «т. п.», «т. е.», «н. э.», «г.», «в.».
    /// </summary>
    public static bool IsAbbreviationPart(ReadOnlySpan<char> word)
        => word.Length == 1 && word[0] is 'т' or 'д' or 'п' or 'е' or 'н' or 'э' or 'г' or 'в';
}
