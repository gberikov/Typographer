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

    /// <summary>Частицы, которые нельзя отрывать от предшествующего слова. ГОСТ 9.4.</summary>
    private static readonly string[] Particles = ["ли", "ль", "же", "ж", "бы", "б"];

    private static readonly int ParticleLengths = LengthMask(Particles);

    /// <summary>Слово — частица, которую нельзя отрывать от предыдущего слова.</summary>
    public static bool IsParticle(ReadOnlySpan<char> word) => Contains(Particles, ParticleLengths, word);

    /// <summary>
    /// Предлоги и союзы длиной от четырёх букв, которые нельзя оставлять в конце строки.
    /// Слова до трёх букв покрыты правилом короткого слова, и дублировать их здесь не нужно.
    /// </summary>
    private static readonly string[] FunctionWords =
    [
        "близ", "вместо", "вопреки", "перед", "после", "около", "среди", "сквозь", "через",
        "между", "кроме", "чтобы", "когда", "хотя", "если", "либо", "итак", "зато", "даже",
        "лишь", "пусть", "будто",
    ];

    private static readonly int FunctionWordLengths = LengthMask(FunctionWords);

    /// <summary>Слово — предлог или союз из закрытого списка.</summary>
    public static bool IsFunctionWord(ReadOnlySpan<char> word) => Contains(FunctionWords, FunctionWordLengths, word);

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

    private static readonly int MonthLengths = LengthMask(MonthNames);

    /// <summary>Дни недели в именительном падеже.</summary>
    private static readonly string[] WeekdayNames =
    [
        "понедельник", "вторник", "среда", "четверг", "пятница", "суббота", "воскресенье",
    ];

    private static readonly int WeekdayLengths = LengthMask(WeekdayNames);

    /// <summary>Сокращение года: «г.», «гг.» и те же без точки.</summary>
    private static readonly string[] YearAbbreviations = ["г.", "гг.", "г", "гг"];

    private static readonly int YearLengths = LengthMask(YearAbbreviations);

    /// <summary>Слово — сокращение года.</summary>
    public static bool IsYearAbbreviation(ReadOnlySpan<char> word) => Contains(YearAbbreviations, YearLengths, word);

    /// <summary>Разряды числа словом: «тыс.», «млн», «млрд», «трлн» — с точкой и без.</summary>
    private static readonly string[] Magnitudes =
    [
        "тыс.", "тыс", "млн", "млн.", "млрд", "млрд.", "трлн", "трлн.",
    ];

    private static readonly int MagnitudeLengths = LengthMask(Magnitudes);

    /// <summary>Слово — название разряда числа.</summary>
    public static bool IsMagnitude(ReadOnlySpan<char> word) => Contains(Magnitudes, MagnitudeLengths, word);

    /// <summary>Денежные сокращения: рубли, копейки, доллары — с точкой и без.</summary>
    private static readonly string[] MoneyAbbreviations =
    [
        "руб.", "руб", "коп.", "коп", "р.", "к.", "долл.", "долл",
    ];

    private static readonly int MoneyLengths = LengthMask(MoneyAbbreviations);

    /// <summary>Слово — денежное сокращение.</summary>
    public static bool IsMoneyAbbreviation(ReadOnlySpan<char> word) => Contains(MoneyAbbreviations, MoneyLengths, word);

    /// <summary>Единицы разрешения печати и экрана.</summary>
    private static readonly string[] Resolutions = ["dpi", "lpi", "ppi"];

    private static readonly int ResolutionLengths = LengthMask(Resolutions);

    /// <summary>Слово — единица разрешения.</summary>
    public static bool IsResolution(ReadOnlySpan<char> word) => Contains(Resolutions, ResolutionLengths, word);

    /// <summary>
    /// Адресные сокращения. Точка входит в образец: она и есть признак сокращения,
    /// без неё «с» — предлог, а «д» — буква.
    /// </summary>
    private static readonly string[] AddressAbbreviations =
    [
        "г.", "обл.", "р-н", "ул.", "пр.", "пр-т", "пер.", "пл.", "наб.", "бул.", "ш.",
        "д.", "корп.", "стр.", "кв.", "оф.", "под.", "эт.", "пос.", "с.", "дер.", "ст.", "мкр.",
    ];

    private static readonly int AddressLengths = LengthMask(AddressAbbreviations);

    /// <summary>Слово — адресное сокращение.</summary>
    public static bool IsAddressAbbreviation(ReadOnlySpan<char> word) => Contains(AddressAbbreviations, AddressLengths, word);

    /// <summary>Сокращения ссылок на части текста.</summary>
    private static readonly string[] PageAbbreviations =
    [
        "стр.", "с.", "гл.", "рис.", "илл.", "табл.", "п.", "пп.", "ч.", "т.",
    ];

    private static readonly int PageLengths = LengthMask(PageAbbreviations);

    /// <summary>Слово — сокращение ссылки на часть текста.</summary>
    public static bool IsPageAbbreviation(ReadOnlySpan<char> word) => Contains(PageAbbreviations, PageLengths, word);

    /// <summary>Отсылочные сокращения.</summary>
    private static readonly string[] ReferenceAbbreviations = ["см.", "им.", "ср.", "напр."];

    private static readonly int ReferenceLengths = LengthMask(ReferenceAbbreviations);

    /// <summary>Слово — отсылочное сокращение.</summary>
    public static bool IsReferenceAbbreviation(ReadOnlySpan<char> word)
        => Contains(ReferenceAbbreviations, ReferenceLengths, word);

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
    public static bool IsMonth(ReadOnlySpan<char> word) => Contains(MonthNames, MonthLengths, word);

    /// <summary>Слово — название дня недели.</summary>
    public static bool IsWeekday(ReadOnlySpan<char> word) => Contains(WeekdayNames, WeekdayLengths, word);

    /// <summary>
    /// Маска длин слов словаря: бит N поднят, если в словаре есть слово длиной N.
    /// Токен, длина которого в маску не попала, отсекается одним тестом — вместо
    /// перебора всего списка. Слов длиннее тридцати одного символа в словарях нет.
    /// </summary>
    private static int LengthMask(string[] words)
    {
        int mask = 0;
        foreach (string word in words)
        {
            mask |= 1 << word.Length;
        }

        return mask;
    }

    /// <summary>
    /// Поиск по короткому списку сравнением посимвольно. Словари здесь на два десятка слов,
    /// и хеш-множество их не ускорит заметно, зато потребует аллокации ключа из спана на
    /// netstandard2.0 — а путь записи в приёмник обязан не аллоцировать.
    /// </summary>
    private static bool Contains(string[] words, int lengthMask, ReadOnlySpan<char> word)
    {
        // Отсев по длине до всякого перебора: у обычного слова текста длина не совпадает
        // ни с одним словарным словом, и до сравнения символов дело не доходит вовсе.
        if ((uint)word.Length > 31 || (lengthMask & (1 << word.Length)) == 0)
        {
            return false;
        }

        foreach (string candidate in words)
        {
            if (word.Length != candidate.Length)
            {
                continue;
            }

            bool same = true;
            for (int i = 0; i < candidate.Length; i++)
            {
                // Сравнение сначала как есть: словари записаны строчными, и текст обычно
                // тоже. Приведение регистра — таблица в globalization — стоит дороже
                // сравнения символов, и звать его на КАЖДЫЙ символ каждого кандидата
                // незачем: до него доходит только несовпадение.
                char c = word[i];
                if (c != candidate[i] && char.ToLowerInvariant(c) != candidate[i])
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
