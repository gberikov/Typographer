using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>Состояние фазы Bind, живущее сквозь текстовые сегменты документа.</summary>
internal struct BindState
{
    /// <summary>Сколько символов текущего слова уже собрано.</summary>
    public int WordLength;

    /// <summary>Слово длиннее <c>MaxWord</c>: словарная проверка ему заведомо не нужна.</summary>
    public bool WordOverflow;

    /// <summary>Индекс пробела перед текущим словом в документе, -1 — пробела нет.</summary>
    public int PrevSpaceIndex;

    /// <summary>Создаёт состояние в начале документа: слова нет, пробела слева нет.</summary>
    public BindState()
    {
        WordLength = 0;
        WordOverflow = false;
        PrevSpaceIndex = -1;
    }

    /// <summary>Граница, за которой слово продолжаться не может.</summary>
    public void Reset()
    {
        WordLength = 0;
        WordOverflow = false;
        PrevSpaceIndex = -1;
    }
}

/// <summary>
/// Фаза Bind: словарный проход по словам, расстановка неразрывных пробелов.
/// </summary>
/// <remarks>
/// Фаза ничего не добавляет и не удаляет — она только заменяет отдельные пробелы на
/// неразрывные, поэтому правит буфер фазы Scan НА МЕСТЕ, а не переписывает его в новый.
/// Длина при этом не меняется, и индексы уже пройденных позиций остаются валидными: на этом
/// построена привязка фамилии к идущему следом инициалу.
/// Точек входа две — <see cref="Run"/> для обычного текста и <see cref="RunDocument"/> для
/// HTML, — но разбор у них общий, в <see cref="BindSegment"/>. Отличается только деление на
/// сегменты: в обычном тексте «&lt;» — литерал, и <see cref="MarkupScanner"/> там применять
/// нельзя. Пока разбор жил в двух телах, они разъезжались.
/// </remarks>
internal static class WordBinder
{
    /// <summary>
    /// Максимальная длина слова, которую фаза собирает через разметку. Всё длиннее
    /// заведомо не короткое слово, не сокращение и не инициал — накопление прекращается.
    /// </summary>
    private const int MaxWord = 32;

    /// <summary>Фаза Bind по обычному тексту: весь буфер — один текстовый сегмент.</summary>
    /// <param name="buffer">Буфер фазы Scan: правится на месте.</param>
    /// <param name="rules">Набор включённых правил.</param>
    public static void Run(ref CharBuffer buffer, RuleSet rules)
    {
        if (!Enabled(rules, out bool afterShortWord, out bool abbr, out bool initials))
        {
            return;
        }

        Span<char> word = stackalloc char[MaxWord];
        var state = new BindState();
        ReadOnlySpan<char> source = buffer.AsSpan();

        BindSegment(ref buffer, source, 0, source.Length, word, ref state, afterShortWord, abbr, initials);
        BindTrailingInitial(ref buffer, word, ref state, initials);
    }

    /// <summary>
    /// Фаза Bind по документу: слово, разорванное СТРОЧНЫМ тегом, остаётся одним словом.
    /// </summary>
    /// <remarks>
    /// «&lt;b&gt;сло&lt;/b&gt;во» — одно слово, и словарные правила обязаны видеть его целиком;
    /// иначе «во» выглядит коротким словом и получает неразрывный пробел, которого в тексте нет.
    /// Блочный тег и защищённая зона слово, наоборот, завершают: за ними начинается новая строка
    /// или содержимое, которого фаза не касается.
    /// Буквы слова копятся в стековый буфер, а патчатся позиции ДОКУМЕНТА — фаза не меняет длину,
    /// поэтому индексы, взятые до тега, остаются валидными после него.
    /// </remarks>
    /// <param name="buffer">Буфер фазы Scan: правится на месте.</param>
    /// <param name="rules">Набор включённых правил.</param>
    public static void RunDocument(ref CharBuffer buffer, RuleSet rules)
    {
        if (!Enabled(rules, out bool afterShortWord, out bool abbr, out bool initials))
        {
            return;
        }

        Span<char> word = stackalloc char[MaxWord];
        var state = new BindState();
        ReadOnlySpan<char> document = buffer.AsSpan();
        var scanner = new MarkupScanner(document);

        while (scanner.TryRead(out Segment segment))
        {
            if (segment.Kind == SegmentKind.Markup && !segment.IsBlock)
            {
                // Строчный тег для слова прозрачен: ни буквы, ни границы он не даёт.
                continue;
            }

            if (segment.Kind != SegmentKind.Text)
            {
                state.Reset();
                continue;
            }

            BindSegment(
                ref buffer,
                document,
                segment.Start,
                segment.Start + segment.Length,
                word,
                ref state,
                afterShortWord,
                abbr,
                initials);
        }

        BindTrailingInitial(ref buffer, word, ref state, initials);
    }

