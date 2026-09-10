namespace Typographer.Internal;

/// <summary>Словари, по которым фаза Bind принимает решения.</summary>
internal static class Dictionaries
{
    /// <summary>
    /// Короткое слово — предлог, союз или частица длиной до трёх букв,
    /// после которого перенос строки нежелателен. ГОСТ Р 7.0.110-2025, 9.4.
    /// </summary>
    /// <remarks>
    /// Проверяются именно БУКВЫ, а не длина: токен фазы Bind включает точки и дефисы, и без
    /// этой проверки «А-» в «А- б» считалось бы коротким словом и получало неразрывный пробел.
    /// </remarks>
    public static bool IsShortWord(ReadOnlySpan<char> word)
    {
        if (word.Length is 0 or > 3)
        {
            return false;
        }

        foreach (char c in word)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Проверяет, что слово может быть частью устойчивого сокращения:
    /// «т. д.», «т. п.», «т. е.», «н. э.», «г.», «в.».
    /// </summary>
    public static bool IsAbbreviationPart(ReadOnlySpan<char> word)
        => word.Length == 1 && word[0] is 'т' or 'д' or 'п' or 'е' or 'н' or 'э' or 'г' or 'в';

    /// <summary>
    /// Месяцы в именительном и родительном падеже: «январь» для интервала месяцев,
    /// «января» для даты вида «5 января».
    /// </summary>
    private static readonly string[] MonthNames =
    [
        "январь", "января", "февраль", "февраля", "март", "марта", "апрель", "апреля",
        "май", "мая", "июнь", "июня", "июль", "июля", "август", "августа",
        "сентябрь", "сентября", "октябрь", "октября", "ноябрь", "ноября", "декабрь", "декабря",
    ];

    /// <summary>Дни недели в именительном падеже.</summary>
    private static readonly string[] WeekdayNames =
    [
        "понедельник", "вторник", "среда", "четверг", "пятница", "суббота", "воскресенье",
    ];

    /// <summary>Слово — название месяца.</summary>
    public static bool IsMonth(ReadOnlySpan<char> word) => Contains(MonthNames, word);

    /// <summary>Слово — название дня недели.</summary>
    public static bool IsWeekday(ReadOnlySpan<char> word) => Contains(WeekdayNames, word);

    /// <summary>
    /// Поиск по короткому списку сравнением посимвольно. Словари здесь на два десятка слов,
    /// и хеш-множество их не ускорит заметно, зато потребует аллокации ключа из спана на
    /// netstandard2.0 — а путь записи в приёмник обязан не аллоцировать.
    /// </summary>
    private static bool Contains(string[] words, ReadOnlySpan<char> word)
    {
        foreach (string candidate in words)
        {
            if (word.Length != candidate.Length)
            {
                continue;
            }

            bool same = true;
            for (int i = 0; i < candidate.Length; i++)
            {
                if (char.ToLowerInvariant(word[i]) != candidate[i])
                {
                    same = false;
                    break;
                }
            }

            if (same)
            {
                return true;
            }
        }

        return false;
    }
}
