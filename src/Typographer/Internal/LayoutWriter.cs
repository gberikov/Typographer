namespace Typographer.Internal;

/// <summary>Фаза Layout: неразрывные блоки, переносы строк и абзацы.</summary>
/// <remarks>
/// Фаза работает на двух разных уровнях, и путать их нельзя. Неразрывные блоки живут ВНУТРИ
/// текстового узла: цепочка слов через тег не тянется. Абзацы и переносы строк, наоборот,
/// живут на уровне ДОКУМЕНТА: <c>&lt;p&gt;</c> — блочный тег, и обёртка вокруг каждого
/// текстового узла порождала бы абзац внутри <c>&lt;b&gt;</c> и абзац из одного пробела
/// между двумя тегами.
/// </remarks>
internal static class LayoutWriter
{
    private static readonly string[] VoidTags =
    [
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta",
        "param", "source", "track", "wbr",
    ];

    /// <summary>Уровень текстового узла: неразрывные блоки.</summary>
    public static void Run(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)
    {
        if (options.MaxNobr > 0)
        {
            WriteWithNobr(source, options.MaxNobr, ref buffer);
            return;
        }

        buffer.Write(source);
    }

    /// <summary>
    /// Уровень документа: переносы строк и абзацы по готовому телу документа.
    /// Переводы строк внутри разметки и защищённых зон остаются без изменений.
    /// </summary>
    /// <param name="source">Тело документа: текстовые узлы уже обработаны, разметка на месте.</param>
    /// <param name="useBr">Заменять перевод строки тегом переноса.</param>
    /// <param name="useP">Оборачивать абзацы в теги абзаца.</param>
    /// <param name="buffer">Приёмник.</param>
    public static void WriteBreaks(ReadOnlySpan<char> source, bool useBr, bool useP, ref CharBuffer buffer)
    {
        if (useP)
        {
            WriteParagraphs(source, useBr, ref buffer);
            return;
        }

        if (useBr)
        {
            WriteWithBr(source, ref buffer);
            return;
        }

        buffer.Write(source);
    }

    /// <summary>
    /// Разбивает текст на цепочки слов, склеенных неразрывными пробелами, и оборачивает
    /// каждую цепочку (или её часть — см. <see cref="WriteChain"/>) в &lt;nobr&gt;.
    /// На каждой итерации курсор <c>i</c> либо продвигается на один символ (границы,
    /// одиночный неразрывный пробел), либо перескакивает на конец только что распознанного
    /// слова или всей цепочки — оба варианта строго больше текущего <c>i</c>, поэтому обход
    /// гарантированно завершается и линеен по длине входа.
    /// </summary>
    private static void WriteWithNobr(ReadOnlySpan<char> source, int maxWords, ref CharBuffer buffer)
    {
        int flushStart = 0;
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            if (IsBoundary(c) || c == Chars.Nbsp)
            {
                i++;
                continue;
            }

            int wordEnd = FindWordEnd(source, i);
            if (wordEnd >= source.Length || source[wordEnd] != Chars.Nbsp || !HasWordAfter(source, wordEnd + 1))
            {
                // Слово не является началом цепочки — продвигаем курсор сразу за него.
                i = wordEnd;
                continue;
            }

            int chainEnd = wordEnd;
            while (chainEnd < source.Length && source[chainEnd] == Chars.Nbsp && HasWordAfter(source, chainEnd + 1))
            {
                chainEnd = FindWordEnd(source, chainEnd + 1);
            }

            buffer.Write(source.Slice(flushStart, i - flushStart));
            WriteChain(source.Slice(i, chainEnd - i), maxWords, ref buffer);
            i = chainEnd;
            flushStart = i;
        }