    /// <summary>Включено ли хоть одно словарное правило фазы.</summary>
    private static bool Enabled(RuleSet rules, out bool afterShortWord, out bool abbr, out bool initials)
    {
        afterShortWord = rules.Contains(RuleId.Common.Nbsp.AfterShortWord);
        abbr = rules.Contains(RuleId.Ru.Nbsp.Abbr);
        initials = rules.Contains(RuleId.Ru.Nbsp.Initials);
        return afterShortWord || abbr || initials;
    }

    /// <summary>
    /// Разбирает один текстовый сегмент, продолжая слово, начатое в предыдущем.
    /// Индексы — координаты ДОКУМЕНТА: патчится буфер, а не стековая копия слова.
    /// </summary>
    private static void BindSegment(
        ref CharBuffer buffer,
        ReadOnlySpan<char> document,
        int start,
        int end,
        Span<char> word,
        ref BindState state,
        bool afterShortWord,
        bool abbr,
        bool initials)
    {
        for (int i = start; i < end; i++)
        {
            char c = document[i];

            // Точка слово не НАЧИНАЕТ: «.дом» — это «дом» с точкой слева, и словарю
            // достаётся «дом», иначе четырёхсимвольное «.дом» перестаёт быть коротким
            // словом. Но и не завершает: внутри слова она может быть частью сокращения
            // или инициала, решение принимает следующий за ней пробел.
            if (c == '.' && state.WordLength == 0)
            {
                continue;
            }

            if (char.IsLetter(c) || c == '.')
            {
                if (state.WordLength < word.Length)
                {
                    word[state.WordLength++] = c;
                }
                else
                {
                    state.WordOverflow = true;
                }

                continue;
            }

            if (c != ' ')
            {
                // Токен закончился знаком препинания: «Пушкин А., автор». Вперёд связывать
                // нечего, но связь НАЗАД, с фамилией, инициалу по-прежнему нужна.
                BindTrailingInitial(ref buffer, word, ref state, initials);
                state.Reset();
                continue;
            }

            if (state.WordLength == 0)
            {
                state.PrevSpaceIndex = -1;
                continue;
            }

            ReadOnlySpan<char> token = word.Slice(0, state.WordLength);
            bool hasDot = token[token.Length - 1] == '.';
            ReadOnlySpan<char> letters = hasDot ? token.Slice(0, token.Length - 1) : token;
            bool isInitial = initials && !state.WordOverflow && IsInitial(token);

            bool bind = !state.WordOverflow
                && ((afterShortWord && !hasDot && Dictionaries.IsShortWord(letters))
                    || (abbr && hasDot && Dictionaries.IsAbbreviationPart(letters))
                    || isInitial);

            // Инициал связывает себя не только со следующим словом, но и с предыдущим —
            // «Пушкин А.» нуждается в неразрывном пробеле по обе стороны от «А.».
            if (isInitial && state.PrevSpaceIndex >= 0)
            {
                buffer.PatchAt(state.PrevSpaceIndex, Chars.Nbsp);
            }

            if (bind)
            {
                buffer.PatchAt(i, Chars.Nbsp);
            }

            state.WordLength = 0;
            state.WordOverflow = false;
            state.PrevSpaceIndex = i;
        }
    }

    /// <summary>
    /// Ставит неразрывный пробел ПЕРЕД инициалом, завершившимся на границе, за которой
    /// пробела нет: конец ввода или знак препинания.
    /// </summary>
    private static void BindTrailingInitial(
        ref CharBuffer buffer, ReadOnlySpan<char> word, ref BindState state, bool initials)
    {
        if (initials && !state.WordOverflow && state.PrevSpaceIndex >= 0
            && IsInitial(word.Slice(0, state.WordLength)))
        {
            buffer.PatchAt(state.PrevSpaceIndex, Chars.Nbsp);
        }
    }

    /// <summary>Инициал — одна прописная буква с точкой: «А.».</summary>
    private static bool IsInitial(ReadOnlySpan<char> token)
        => token.Length == 2 && token[1] == '.' && char.IsUpper(token[0]);
}
