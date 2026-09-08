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

    private static void WriteWithNobr(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)
    {
        int index = 0;
        while (index < source.Length)
        {
            int groupStart = FindNobrGroupStart(source, index);
            if (groupStart < 0)
            {
                WriteBreaks(source.Slice(index), options, ref buffer);
                return;
            }

            WriteBreaks(source.Slice(index, groupStart - index), options, ref buffer);

            int groupEnd = FindNobrGroupEnd(source, groupStart, options.MaxNobr);
            buffer.Write("<nobr>");
            buffer.Write(source.Slice(groupStart, groupEnd - groupStart));
            buffer.Write("</nobr>");
            index = groupEnd;
        }
    }

    /// <summary>Начало неразрывной группы — начало слова, за которым идёт неразрывный пробел.</summary>
    private static int FindNobrGroupStart(ReadOnlySpan<char> source, int from)
    {
        int nbsp = source.Slice(from).IndexOf(Chars.Nbsp);
        if (nbsp < 0)
        {
            return -1;
        }

        int position = from + nbsp;
        while (position > from && !IsBoundary(source[position - 1]))
        {
            position--;
        }

        return position;
    }

    /// <summary>Конец группы — после указанного числа слов или на первом обычном пробеле.</summary>
    private static int FindNobrGroupEnd(ReadOnlySpan<char> source, int start, int maxWords)
    {
        int words = 1;
        int position = start;
        while (position < source.Length)
        {
            char c = source[position];
            if (c == Chars.Nbsp)
            {
                if (++words > maxWords)
                {
                    return position;
                }
            }
            else if (IsBoundary(c))
            {
                return position;
            }

            position++;
        }

        return source.Length;
    }

    private static bool IsBoundary(char c) => c is ' ' or '\n' or '\t';

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
            int separator = FindDoubleNewline(source, start);
            ReadOnlySpan<char> paragraph = separator < 0
                ? source.Slice(start)
                : source.Slice(start, separator - start);

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
            start = separator < 0 ? source.Length : separator + 2;
        }
    }

    /// <summary>Индекс начала первого «\n\n» на позиции from и далее, или -1.</summary>
    private static int FindDoubleNewline(ReadOnlySpan<char> source, int from)
    {
        for (int i = from; i < source.Length - 1; i++)
        {
            if (source[i] == '\n' && source[i + 1] == '\n')
            {
                return i;
            }
        }

        return -1;
    }
}
