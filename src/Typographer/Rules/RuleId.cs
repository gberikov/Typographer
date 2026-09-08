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
            /// <summary>Расстановка запятых перед «а» и «но». По умолчанию выключено.</summary>
            public static RuleId Ano => Registry.Ano;
        }

        /// <summary>Исправление опечаток.</summary>
        public static class Typo
        {
            /// <summary>Замена латинских букв на русские при ошибке раскладки. По умолчанию выключено.</summary>
            public static RuleId SwitchingKeyboardLayout => Registry.KeyboardLayout;
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

        public static readonly RuleId[] All =
        [
            Quote, Hellip, Apostrophe, DelRepeatSpace, DelBeforePunctuation, AfterComma,
            AfterShortWord, DashMain, DashDirectSpeech, DashYears, NbspAbbr, NbspInitials,
            Ano, KeyboardLayout,
        ];

        /// <summary>Правила, выключенные в пресете Default: меняют смысл текста.</summary>
        public static readonly RuleId[] Unsafe = [Ano, KeyboardLayout];
    }
}
