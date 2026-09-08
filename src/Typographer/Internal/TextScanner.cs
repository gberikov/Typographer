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
        bool quotes = rules.Contains(RuleId.Common.Punctuation.Quote);
        bool apostrophe = rules.Contains(RuleId.Common.Punctuation.Apostrophe);
        bool dashMain = rules.Contains(RuleId.Ru.Dash.Main);
        bool directSpeech = rules.Contains(RuleId.Ru.Dash.DirectSpeech);
        bool dashYears = rules.Contains(RuleId.Ru.Dash.Years);
        var quoteStack = new QuoteStack();

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];

            if (hellip && c == '.' && IsExactlyThreeDots(source, i))
            {
                buffer.Write(Chars.Hellip);
                i += 2;
                continue;
            }

            if (quotes && c == '"')
            {
                char previous = i > 0 ? source[i - 1] : '\0';
                bool inch = char.IsDigit(previous) && quoteStack.IsEmpty;
                if (inch)
                {
                    buffer.Write(c);
                    continue;
                }

                bool opening = previous is '\0' or ' ' or '(' or '[' or '\n' or Chars.Nbsp
                    || (quoteStack.IsEmpty && previous == ':');
                buffer.Write(opening ? quoteStack.Open() : quoteStack.Close());
                continue;
            }

            if (apostrophe && c == '\'')
            {
                bool letterBefore = i > 0 && char.IsLetter(source[i - 1]);
                bool letterAfter = i + 1 < source.Length && char.IsLetter(source[i + 1]);
                buffer.Write(letterBefore && letterAfter ? Chars.Rsquo : c);
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

            if (c == '-')
            {
                char previous = i > 0 ? source[i - 1] : '\0';
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                // Тире прямой речи: дефис в начале текста или строки, за ним пробел.
                if (directSpeech && next == ' ' && previous is '\0' or '\n')
                {
                    buffer.Write(Chars.MDash);
                    continue;
                }

                // Диапазон чисел: цифра с обеих сторон, без пробелов.
                if (dashYears && char.IsDigit(previous) && char.IsDigit(next))
                {
                    buffer.Write(Chars.MDash);
                    continue;
                }

                // Тире между словами: пробел с обеих сторон, справа не число.
                if (dashMain && previous == ' ' && next == ' ' && !IsNumberAhead(source, i + 2))
                {
                    buffer.PatchAt(buffer.Length - 1, Chars.Nbsp);
                    buffer.Write(Chars.MDash);
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

    // Знак препинания — класс, а не только сырая форма во входном тексте: многоточие входит
    // сюда как готовый символ (Chars.Hellip), потому что правило hellip схлопывает "..." в
    // него ДО того, как другие правила решают, что рядом со знаком препинания. Без этого
    // предикат путает "raw ..." и уже свёрнутый "…" — решения о пробеле расходятся между
    // первым прогоном (видит сырые точки) и вторым (видит готовое многоточие).
    private static bool IsPunctuation(char c) => c is ',' or '.' or ';' or ':' or '!' or '?' or Chars.Hellip;

    private static bool IsNumberAhead(ReadOnlySpan<char> source, int index)
        => index < source.Length && char.IsDigit(source[index]);

    private static bool NeedsSpaceAfterComma(ReadOnlySpan<char> source, int index)
    {
        // Неразрывный пробел — уже пробел. Без этой проверки правило не узнаёт пробел,
        // ранее превращённый в nbsp другим правилом (например, тире прямой речи после
        // запятой), и на повторном прогоне вставляет ещё один — нарушая идемпотентность.
        if (index + 1 >= source.Length || source[index + 1] is ' ' or Chars.Nbsp)
        {
            return false;
        }

        char next = source[index + 1];

        // Между двумя соседними знаками препинания пробела не бывает: "текст,,ещё" — за
        // первой запятой сразу вторая, вставлять пробел некуда. Без этой проверки решение
        // принимается только по второй запятой (пробел за ней и правда нужен), а пробел,
        // который поставило бы это же правило перед первой, дописывается прямо в буфер
        // мимо основного цикла — и правило удаления пробела перед пунктуацией его не видит.
        // На повторном прогоне тот пробел уже часть входа и благополучно удаляется, из-за
        // чего результат первого и второго прогона расходятся.
        if (IsPunctuation(next))
        {
            return false;
        }

        // Запятая внутри числа — десятичный разделитель, пробел не нужен.
        bool digitBefore = index > 0 && char.IsDigit(source[index - 1]);
        bool digitAfter = char.IsDigit(next);
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
