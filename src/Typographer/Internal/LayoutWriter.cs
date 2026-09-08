namespace Typographer.Internal;

/// <summary>Фаза Layout: неразрывные блоки, переносы строк и абзацы.</summary>
internal static class LayoutWriter
{
    public static void Run(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)
    {
        if (options.MaxNobr > 0)
        {
            WriteWithNobr(source, options, ref buffer);
            return;
        }

        WriteBreaks(source, options, ref buffer);
    }

    /// <summary>
    /// Разбивает текст на цепочки слов, склеенных неразрывными пробелами, и оборачивает
    /// каждую цепочку (или её часть — см. <see cref="WriteChain"/>) в &lt;nobr&gt;.
    /// На каждой итерации курсор <c>i</c> либо продвигается на один символ (границы,
    /// одиночный неразрывный пробел), либо перескакивает на конец только что распознанного
    /// слова или всей цепочки — оба варианта строго больше текущего <c>i</c>, поэтому обход
    /// гарантированно завершается и линеен по длине входа.
    /// </summary>
    private static void WriteWithNobr(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)
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

            WriteBreaks(source.Slice(flushStart, i - flushStart), options, ref buffer);
            WriteChain(source.Slice(i, chainEnd - i), options.MaxNobr, ref buffer);
            i = chainEnd;
            flushStart = i;
        }

        WriteBreaks(source.Slice(flushStart), options, ref buffer);
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

    private static void WriteBreaks(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)
    {
        if (!options.UseBr && !options.UseP)
        {
            buffer.Write(source);
            return;
        }

        if (options.UseP)
        {
            WriteParagraphs(source, options, ref buffer);
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                buffer.Write("<br />");
            }

            buffer.Write(source[i]);
        }
    }

    private static void WriteParagraphs(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)
    {
        int start = 0;
        bool first = true;
        while (start < source.Length)
        {
            int separator = FindDoubleNewline(source, start, out int separatorLength);
            ReadOnlySpan<char> paragraph = separator < 0
                ? source.Slice(start)
                : source.Slice(start, separator - start);

            // Пустой абзац — разметка из ничего (двойной перевод строки в начале/конце
            // входа или три и более подряд): типограф не добавляет в чужой HTML пустых
            // блоков, поэтому такой сегмент просто пропускается.
            if (paragraph.Length > 0)
            {
                if (!first)
                {
                    buffer.Write('\n');
                }

                buffer.Write("<p>");
                if (options.UseBr)
                {
                    for (int i = 0; i < paragraph.Length; i++)
                    {
                        if (paragraph[i] == '\n')
                        {
                            buffer.Write("<br />");
                        }

                        buffer.Write(paragraph[i]);
                    }
                }
                else
                {
                    buffer.Write(paragraph);
                }

                buffer.Write("</p>");
                first = false;
            }

            start = separator < 0 ? source.Length : separator + separatorLength;
        }
    }

    /// <summary>
    /// Индекс начала первой границы абзаца на позиции from и далее, или -1. Переводы строк не
    /// нормализуются: распознаются обе формы двойного перевода строки — Unix (<c>"\n\n"</c>,
    /// длина 2) и Windows (<c>"\r\n\r\n"</c>, длина 4) — <paramref name="separatorLength"/>
    /// сообщает вызывающему коду, сколько символов входа занимает найденная граница.
    /// </summary>
    private static int FindDoubleNewline(ReadOnlySpan<char> source, int from, out int separatorLength)
    {
        for (int i = from; i < source.Length; i++)
        {
            if (source[i] == '\r' && i + 3 < source.Length
                && source[i + 1] == '\n' && source[i + 2] == '\r' && source[i + 3] == '\n')
            {
                separatorLength = 4;
                return i;
            }

            if (source[i] == '\n' && i + 1 < source.Length && source[i + 1] == '\n')
            {
                separatorLength = 2;
                return i;
            }
        }

        separatorLength = 0;
        return -1;
    }
}
