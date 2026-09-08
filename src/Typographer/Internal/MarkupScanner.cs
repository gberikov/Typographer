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
internal readonly record struct Segment(SegmentKind Kind, int Start, int Length);

/// <summary>
/// Фаза Protect: делит вход на текстовые узлы, разметку и защищённые зоны.
/// Ломаный HTML ошибкой не считается и выводится как есть.
/// </summary>
internal ref struct MarkupScanner
{
    private static readonly string[] ProtectedTags =
        ["code", "pre", "script", "style", "textarea", "kbd", "samp"];

    private readonly ReadOnlySpan<char> _source;
    private int _position;
    private int _protectedUntil;

    public MarkupScanner(ReadOnlySpan<char> source)
    {
        _source = source;
        _position = 0;
        _protectedUntil = 0;
    }

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
            _position = end < 0 ? _source.Length : end + 3;
            segment = new Segment(SegmentKind.Protected, start, _position - start);
            return true;
        }

        if (IsTagStart(start))
        {
            _position = ReadTagEnd(start);
            segment = new Segment(SegmentKind.Markup, start, _position - start);
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
                index++;
                MarkProtectedContent(start, index);
                return index;
            }

            index++;
        }

        return _source.Length;
    }

    private void MarkProtectedContent(int tagStart, int tagEnd)
    {
        if (_source[tagStart + 1] == '/')
        {
            return;
        }

        int nameStart = tagStart + 1;
        int nameEnd = nameStart;
        while (nameEnd < tagEnd && char.IsLetter(_source[nameEnd]))
        {
            nameEnd++;
        }

        ReadOnlySpan<char> name = _source.Slice(nameStart, nameEnd - nameStart);
        foreach (string tag in ProtectedTags)
        {
            if (name.Equals(tag.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                int closing = IndexOfClosingTag(tagEnd, tag);
                _protectedUntil = closing < 0 ? _source.Length : closing;
                return;
            }
        }
    }

    private readonly int IndexOfClosingTag(int from, string tag)
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
                || !_source.Slice(i + 2, tag.Length).Equals(tag.AsSpan(), StringComparison.OrdinalIgnoreCase))
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
