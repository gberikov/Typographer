using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>Состояние фазы Bind, живущее сквозь текстовые сегменты документа.</summary>
internal struct BindState
{
    /// <summary>Сколько букв текущего слова уже собрано.</summary>
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
/// </remarks>
internal static class WordBinder
{
    /// <summary>
    /// Максимальная длина слова, которую фаза собирает через разметку. Всё длиннее
    /// заведомо не короткое слово, не сокращение и не инициал — накопление прекращается.
    /// </summary>
    private const int MaxWord = 32;

    public static void Run(ref CharBuffer buffer, RuleSet rules)
    {
        ReadOnlySpan<char> source = buffer.AsSpan();

        bool afterShortWord = rules.Contains(RuleId.Common.Nbsp.AfterShortWord);
        bool abbr = rules.Contains(RuleId.Ru.Nbsp.Abbr);
        bool initials = rules.Contains(RuleId.Ru.Nbsp.Initials);

        int wordStart = -1;

        // Индекс пробела перед текущим словом, -1 — если слова не разделены пробелом
        // (начало строки или после другой пунктуации). Нужен, чтобы связать фамилию с инициалом,
        // который идёт СЛЕДОМ: «Пушкин А.» — на момент обработки «Пушкин» ещё неизвестно, что
        // дальше инициал, поэтому решение принимается при разборе «А.» задним числом.
        int prevSpaceIndex = -1;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];

            if (char.IsLetter(c))
            {
                if (wordStart < 0)
                {
                    wordStart = i;
                }

                continue;
            }

            // Точка не завершает слово: она может быть частью сокращения или инициала,
            // решение принимает следующий за ней пробел.
            if (c == '.')
            {
                continue;
            }

            if (c != ' ')
            {
                // Токен закончился не пробелом, а знаком препинания: «Пушкин А., автор».
                // Вперёд связывать нечего — пробела справа нет, — но связь НАЗАД, с
                // фамилией, инициалу по-прежнему нужна.
                BindInitialToPreviousWord(ref buffer, source, wordStart, i, initials, prevSpaceIndex);
                wordStart = -1;
                prevSpaceIndex = -1;
                continue;
            }

            if (wordStart < 0)
            {
                prevSpaceIndex = -1;
                continue;
            }

            ReadOnlySpan<char> token = source.Slice(wordStart, i - wordStart);
            bool hasDot = token[token.Length - 1] == '.';
            ReadOnlySpan<char> letters = hasDot ? token.Slice(0, token.Length - 1) : token;
            bool isInitial = initials && IsInitial(token);

            bool bind =
                (afterShortWord && !hasDot && Dictionaries.IsShortWord(letters))
                || (abbr && hasDot && Dictionaries.IsAbbreviationPart(letters))
                || isInitial;

            // Инициал связывает себя не только со следующим словом, но и с предыдущим —
            // «Пушкин А.» нуждается в неразрывном пробеле по обе стороны от «А.».
            if (isInitial && prevSpaceIndex >= 0)
            {
                buffer.PatchAt(prevSpaceIndex, Chars.Nbsp);
            }

            if (bind)
            {
                buffer.PatchAt(i, Chars.Nbsp);
            }

            wordStart = -1;
            prevSpaceIndex = i;
        }

        // Конец ввода тоже завершает токен: «Пушкин А.» кончается инициалом, и без этого
        // разбор последнего слова не запускался вовсе.
        BindInitialToPreviousWord(ref buffer, source, wordStart, source.Length, initials, prevSpaceIndex);
    }

    /// <summary>
    /// Ставит неразрывный пробел ПЕРЕД инициалом, завершившимся на границе, за которой
    /// пробела нет: конец ввода или знак препинания.
    /// </summary>
    private static void BindInitialToPreviousWord(
        ref CharBuffer buffer,
        ReadOnlySpan<char> source,
        int wordStart,
        int wordEnd,
        bool initials,
        int prevSpaceIndex)
    {
        if (!initials || wordStart < 0 || prevSpaceIndex < 0)
        {
            return;
        }

        if (IsInitial(source.Slice(wordStart, wordEnd - wordStart)))
        {
            buffer.PatchAt(prevSpaceIndex, Chars.Nbsp);
        }
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
        bool afterShortWord = rules.Contains(RuleId.Common.Nbsp.AfterShortWord);
        bool abbr = rules.Contains(RuleId.Ru.Nbsp.Abbr);
        bool initials = rules.Contains(RuleId.Ru.Nbsp.Initials);
        if (!afterShortWord && !abbr && !initials)
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

            BindSegment(ref buffer, document, segment, word, ref state, afterShortWord, abbr, initials);
        }

        // Конец документа тоже завершает токен: «Пушкин А.» кончается инициалом.
        if (initials && state.WordLength == 2 && state.PrevSpaceIndex >= 0
            && IsInitial(word.Slice(0, state.WordLength)))
        {
            buffer.PatchAt(state.PrevSpaceIndex, Chars.Nbsp);
        }
    }

    /// <summary>Разбирает один текстовый сегмент документа, продолжая начатое слово.</summary>
    private static void BindSegment(
        ref CharBuffer buffer,
        ReadOnlySpan<char> document,
        Segment segment,
        Span<char> word,
        ref BindState state,
        bool afterShortWord,
        bool abbr,
        bool initials)
    {
        int end = segment.Start + segment.Length;
        for (int i = segment.Start; i < end; i++)
        {
            char c = document[i];

            if (char.IsLetter(c) || c == '.')
            {
                // Точка слово не завершает: она может быть частью сокращения или инициала,
                // решение принимает следующий за ней пробел.
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
                if (initials && !state.WordOverflow && state.PrevSpaceIndex >= 0
                    && IsInitial(word.Slice(0, state.WordLength)))
                {
                    buffer.PatchAt(state.PrevSpaceIndex, Chars.Nbsp);
                }

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

    /// <summary>Инициал — одна прописная буква с точкой: «А.».</summary>
    private static bool IsInitial(ReadOnlySpan<char> token)
        => token.Length == 2 && token[1] == '.' && char.IsUpper(token[0]);
}
