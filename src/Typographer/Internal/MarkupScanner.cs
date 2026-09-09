namespace Typographer.Internal;

/// <summary>Вид сегмента входного текста.</summary>
internal enum SegmentKind
{
    /// <summary>Текстовый узел: к нему применяются правила типографики.</summary>
    Text,

    /// <summary>Тег целиком, включая значения атрибутов: копируется без изменений.</summary>
    Markup,

    /// <summary>Защищённая зона (code, pre, script, комментарий): копируется без изменений.</summary>
    Protected,
}

/// <summary>Сегмент входного текста.</summary>
/// <param name="Kind">Вид сегмента.</param>
/// <param name="Start">Смещение от начала входа.</param>
/// <param name="Length">Длина в символах.</param>
/// <param name="IsBlock">
/// Разметка блочного уровня (<c>&lt;p&gt;</c>, <c>&lt;br&gt;</c>, <c>&lt;li&gt;</c>…).
/// Для правил это граница строки, а не продолжение предложения.
/// </param>
/// <param name="PreventsParagraphWrapping">
/// Тег, вокруг которого нельзя добавлять абзац. <c>&lt;br&gt;</c> начинает новую строку,
/// но сам по себе не запрещает оборачивать окружающий текст в <c>&lt;p&gt;</c>.
/// </param>
internal readonly record struct Segment(
    SegmentKind Kind,
    int Start,
    int Length,
    bool IsBlock = false,
    bool PreventsParagraphWrapping = false);

