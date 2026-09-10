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

    /// <summary>Сокращение года: «г.», «гг.» и те же без точки.</summary>
    private static readonly string[] YearAbbreviations = ["г.", "гг.", "г", "гг"];

    /// <summary>Слово — сокращение года.</summary>
    public static bool IsYearAbbreviation(ReadOnlySpan<char> word) => Contains(YearAbbreviations, word);

    /// <summary>Разряды числа словом: «тыс.», «млн», «млрд», «трлн» — с точкой и без.</summary>
    private static readonly string[] Magnitudes =
    [
        "тыс.", "тыс", "млн", "млн.", "млрд", "млрд.", "трлн", "трлн.",
    ];

    /// <summary>Слово — название разряда числа.</summary>
    public static bool IsMagnitude(ReadOnlySpan<char> word) => Contains(Magnitudes, word);

    /// <summary>Денежные сокращения: рубли, копейки, доллары — с точкой и без.</summary>
    private static readonly string[] MoneyAbbreviations =
    [
        "руб.", "руб", "коп.", "коп", "р.", "к.", "долл.", "долл",
    ];

    /// <summary>Слово — денежное сокращение.</summary>
    public static bool IsMoneyAbbreviation(ReadOnlySpan<char> word) => Contains(MoneyAbbreviations, word);

    /// <summary>Единицы разрешения печати и экрана.</summary>
    private static readonly string[] Resolutions = ["dpi", "lpi", "ppi"];

    /// <summary>Слово — единица разрешения.</summary>
    public static bool IsResolution(ReadOnlySpan<char> word) => Contains(Resolutions, word);

    /// <summary>
    /// Адресные сокращения. Точка входит в образец: она и есть признак сокращения,
    /// без неё «с» — предлог, а «д» — буква.
    /// </summary>
    private static readonly string[] AddressAbbreviations =
    [
        "г.", "обл.", "р-н", "ул.", "пр.", "пр-т", "пер.", "пл.", "наб.", "бул.", "ш.",
        "д.", "корп.", "стр.", "кв.", "оф.", "под.", "эт.", "пос.", "с.", "дер.", "ст.", "мкр.",
    ];

    /// <summary>Слово — адресное сокращение.</summary>
    public static bool IsAddressAbbreviation(ReadOnlySpan<char> word) => Contains(AddressAbbreviations, word);

    /// <summary>Сокращения ссылок на части текста.</summary>
    private static readonly string[] PageAbbreviations =
    [
        "стр.", "с.", "гл.", "рис.", "илл.", "табл.", "п.", "пп.", "ч.", "т.",
    ];

    /// <summary>Слово — сокращение ссылки на часть текста.</summary>
    public static bool IsPageAbbreviation(ReadOnlySpan<char> word) => Contains(PageAbbreviations, word);

    /// <summary>Отсылочные сокращения.</summary>
    private static readonly string[] ReferenceAbbreviations = ["см.", "им.", "ср.", "напр."];

    /// <summary>Слово — отсылочное сокращение.</summary>
    public static bool IsReferenceAbbreviation(ReadOnlySpan<char> word)
        => Contains(ReferenceAbbreviations, word);

    /// <summary>Формы собственности и организационные сокращения. Регистр значим.</summary>
    private static readonly string[] Organizations =
    [
        "ООО", "ОАО", "ЗАО", "ПАО", "АО", "НИИ", "ПБОЮЛ", "ИП", "НПО", "КБ",
    ];

    /// <summary>
    /// Слово — форма собственности. Сравнение с учётом регистра: «ооо» строчными —
    /// не название формы, а звук.
    /// </summary>
    public static bool IsOrganization(ReadOnlySpan<char> word)
    {
        foreach (string candidate in Organizations)
        {
            if (word.Equals(candidate.AsSpan(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

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
