using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>Фаза Scan: посимвольное применение правил к текстовому узлу.</summary>
internal static class TextScanner
{
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)
    {
        bool delRepeatSpace = rules.Contains(RuleId.Common.Space.DelRepeatSpace);
        bool delBeforePunctuation = rules.Contains(RuleId.Common.Space.DelBeforePunctuation);
        bool afterComma = rules.Contains(RuleId.Common.Space.AfterComma);
        bool hellip = rules.Contains(RuleId.Common.Punctuation.Hellip);

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];

            if (hellip && c == '.' && IsExactlyThreeDots(source, i))
            {
                buffer.Write(Chars.Hellip);
                i += 2;
                continue;
            }

            if (c == ' ')
            {
                if (delRepeatSpace && i + 1 < source.Length && source[i + 1] == ' ')
                {
                    continue;
                }

                if (delBeforePunctuation && i + 1 < source.Length && IsPunctuation(source[i + 1]))
                {
                    continue;
                }

                buffer.Write(c);
                continue;
            }

            buffer.Write(c);

            if (afterComma && c == ',' && NeedsSpaceAfterComma(source, i))
            {
                buffer.Write(' ');
            }
        }
    }

    private static bool IsPunctuation(char c) => c is ',' or '.' or ';' or ':' or '!' or '?';

    private static bool NeedsSpaceAfterComma(ReadOnlySpan<char> source, int index)
    {
        if (index + 1 >= source.Length || source[index + 1] == ' ')
        {
            return false;
        }

        // Запятая внутри числа — десятичный разделитель, пробел не нужен.
        bool digitBefore = index > 0 && char.IsDigit(source[index - 1]);
        bool digitAfter = char.IsDigit(source[index + 1]);
        return !(digitBefore && digitAfter);
    }

    private static bool IsExactlyThreeDots(ReadOnlySpan<char> source, int index)
    {
        if (index + 2 >= source.Length || source[index + 1] != '.' || source[index + 2] != '.')
        {
            return false;
        }

        bool dotBefore = index > 0 && source[index - 1] == '.';
        bool dotAfter = index + 3 < source.Length && source[index + 3] == '.';
        return !dotBefore && !dotAfter;
    }
}