/// <summary>
/// Фаза Protect: делит вход на текстовые узлы, разметку и защищённые зоны.
/// Ломаный HTML ошибкой не считается и выводится как есть.
/// </summary>
internal ref struct MarkupScanner
{
    private const string CDataPrefix = "<![CDATA[";

    private static readonly string[] ProtectedTags =
        ["code", "pre", "script", "style", "textarea", "kbd", "samp"];

    // Элементы «сырого текста»: их содержимое не разметка вовсе. Внутри <script> угловая
    // скобка в строковом литерале тегом не является, комментарий не начинается, а вложить
    // одноимённый элемент невозможно — поэтому зону закрывает ПЕРВЫЙ же закрывающий тег.
    // У остальных защищённых элементов (code, pre, kbd, samp) содержимое обычное, и конец
    // зоны приходится искать структурно.
    private static readonly string[] RawTextTags = ["script", "style", "textarea"];

    // Теги, которые начинают новую строку или новый блок текста. Список нужен правилам фазы
    // Scan: за закрывающим </p> начинается следующий абзац, и кавычка в его начале обязана
    // быть открывающей, а дефис — тире прямой речи. Инлайновые теги (<b>, <a>, <span>)
    // предложение не разрывают и сюда не входят.
    private static readonly string[] BlockTags =
    [
        "p", "br", "div", "hr", "li", "ul", "ol", "dl", "dt", "dd",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "table", "tr", "td", "th", "thead", "tbody", "tfoot", "caption",
        "blockquote", "section", "article", "header", "footer", "aside", "nav", "main",
        "figure", "figcaption", "address", "form", "fieldset", "pre", "details", "summary",
        "dialog", "menu", "search", "center", "dir",
    ];

    private readonly ReadOnlySpan<char> _source;
    private int _position;
    private int _protectedUntil;

    public MarkupScanner(ReadOnlySpan<char> source)
    {
        _source = source;
        _position = 0;
        _protectedUntil = 0;
        HasUnclosedMarkup = false;
    }

    /// <summary>Найден незакрытый тег, комментарий, CDATA или защищённый элемент.</summary>
    public bool HasUnclosedMarkup { get; private set; }

    public bool TryRead(out Segment segment)
    {
        if (_position >= _source.Length)
        {
            segment = default;
            return false;
        }

        int start = _position;

        // Внутри защищённой зоны (например, <code>) даже похожий на тег фрагмент
        // не является разметкой — проверка обязана идти раньше распознавания
        // тега и комментария, иначе внутренние теги вида <code><b>x</b></code>
        // ошибочно уйдут как Markup.
        if (_protectedUntil > start)
        {
            _position = Math.Min(_protectedUntil, _source.Length);
            segment = new Segment(SegmentKind.Protected, start, _position - start);
            return true;
        }

        if (IsCommentStart(start))
        {
            int end = IndexOfSequence(start + 4, "-->");
            HasUnclosedMarkup |= end < 0;
            _position = end < 0 ? _source.Length : end + 3;
            segment = new Segment(SegmentKind.Protected, start, _position - start);
            return true;
        }

        // Секция CDATA заканчивается на "]]>", а не на первой угловой скобке: внутри неё
        // '>' — обычный символ. Проверка обязана идти раньше разбора тега, который иначе
        // оборвал бы секцию на первом же '>' и отдал её хвост в типографирование.
        if (IsCDataStart(start))
        {
            int end = IndexOfSequence(start + CDataPrefix.Length, "]]>");
            HasUnclosedMarkup |= end < 0;
            _position = end < 0 ? _source.Length : end + 3;
            segment = new Segment(SegmentKind.Protected, start, _position - start);
            return true;
        }

        if (IsTagStart(start))
        {
            _position = ReadTagEnd(start);
            bool isBlock = IsBlockTag(start, _position, out bool isBreak);
            segment = new Segment(
                SegmentKind.Markup,
                start,
                _position - start,
                isBlock,
                isBlock && !isBreak);
            return true;
        }

        while (_position < _source.Length && !IsTagStart(_position) && !IsCommentStart(_position))
        {
            _position++;
        }

        segment = new Segment(SegmentKind.Text, start, _position - start);
        return true;
    }

    private readonly bool IsCommentStart(int index)
        => index + 3 < _source.Length
           && _source[index] == '<' && _source[index + 1] == '!'
           && _source[index + 2] == '-' && _source[index + 3] == '-';

    private readonly bool IsCDataStart(int index)
        => index + CDataPrefix.Length <= _source.Length
           && _source.Slice(index, CDataPrefix.Length).SequenceEqual(CDataPrefix.AsSpan());

    private readonly bool IsTagStart(int index)
    {
        if (index + 1 >= _source.Length || _source[index] != '<')
        {
            return false;
        }

        char next = _source[index + 1];
        return char.IsLetter(next) || next is '/' or '!' or '?';
    }

    private int ReadTagEnd(int start)
    {
        int end = SkipTag(start, out bool closed);
        HasUnclosedMarkup |= !closed;
        if (closed)
        {
            MarkProtectedContent(start, end);
        }

        return end;
    }

    /// <summary>
    /// Индекс сразу за концом тега, начинающегося на <paramref name="start"/>, или конец
    /// входа у незакрытого тега. Угловая скобка внутри значения атрибута тег не завершает.
    /// </summary>
    private readonly int SkipTag(int start) => SkipTag(start, out _);

    private readonly int SkipTag(int start, out bool closed)
    {
        int index = start + 1;
        char quote = '\0';

        while (index < _source.Length)
        {
            char c = _source[index];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                closed = true;
                return index + 1;
            }

            index++;
        }

        closed = false;
        return _source.Length;
    }

    private void MarkProtectedContent(int tagStart, int tagEnd)
    {
        if (_source[tagStart + 1] == '/')
        {
            return;
        }

        ReadOnlySpan<char> name = ReadTagName(tagStart + 1, tagEnd);
        foreach (string tag in ProtectedTags)
        {
            if (name.Equals(tag.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                int closing = IsRawTextTag(name)
                    ? IndexOfClosingTag(tagEnd, tag)
                    : IndexOfStructuralClosingTag(tagEnd, tag);
                HasUnclosedMarkup |= closing < 0;
                _protectedUntil = closing < 0 ? _source.Length : closing;
                return;
            }
        }
    }

    private static bool IsRawTextTag(ReadOnlySpan<char> name)
    {
        foreach (string tag in RawTextTags)
        {
            if (name.Equals(tag.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Конец защищённой зоны с обычным HTML-содержимым: <c>code</c>, <c>pre</c>,
    /// <c>kbd</c>, <c>samp</c>.
    /// </summary>
    /// <remarks>
    /// Простой текстовый поиск закрывающего тега здесь не годится: он обрывает зону на
    /// первом совпадении, где бы оно ни стояло. Три случая, в которых это молча снимало
    /// защиту с остатка зоны, — закрывающий тег внутри комментария, внутри значения
    /// атрибута и закрывающий тег ВЛОЖЕННОГО одноимённого элемента. Поэтому обход идёт
    /// структурно: комментарии пропускаются целиком, теги — вместе со значениями
    /// атрибутов, а вложенные одноимённые элементы считаются.
    /// </remarks>
    private readonly int IndexOfStructuralClosingTag(int from, string tag)
    {
        int depth = 0;
        int index = from;
        while (index < _source.Length)
        {
            if (IsCommentStart(index))
            {
                int end = IndexOfSequence(index + 4, "-->");
                index = end < 0 ? _source.Length : end + 3;
                continue;
            }

            if (!IsTagStart(index))
            {
                index++;
                continue;
            }

            bool closing = _source[index + 1] == '/';
            bool matches = IsTagNamed(index + (closing ? 2 : 1), tag);
            int tagEnd = SkipTag(index);

            // Вложенный элемент с сырым текстом пропускается целиком: строка "<pre>"
            // внутри script или textarea не открывает ещё один уровень внешнего pre.
            ReadOnlySpan<char> name = ReadTagName(index + 1, tagEnd);
            if (!closing && IsRawTextTag(name))
            {
                int rawClosing = IndexOfClosingTag(tagEnd, name);
                if (rawClosing < 0)
                {
                    return -1;
                }

                index = SkipTag(rawClosing);
                continue;
            }

            if (closing && matches)
            {
                if (depth == 0)
                {
                    return index;
                }

                depth--;
            }
            else if (matches && !IsSelfClosing(index, tagEnd))
            {
                depth++;
            }

            index = tagEnd > index ? tagEnd : index + 1;
        }

        return -1;
    }

    private readonly bool IsTagNamed(int nameStart, string tag)
        => ReadTagName(nameStart, _source.Length).Equals(tag.AsSpan(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Тег вида <c>&lt;code /&gt;</c> содержимого не открывает.</summary>
    private readonly bool IsSelfClosing(int tagStart, int tagEnd)
        => tagEnd - tagStart >= 3 && tagEnd <= _source.Length
           && _source[tagEnd - 1] == '>' && _source[tagEnd - 2] == '/';

    private readonly bool IsBlockTag(int tagStart, int tagEnd, out bool isBreak)
    {
        isBreak = false;
        int nameStart = tagStart + 1;
        if (nameStart < tagEnd && _source[nameStart] == '/')
        {
            nameStart++;
        }

        ReadOnlySpan<char> name = ReadTagName(nameStart, tagEnd);
        foreach (string tag in BlockTags)
        {
            if (name.Equals(tag.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                isBreak = name.Equals("br".AsSpan(), StringComparison.OrdinalIgnoreCase);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Имя тега, начинающееся на <paramref name="nameStart"/>, или пустой срез, если имени
    /// нет. Имя обязано заканчиваться границей — <c>'&gt;'</c>, <c>'/'</c> или пробельным
    /// символом: без этой проверки имя читалось бы до первого не-буквенно-цифрового символа,
    /// и пользовательский элемент <c>&lt;code-block&gt;</c> притворялся бы <c>&lt;code&gt;</c>
    /// (закрывающий <c>&lt;/code&gt;</c> при этом не находится никогда, и защищённой
    /// оказывалась вся оставшаяся часть документа).
    /// </summary>
    private readonly ReadOnlySpan<char> ReadTagName(int nameStart, int tagEnd)
    {
        int nameEnd = nameStart;
        while (nameEnd < tagEnd && (char.IsLetter(_source[nameEnd]) || char.IsDigit(_source[nameEnd])))
        {
            nameEnd++;
        }

        if (nameEnd == nameStart || nameEnd >= _source.Length)
        {
            return default;
        }

        char after = _source[nameEnd];
        return after is '>' or '/' || char.IsWhiteSpace(after)
            ? _source.Slice(nameStart, nameEnd - nameStart)
            : default;
    }

    private readonly int IndexOfClosingTag(int from, ReadOnlySpan<char> tag)
    {
        for (int i = from; i + 1 < _source.Length; i++)
        {
            if (_source[i] != '<' || _source[i + 1] != '/')
            {
                continue;
            }

            // Имя тега сравнивается срезом фиксированной длины, поэтому "code"
            // без проверки границы совпало бы и с началом "codex"/"codemirror".
            // Совпадением считаем только тег, чьё имя заканчивается ровно там,
            // где после него идёт '>', пробельный символ, либо конец входа.
            int nameEnd = i + 2 + tag.Length;
            if (nameEnd > _source.Length
                || !_source.Slice(i + 2, tag.Length).Equals(tag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (nameEnd == _source.Length || _source[nameEnd] == '>' || char.IsWhiteSpace(_source[nameEnd]))
            {
                return i;
            }
        }

        return -1;
    }

    private readonly int IndexOfSequence(int from, string needle)
    {
        if (from >= _source.Length)
        {
            return -1;
        }

        int found = _source.Slice(from).IndexOf(needle.AsSpan(), StringComparison.Ordinal);
        return found < 0 ? -1 : from + found;
    }
}
