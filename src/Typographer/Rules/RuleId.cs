namespace Typographer.Rules;

/// <summary>Идентификатор правила типографики.</summary>
/// <remarks>Имена совпадают с именами правил JS-typograf, например «ru/nbsp/abbr».</remarks>
public readonly struct RuleId : IEquatable<RuleId>
{
    internal RuleId(int index, string name, RulePhase phase)
    {
        Index = index;
        _name = name;
        Phase = phase;
    }

    private readonly string? _name;

    /// <summary>Порядковый номер правила в реестре. Используется как позиция бита в <see cref="RuleSet"/>.</summary>
    internal int Index { get; }

    /// <summary>
    /// Имя правила вида «ru/dash/main». У <c>default(RuleId)</c> — пустая строка, а не null:
    /// значение по умолчанию не совпадает ни с одним зарегистрированным правилом
    /// (см. <see cref="Index"/>), но безопасно для вывода и сравнения без дополнительной проверки на null.
    /// </summary>
    public string Name => _name ?? string.Empty;

    /// <summary>Фаза конвейера, на которой правило применяется.</summary>
    public RulePhase Phase { get; }

    /// <summary>Разбирает имя правила. Возвращает false, если такого правила нет.</summary>
    public static bool TryParse(string name, out RuleId rule)
    {
        foreach (RuleId candidate in Registry.All)
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                rule = candidate;
                return true;
            }
        }

        rule = default;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(RuleId other) => Index == other.Index;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RuleId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Index;

    /// <inheritdoc />
    public override string ToString() => Name;

    /// <summary>Правила, общие для всех языков.</summary>
    public static class Common
    {
        /// <summary>Пунктуация.</summary>
        public static class Punctuation
        {
            /// <summary>Расстановка кавычек правильного вида.</summary>
            public static RuleId Quote => Registry.Quote;

            /// <summary>Замена трёх точек на многоточие.</summary>
            public static RuleId Hellip => Registry.Hellip;

            /// <summary>Расстановка правильного апострофа.</summary>
            public static RuleId Apostrophe => Registry.Apostrophe;

            /// <summary>Удаление двойной пунктуации: «слово,,ещё» становится «слово,ещё».</summary>
            public static RuleId DelDoublePunctuation => Registry.DelDoublePunctuation;

            /// <summary>
            /// Вынос кавычек за пределы ссылки. НЕ РЕАЛИЗУЕТСЯ: правило перемещает текст через
            /// границу тега, а гарантия 3 спецификации обещает разметку байт в байт. Имя
            /// зарегистрировано ради паритета с JS-typograf, чтобы <see cref="RuleId.TryParse"/>
            /// его узнавал, а справочник правил был полным.
            /// </summary>
            public static RuleId QuoteLink => Registry.QuoteLink;
        }

        /// <summary>Пробелы.</summary>
        public static class Space
        {
            /// <summary>Удаление повторяющихся пробелов.</summary>
            public static RuleId DelRepeatSpace => Registry.DelRepeatSpace;

            /// <summary>Удаление пробелов перед знаками пунктуации.</summary>
            public static RuleId DelBeforePunctuation => Registry.DelBeforePunctuation;

            /// <summary>Пробел после запятой.</summary>
            public static RuleId AfterComma => Registry.AfterComma;

            /// <summary>Пробел после двоеточия.</summary>
            public static RuleId AfterColon => Registry.SpaceAfterColon;

            /// <summary>Пробел после точки с запятой.</summary>
            public static RuleId AfterSemicolon => Registry.SpaceAfterSemicolon;

            /// <summary>Пробел после восклицательного знака.</summary>
            public static RuleId AfterExclamationMark => Registry.SpaceAfterExclamationMark;

            /// <summary>Пробел после вопросительного знака.</summary>
            public static RuleId AfterQuestionMark => Registry.SpaceAfterQuestionMark;

            /// <summary>Удаление пробела перед точкой.</summary>
            public static RuleId DelBeforeDot => Registry.SpaceDelBeforeDot;

            /// <summary>Удаление пробела перед знаком процента, промилле и продецимилле.</summary>
            public static RuleId DelBeforePercent => Registry.SpaceDelBeforePercent;

            /// <summary>Удаление пробелов между восклицательными знаками.</summary>
            public static RuleId DelBetweenExclamationMarks => Registry.SpaceDelBetweenExclamationMarks;

            /// <summary>Пробел перед открывающей скобкой.</summary>
            public static RuleId BeforeBracket => Registry.SpaceBeforeBracket;

            /// <summary>Удаление лишних пробелов внутри круглых скобок.</summary>
            public static RuleId Bracket => Registry.SpaceBracket;

            /// <summary>Удаление лишних пробелов внутри квадратных скобок.</summary>
            public static RuleId SquareBracket => Registry.SpaceSquareBracket;

            /// <summary>Удаление пробелов и переводов строк в начале текста. Вне <see cref="RuleSet.Default"/>.</summary>
            public static RuleId TrimLeft => Registry.SpaceTrimLeft;

            /// <summary>Удаление пробелов и переводов строк в конце текста. Вне <see cref="RuleSet.Default"/>.</summary>
            public static RuleId TrimRight => Registry.SpaceTrimRight;

            /// <summary>Удаление пробелов в начале строки. Вне <see cref="RuleSet.Default"/>.</summary>
            public static RuleId DelLeadingBlanks => Registry.SpaceDelLeadingBlanks;

            /// <summary>Удаление пробелов в конце строки. Вне <see cref="RuleSet.Default"/>.</summary>
            public static RuleId DelTrailingBlanks => Registry.SpaceDelTrailingBlanks;

            /// <summary>Удаление повторяющихся переводов строки. Вне <see cref="RuleSet.Default"/>.</summary>
            public static RuleId DelRepeatN => Registry.SpaceDelRepeatN;

            /// <summary>Замена табуляции на четыре пробела. Вне <see cref="RuleSet.Default"/>.</summary>
            public static RuleId ReplaceTab => Registry.SpaceReplaceTab;

            /// <summary>Перевод строки в конце текста. Вне <see cref="RuleSet.Default"/>.</summary>
            public static RuleId InsertFinalNewline => Registry.SpaceInsertFinalNewline;
        }

        /// <summary>Типографские символы.</summary>
        public static class Symbols
        {
            /// <summary>Стрелки из дефиса и угловой скобки.</summary>
            public static RuleId Arrow => Registry.SymbolArrow;

            /// <summary>Добавление знака градуса к «C» и «F».</summary>
            public static RuleId Cf => Registry.SymbolCf;

            /// <summary>Знаки копирайта, торговой марки и регистрации из скобочной записи.</summary>
            public static RuleId Copy => Registry.SymbolCopy;
        }

        /// <summary>Числа.</summary>
        public static class Number
        {
            /// <summary>Простые дроби одной восьмой доли: половина, четверть и три четверти.</summary>
            public static RuleId Fraction => Registry.NumberFraction;

            /// <summary>Знаки сравнения и «плюс-минус» из двухсимвольной записи.</summary>
            public static RuleId MathSigns => Registry.NumberMathSigns;

            /// <summary>Знак умножения вместо латинской буквы между числами.</summary>
            public static RuleId Times => Registry.NumberTimes;

            /// <summary>Разбиение длинных чисел по разрядам. Вне <see cref="RuleSet.Default"/>.</summary>
            public static RuleId DigitGrouping => Registry.NumberDigitGrouping;
        }

        /// <summary>Неразрывные пробелы, общие для языков.</summary>
        public static class Nbsp
        {
            /// <summary>Неразрывный пробел после короткого слова.</summary>
            public static RuleId AfterShortWord => Registry.AfterShortWord;
        }
    }

    /// <summary>Правила русского языка.</summary>
    public static class Ru
    {
        /// <summary>Тире и дефисы.</summary>
        public static class Dash
        {
            /// <summary>Замена дефиса на тире.</summary>
            public static RuleId Main => Registry.DashMain;

            /// <summary>Тире в прямой речи.</summary>
            public static RuleId DirectSpeech => Registry.DashDirectSpeech;

            /// <summary>Замена дефиса на тире в годах.</summary>
            public static RuleId Years => Registry.DashYears;

            /// <summary>
            /// Дефис перед «то», «либо», «нибудь». Вне <see cref="RuleSet.Default"/>: «то»
            /// омонимично указательному местоимению, и «Я знал, что то было ошибкой»
            /// правило испортило бы, сменив смысл фразы.
            /// </summary>
            public static RuleId To => Registry.DashTo;

            /// <summary>Дефис перед «ка» и «кась».</summary>
            public static RuleId Ka => Registry.DashKa;

            /// <summary>Дефис перед «таки».</summary>
            public static RuleId Taki => Registry.DashTaki;

            /// <summary>Дефис после «кое» и «кой».</summary>
            public static RuleId Koe => Registry.DashKoe;

            /// <summary>Дефис в «из-под».</summary>
            public static RuleId Izpod => Registry.DashIzpod;

            /// <summary>Дефис в «из-за».</summary>
            public static RuleId Izza => Registry.DashIzza;

            /// <summary>
            /// Дефис в «как-то». Вне <see cref="RuleSet.Default"/> по той же причине, что и
            /// <see cref="To"/>: «как то» бывает союзом с местоимением — «как то: раз, два».
            /// </summary>
            public static RuleId KakTo => Registry.DashKakTo;

            /// <summary>
            /// Дефис перед частицей «де». Вне <see cref="RuleSet.Default"/>: частица омонимична
            /// предлогу в иностранных фамилиях («Шарль де Голль»), и без словаря фамилий правило ошибается.
            /// </summary>
            public static RuleId De => Registry.DashDe;

            /// <summary>Тире в диапазоне веков.</summary>
            public static RuleId Centuries => Registry.DashCenturies;

            /// <summary>Тире в десятилетиях.</summary>
            public static RuleId Decade => Registry.DashDecade;

            /// <summary>Тире в интервалах времени.</summary>
            public static RuleId Time => Registry.DashTime;

            /// <summary>Тире между днями одного месяца.</summary>
            public static RuleId DaysMonth => Registry.DashDaysMonth;

            /// <summary>Тире между месяцами.</summary>
            public static RuleId Month => Registry.DashMonth;

            /// <summary>Тире между днями недели.</summary>
            public static RuleId Weekday => Registry.DashWeekday;

            /// <summary>Защита дефиса в двойных фамилиях: «Салтыков-Щедрин» не становится тире.</summary>
            public static RuleId Surname => Registry.DashSurname;
        }

        /// <summary>Неразрывные пробелы русского языка.</summary>
        public static class Nbsp
        {
            /// <summary>Неразрывный пробел в сокращениях, например «т. д.».</summary>
            public static RuleId Abbr => Registry.NbspAbbr;

            /// <summary>Привязка инициалов к фамилии.</summary>
            public static RuleId Initials => Registry.NbspInitials;
        }

        /// <summary>Пунктуация русского языка.</summary>
        public static class Punctuation
        {
            /// <summary>
            /// Расстановка запятых перед «а» и «но». По умолчанию выключено.
            /// Правило зарегистрировано (участвует в <see cref="RuleSet"/>, проходит <see cref="RuleId.TryParse"/>),
            /// но пока НЕ ДЕЙСТВУЕТ: ни одна фаза конвейера его не читает, включение через
            /// <see cref="RuleSet.With(RuleId[])"/> ничего не меняет в выводе. Реализация ждёт следующей версии.
            /// </summary>
            public static RuleId Ano => Registry.Ano;

            /// <summary>Удвоенный восклицательный знак сводится к одному.</summary>
            public static RuleId Exclamation => Registry.PunctuationExclamation;

            /// <summary>Восклицательный знак с вопросительным ставятся в принятом порядке.</summary>
            public static RuleId ExclamationQuestion => Registry.PunctuationExclamationQuestion;

            /// <summary>Многоточие после знака конца предложения сокращается до двух точек.</summary>
            public static RuleId HellipQuestion => Registry.PunctuationHellipQuestion;
        }

        /// <summary>Числа в русском тексте.</summary>
        public static class Number
        {
            /// <summary>Десятичная запятая вместо точки.</summary>
            public static RuleId Comma => Registry.RuNumberComma;

            /// <summary>Краткая форма порядковых числительных.</summary>
            public static RuleId Ordinals => Registry.RuNumberOrdinals;
        }

        /// <summary>Пробелы, специфичные для русского языка.</summary>
        public static class Space
        {
            /// <summary>Пробел после многоточия и его сочетаний со знаком конца предложения.</summary>
            public static RuleId AfterHellip => Registry.RuSpaceAfterHellip;

            /// <summary>Пробел между числом и словом «год».</summary>
            public static RuleId Year => Registry.RuSpaceYear;
        }

        /// <summary>Знаки русского языка.</summary>
        public static class Symbols
        {
            /// <summary>Сдвоенный знак номера сводится к одному.</summary>
            public static RuleId NN => Registry.RuSymbolsNN;
        }

        /// <summary>Исправление опечаток.</summary>
        public static class Typo
        {
            /// <summary>
            /// Замена латинских букв на русские при ошибке раскладки. По умолчанию выключено.
            /// Правило зарегистрировано (участвует в <see cref="RuleSet"/>, проходит <see cref="RuleId.TryParse"/>),
            /// но пока НЕ ДЕЙСТВУЕТ: ни одна фаза конвейера его не читает, включение через
            /// <see cref="RuleSet.With(RuleId[])"/> ничего не меняет в выводе. Реализация ждёт следующей версии.
            /// </summary>
            public static RuleId SwitchingKeyboardLayout => Registry.KeyboardLayout;
        }
    }

    /// <summary>Британский английский. Вне <see cref="RuleSet.Default"/>: язык библиотеки — русский.</summary>
    public static class EnGb
    {
        /// <summary>Тире.</summary>
        public static class Dash
        {
            /// <summary>Дефис с пробелами становится коротким тире с отбивкой.</summary>
            public static RuleId Main => Registry.EnGbDashMain;
        }
    }

    /// <summary>Американский английский. Вне <see cref="RuleSet.Default"/>: язык библиотеки — русский.</summary>
    public static class EnUs
    {
        /// <summary>Тире.</summary>
        public static class Dash
        {
            /// <summary>Дефис с пробелами становится длинным тире без отбивки.</summary>
            public static RuleId Main => Registry.EnUsDashMain;
        }
    }

    internal static class Registry
    {
        // Индексы начинаются с единицы: ноль остаётся незанятым, чтобы default(RuleId)
        // (Index == 0) не совпадал ни с одним настоящим правилом.
        public static readonly RuleId Quote = new(1, "common/punctuation/quote", RulePhase.Scan);
        public static readonly RuleId Hellip = new(2, "common/punctuation/hellip", RulePhase.Scan);
        public static readonly RuleId Apostrophe = new(3, "common/punctuation/apostrophe", RulePhase.Scan);
        public static readonly RuleId DelRepeatSpace = new(4, "common/space/delRepeatSpace", RulePhase.Scan);
        public static readonly RuleId DelBeforePunctuation = new(5, "common/space/delBeforePunctuation", RulePhase.Scan);
        public static readonly RuleId AfterComma = new(6, "common/space/afterComma", RulePhase.Scan);
        public static readonly RuleId AfterShortWord = new(7, "common/nbsp/afterShortWord", RulePhase.Bind);
        public static readonly RuleId DashMain = new(8, "ru/dash/main", RulePhase.Scan);
        public static readonly RuleId DashDirectSpeech = new(9, "ru/dash/directSpeech", RulePhase.Scan);
        public static readonly RuleId DashYears = new(10, "ru/dash/years", RulePhase.Scan);
        public static readonly RuleId NbspAbbr = new(11, "ru/nbsp/abbr", RulePhase.Bind);
        public static readonly RuleId NbspInitials = new(12, "ru/nbsp/initials", RulePhase.Bind);
        public static readonly RuleId Ano = new(13, "ru/punctuation/ano", RulePhase.Scan);
        public static readonly RuleId KeyboardLayout = new(14, "ru/typo/switchingKeyboardLayout", RulePhase.Bind);
        public static readonly RuleId SpaceAfterColon = new(15, "common/space/afterColon", RulePhase.Scan);
        public static readonly RuleId SpaceAfterSemicolon = new(16, "common/space/afterSemicolon", RulePhase.Scan);
        public static readonly RuleId SpaceAfterExclamationMark = new(17, "common/space/afterExclamationMark", RulePhase.Scan);
        public static readonly RuleId SpaceAfterQuestionMark = new(18, "common/space/afterQuestionMark", RulePhase.Scan);
        public static readonly RuleId SpaceDelBeforeDot = new(19, "common/space/delBeforeDot", RulePhase.Scan);
        public static readonly RuleId SpaceDelBeforePercent = new(20, "common/space/delBeforePercent", RulePhase.Scan);
        public static readonly RuleId SpaceDelBetweenExclamationMarks = new(21, "common/space/delBetweenExclamationMarks", RulePhase.Scan);
        public static readonly RuleId SpaceBeforeBracket = new(22, "common/space/beforeBracket", RulePhase.Scan);
        public static readonly RuleId SpaceBracket = new(23, "common/space/bracket", RulePhase.Scan);
        public static readonly RuleId SpaceSquareBracket = new(24, "common/space/squareBracket", RulePhase.Scan);
        public static readonly RuleId SpaceTrimLeft = new(25, "common/space/trimLeft", RulePhase.Scan);
        public static readonly RuleId SpaceTrimRight = new(26, "common/space/trimRight", RulePhase.Scan);
        public static readonly RuleId SpaceDelLeadingBlanks = new(27, "common/space/delLeadingBlanks", RulePhase.Scan);
        public static readonly RuleId SpaceDelTrailingBlanks = new(28, "common/space/delTrailingBlanks", RulePhase.Scan);
        public static readonly RuleId SpaceDelRepeatN = new(29, "common/space/delRepeatN", RulePhase.Scan);
        public static readonly RuleId SpaceReplaceTab = new(30, "common/space/replaceTab", RulePhase.Scan);
        public static readonly RuleId SpaceInsertFinalNewline = new(31, "common/space/insertFinalNewline", RulePhase.Scan);
        public static readonly RuleId DelDoublePunctuation = new(32, "common/punctuation/delDoublePunctuation", RulePhase.Scan);
        public static readonly RuleId QuoteLink = new(33, "common/punctuation/quoteLink", RulePhase.Scan);
        public static readonly RuleId SymbolArrow = new(34, "common/symbols/arrow", RulePhase.Scan);
        public static readonly RuleId SymbolCf = new(35, "common/symbols/cf", RulePhase.Scan);
        public static readonly RuleId SymbolCopy = new(36, "common/symbols/copy", RulePhase.Scan);
        public static readonly RuleId NumberFraction = new(37, "common/number/fraction", RulePhase.Scan);
        public static readonly RuleId NumberMathSigns = new(38, "common/number/mathSigns", RulePhase.Scan);
        public static readonly RuleId NumberTimes = new(39, "common/number/times", RulePhase.Scan);
        public static readonly RuleId NumberDigitGrouping = new(40, "common/number/digitGrouping", RulePhase.Scan);
        public static readonly RuleId DashTo = new(41, "ru/dash/to", RulePhase.Scan);
        public static readonly RuleId DashKa = new(42, "ru/dash/ka", RulePhase.Scan);
        public static readonly RuleId DashTaki = new(43, "ru/dash/taki", RulePhase.Scan);
        public static readonly RuleId DashKoe = new(44, "ru/dash/koe", RulePhase.Scan);
        public static readonly RuleId DashIzpod = new(45, "ru/dash/izpod", RulePhase.Scan);
        public static readonly RuleId DashIzza = new(46, "ru/dash/izza", RulePhase.Scan);
        public static readonly RuleId DashKakTo = new(47, "ru/dash/kakto", RulePhase.Scan);
        public static readonly RuleId DashDe = new(48, "ru/dash/de", RulePhase.Scan);
        public static readonly RuleId DashCenturies = new(49, "ru/dash/centuries", RulePhase.Scan);
        public static readonly RuleId DashDecade = new(50, "ru/dash/decade", RulePhase.Scan);
        public static readonly RuleId DashTime = new(51, "ru/dash/time", RulePhase.Scan);
        public static readonly RuleId DashDaysMonth = new(52, "ru/dash/daysMonth", RulePhase.Scan);
        public static readonly RuleId DashMonth = new(53, "ru/dash/month", RulePhase.Scan);
        public static readonly RuleId DashWeekday = new(54, "ru/dash/weekday", RulePhase.Scan);
        public static readonly RuleId DashSurname = new(55, "ru/dash/surname", RulePhase.Scan);
        public static readonly RuleId PunctuationExclamation = new(56, "ru/punctuation/exclamation", RulePhase.Scan);
        public static readonly RuleId PunctuationExclamationQuestion = new(57, "ru/punctuation/exclamationQuestion", RulePhase.Scan);
        public static readonly RuleId PunctuationHellipQuestion = new(58, "ru/punctuation/hellipQuestion", RulePhase.Scan);
        public static readonly RuleId RuNumberComma = new(59, "ru/number/comma", RulePhase.Scan);
        public static readonly RuleId RuNumberOrdinals = new(60, "ru/number/ordinals", RulePhase.Scan);
        public static readonly RuleId RuSpaceAfterHellip = new(61, "ru/space/afterHellip", RulePhase.Scan);
        public static readonly RuleId RuSpaceYear = new(62, "ru/space/year", RulePhase.Scan);
        public static readonly RuleId RuSymbolsNN = new(63, "ru/symbols/NN", RulePhase.Scan);
        public static readonly RuleId EnGbDashMain = new(64, "en-GB/dash/main", RulePhase.Scan);
        public static readonly RuleId EnUsDashMain = new(65, "en-US/dash/main", RulePhase.Scan);

        public static readonly RuleId[] All =
        [
            Quote, Hellip, Apostrophe, DelRepeatSpace, DelBeforePunctuation,
            AfterComma, AfterShortWord, DashMain, DashDirectSpeech, DashYears,
            NbspAbbr, NbspInitials, Ano, KeyboardLayout, SpaceAfterColon,
            SpaceAfterSemicolon, SpaceAfterExclamationMark, SpaceAfterQuestionMark, SpaceDelBeforeDot, SpaceDelBeforePercent,
            SpaceDelBetweenExclamationMarks, SpaceBeforeBracket, SpaceBracket, SpaceSquareBracket, SpaceTrimLeft,
            SpaceTrimRight, SpaceDelLeadingBlanks, SpaceDelTrailingBlanks, SpaceDelRepeatN, SpaceReplaceTab,
            SpaceInsertFinalNewline, DelDoublePunctuation, QuoteLink, SymbolArrow, SymbolCf,
            SymbolCopy, NumberFraction, NumberMathSigns, NumberTimes, NumberDigitGrouping,
            DashTo, DashKa, DashTaki, DashKoe, DashIzpod,
            DashIzza, DashKakTo, DashDe, DashCenturies, DashDecade,
            DashTime, DashDaysMonth, DashMonth, DashWeekday, DashSurname,
            PunctuationExclamation, PunctuationExclamationQuestion, PunctuationHellipQuestion, RuNumberComma, RuNumberOrdinals,
            RuSpaceAfterHellip, RuSpaceYear, RuSymbolsNN, EnGbDashMain, EnUsDashMain,
        ];

        /// <summary>Правила, выключенные в пресете Default: меняют смысл текста.</summary>
        public static readonly RuleId[] Unsafe = [Ano, KeyboardLayout];

        /// <summary>
        /// Правила нормализации текста: обрезка краёв, отступы, табы, финальный перевод строки.
        /// Выключены в Default — они меняют текст за пределами оформления, и тот, кто вызвал
        /// Typograf.Html(text), такого не ожидает.
        /// </summary>
        public static readonly RuleId[] Normalization =
        [
            SpaceTrimLeft, SpaceTrimRight, SpaceDelLeadingBlanks, SpaceDelTrailingBlanks,
            SpaceDelRepeatN, SpaceReplaceTab, SpaceInsertFinalNewline,
        ];

        /// <summary>
        /// Правила, доступные по требованию, но выключенные в Default по своим причинам:
        /// разбиение разрядов меняет запись числа; quoteLink не реализуется вовсе (нарушает
        /// гарантию 3); частица «де» ошибается на иностранных фамилиях; английское тире
        /// относится к другому языку.
        /// </summary>
        public static readonly RuleId[] OptIn =
        [
            NumberDigitGrouping, QuoteLink, DashTo, DashKakTo, DashDe, EnGbDashMain, EnUsDashMain,
        ];
    }
}