        buffer.Write(source.Slice(flushStart));
    }

    /// <summary>Конец слова начиная с <paramref name="start"/> — первая граница или неразрывный пробел.</summary>
    private static int FindWordEnd(ReadOnlySpan<char> source, int start)
    {
        int end = start;
        while (end < source.Length && !IsBoundary(source[end]) && source[end] != Chars.Nbsp)
        {
            end++;
        }

        return end;
    }

    private static bool HasWordAfter(ReadOnlySpan<char> source, int pos)
        => pos < source.Length && !IsBoundary(source[pos]) && source[pos] != Chars.Nbsp;

    /// <summary>
    /// Оборачивает цепочку слов, склеенных неразрывными пробелами, в один или несколько
    /// блоков &lt;nobr&gt;. Блок держит не менее двух слов: при <paramref name="maxWords"/>
    /// меньше двух блоки не создаются вовсе (цепочка копируется как есть); хвост цепочки
    /// короче двух слов остаётся неоформленным текстом. Неразрывный пробел, разделяющий два
    /// блока, выносится за пределы тегов — между &lt;/nobr&gt; и следующим &lt;nobr&gt; (или
    /// хвостом), а не приклеивается к началу следующего блока. Каждый символ входа
    /// записывается ровно один раз в исходном порядке — снятие тегов восстанавливает вход
    /// побайтово.
    /// </summary>
    private static void WriteChain(ReadOnlySpan<char> chain, int maxWords, ref CharBuffer buffer)
    {
        if (maxWords < 2)
        {
            buffer.Write(chain);
            return;
        }

        int wordsRemaining = 1;
        for (int k = 0; k < chain.Length; k++)
        {
            if (chain[k] == Chars.Nbsp)
            {
                wordsRemaining++;
            }
        }

        bool groupOpen = false;
        int wordsInGroup = 0;
        int pos = 0;
        while (pos < chain.Length)
        {
            if (!groupOpen && wordsRemaining >= 2)
            {
                buffer.Write("<nobr>");
                groupOpen = true;
                wordsInGroup = 0;
            }

            int wordEnd = pos;
            while (wordEnd < chain.Length && chain[wordEnd] != Chars.Nbsp)
            {
                wordEnd++;
            }

            buffer.Write(chain.Slice(pos, wordEnd - pos));
            wordsInGroup++;
            wordsRemaining--;
            pos = wordEnd;

            if (groupOpen && (wordsInGroup == maxWords || wordsRemaining == 0))
            {
                buffer.Write("</nobr>");
                groupOpen = false;
            }

            if (pos < chain.Length)
            {
                buffer.Write(chain[pos]);
                pos++;
            }
        }
    }

    // Возврат каретки не нормализуется в перевод строки — он просто ТОЖЕ считается границей
    // слова, наравне с пробелом и переводом строки, чтобы windows-перевод строки (\r\n) не
    // склеивал соседние слова в одну неразрывную цепочку.
    private static bool IsBoundary(char c) => c is ' ' or '\n' or '\t' or '\r';

    /// <summary>
    /// Пишет текст, ставя тег переноса ПЕРЕД каждым переводом строки целиком.
    /// Windows-перевод (<c>\r\n</c>) — один перенос, а не два: тег, вставленный между
    /// <c>\r</c> и <c>\n</c>, разорвал бы пару и оставил в выводе одинокий возврат каретки.
    /// </summary>
    private static void WriteWithBr(ReadOnlySpan<char> source, ref CharBuffer buffer)
    {
        var scanner = new MarkupScanner(source);
        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = source.Slice(segment.Start, segment.Length);
            if (segment.Kind == SegmentKind.Text)
            {
                WriteTextWithBr(slice, ref buffer);
            }
            else
            {
                buffer.Write(slice);
            }
        }
    }

    private static void WriteTextWithBr(ReadOnlySpan<char> source, ref CharBuffer buffer)
    {
        for (int i = 0; i < source.Length; i++)
        {
            int length = NewlineLength(source, i);
            if (length == 0)
            {
                buffer.Write(source[i]);
                continue;
            }

            buffer.Write("<br />");
            buffer.Write(source.Slice(i, length));
            i += length - 1;
        }
    }

    /// <summary>Длина перевода строки на позиции <paramref name="index"/>, или ноль.</summary>
    private static int NewlineLength(ReadOnlySpan<char> source, int index)
    {
        if (source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n')
        {
            return 2;
        }

        return source[index] == '\n' ? 1 : 0;
    }

    private static void WriteParagraphs(ReadOnlySpan<char> source, bool useBr, ref CharBuffer buffer)
    {
        // Добавленные <p> не должны пересекать существующую разметку. Если пустая строка
        // находится внутри открытого inline-элемента, безопасно разделить его на абзацы
        // без переписывания исходных тегов невозможно. В таком случае UseP пропускается,
        // а независимая опция UseBr продолжает работать.
        if (ParagraphSeparatorCrossesElement(source))
        {
            if (useBr)
            {
                WriteWithBr(source, ref buffer);
            }
            else
            {
                buffer.Write(source);
            }

            return;
        }

        var scanner = new MarkupScanner(source);
        int paragraphStart = 0;
        bool first = true;
        while (scanner.TryRead(out Segment segment))
        {
            if (segment.Kind != SegmentKind.Text)
            {
                continue;
            }

            ReadOnlySpan<char> text = source.Slice(segment.Start, segment.Length);
            int offset = 0;
            while (offset < text.Length)
            {
                int separator = FindDoubleNewline(text, offset, out int separatorLength);
                if (separator < 0)
                {
                    break;
                }

                // Ищем границу только в тексте, но оборачиваем весь абзац вместе с
                // инлайновыми тегами и защищёнными элементами между его границами.
                int end = segment.Start + separator;
                WriteParagraph(source.Slice(paragraphStart, end - paragraphStart), useBr, ref first, ref buffer);
                offset = separator + separatorLength;
                paragraphStart = segment.Start + offset;
            }
        }

        WriteParagraph(source.Slice(paragraphStart), useBr, ref first, ref buffer);
    }

    private static bool ParagraphSeparatorCrossesElement(ReadOnlySpan<char> source)
    {
        var scanner = new MarkupScanner(source);
        int depth = 0;
        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = source.Slice(segment.Start, segment.Length);
            if (segment.Kind == SegmentKind.Text)
            {
                if (depth > 0 && FindDoubleNewline(slice, 0, out _) >= 0)
                {
                    return true;
                }

                continue;
            }

            if (segment.Kind == SegmentKind.Markup)
            {
                UpdateElementDepth(slice, ref depth);
            }
        }

        return false;
    }

    private static void UpdateElementDepth(ReadOnlySpan<char> tag, ref int depth)
    {
        if (tag.Length < 3 || tag[0] != '<' || tag[1] is '!' or '?')
        {
            return;
        }

        bool closing = tag[1] == '/';
        int nameStart = closing ? 2 : 1;
        int nameEnd = nameStart;
        while (nameEnd < tag.Length
               && !char.IsWhiteSpace(tag[nameEnd])
               && tag[nameEnd] is not ('/' or '>'))
        {
            nameEnd++;
        }

        if (nameEnd == nameStart)
        {
            return;
        }

        if (closing)
        {
            depth = Math.Max(0, depth - 1);
            return;
        }

        ReadOnlySpan<char> name = tag.Slice(nameStart, nameEnd - nameStart);
        if (!IsSelfClosing(tag) && !IsVoidTag(name))
        {
            depth++;
        }
    }

    private static bool IsSelfClosing(ReadOnlySpan<char> tag)
    {
        int index = tag.Length - 2;
        while (index >= 0 && char.IsWhiteSpace(tag[index]))
        {
            index--;
        }

        return index >= 0 && tag[index] == '/';
    }

    private static bool IsVoidTag(ReadOnlySpan<char> name)
    {
        foreach (string tag in VoidTags)
        {
            if (name.Equals(tag.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteParagraph(ReadOnlySpan<char> paragraph, bool useBr, ref bool first, ref CharBuffer buffer)
    {
        // Двойные переводы строк в начале и конце документа не создают пустых абзацев.
        if (paragraph.IsEmpty)
        {
            return;
        }

        if (!first)
        {
            buffer.Write('\n');
        }

        buffer.Write("<p>");
        if (useBr)
        {
            WriteWithBr(paragraph, ref buffer);
        }
        else
        {
            buffer.Write(paragraph);
        }

        buffer.Write("</p>");
        first = false;
    }

    /// <summary>
    /// Индекс начала первой границы абзаца на позиции from и далее, или -1. Переводы строк
    /// не нормализуются: распознаются обе формы — Unix (<c>"\n"</c>) и Windows
    /// (<c>"\r\n"</c>), — а <paramref name="separatorLength"/> сообщает вызывающему коду,
    /// сколько символов входа занимает найденная граница.
    /// </summary>
    /// <remarks>
    /// Граница съедается ЦЕЛИКОМ: три и более переводов строки подряд — всё ещё одна
    /// граница. Иначе лишний перевод строки оставался бы в начале следующего абзаца и при
    /// включённом переносе превращался бы там в тег переноса из ниоткуда.
    /// </remarks>
    private static int FindDoubleNewline(ReadOnlySpan<char> source, int from, out int separatorLength)
    {
        for (int i = from; i < source.Length; i++)
        {
            int first = NewlineLength(source, i);
            if (first == 0)
            {
                continue;
            }

            int second = i + first < source.Length ? NewlineLength(source, i + first) : 0;
            if (second == 0)
            {
                i += first - 1;
                continue;
            }

            int end = i + first + second;
            while (end < source.Length)
            {
                int more = NewlineLength(source, end);
                if (more == 0)
                {
                    break;
                }

                end += more;
            }

            separatorLength = end - i;
            return i;
        }

        separatorLength = 0;
        return -1;
    }
}
