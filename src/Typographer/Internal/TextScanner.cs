using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>
/// Состояние сканера, живущее сквозь ВСЕ текстовые сегменты одного документа.
/// Разметка предложение не разрывает: «а <b>б</b>» — одно предложение, поэтому стек уровней
/// кавычек и последний записанный символ обязаны переживать границу текстового сегмента.
/// Изменяемая структура: передавать только по ссылке.
/// </summary>
internal struct ScanState
{
    /// <summary>Уровни вложенности кавычек, накопленные с начала документа.</summary>
    public QuoteStack Quotes;

    /// <summary>
    /// Последний символ, записанный сканером в вывод. Ноль означает начало ДОКУМЕНТА —
    /// не начало сегмента: пустой буфер сегмента сам по себе о начале документа не говорит.
    /// </summary>
    public char Last;
}

/// <summary>Фаза Scan: посимвольное применение правил к текстовому узлу.</summary>
/// <remarks>
/// Левый контекст правила читается ИЗ БУФЕРА, а не из исходной строки: правила фазы Scan
/// пишут в буфер, и решение по исходной строке не видит правок, сделанных соседним правилом
/// на предыдущем символе. Именно из этого росли все обнаруженные разрывы идемпотентности.
/// Правый контекст, наоборот, обязан читаться из исходной строки — справа буфера ещё нет.
/// </remarks>
internal static class TextScanner
{
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref ScanState state, ref CharBuffer buffer)
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

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            char previous = buffer.Length > 0 ? buffer.CharAt(buffer.Length - 1) : state.Last;

            // Многоточие схлопывается ЗАДНИМ ЧИСЛОМ: третья подряд точка в буфере забирает
            // две предыдущие. По исходной строке это не решается — точки становятся соседями
            // уже в буфере, после того как удалён пробел между ними («текст. ..»).
            if (hellip && c == '.'
                && (i + 1 >= source.Length || source[i + 1] != '.')
                && ClosesEllipsis(ref buffer, state.Last))
            {
                buffer.Truncate(buffer.Length - 2);
                buffer.Write(Chars.Hellip);
                continue;
            }

            if (quotes && c == '"')
            {
                bool inch = char.IsDigit(previous) && state.Quotes.IsEmpty;
                if (inch)
                {
                    buffer.Write(c);
                    continue;
                }

                bool opening = previous is '\0' or ' ' or '(' or '[' or '\n' or Chars.Nbsp
                    || (state.Quotes.IsEmpty && previous == ':');
                buffer.Write(opening ? state.Quotes.Open() : state.Quotes.Close());
                continue;
            }

            if (apostrophe && c == '\'')
            {
                bool letterAfter = i + 1 < source.Length && char.IsLetter(source[i + 1]);
                buffer.Write(char.IsLetter(previous) && letterAfter ? Chars.Rsquo : c);
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
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                // Тире прямой речи: дефис в начале документа или строки, за ним пробел.
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

                // Тире между словами: пробел с обеих сторон, справа не число. Слева годится и
                // неразрывный пробел: в буфере он мог оказаться от предыдущего правила, и по
                // классу это тот же пробел — иначе тире молча не ставится.
                if (dashMain && (previous is ' ' or Chars.Nbsp) && next == ' ' && !IsNumberAhead(source, i + 2))
                {
                    // Патчится ровно та позиция, по которой принято решение. Если буфер пуст,
                    // пробел уехал в вывод с предыдущим сегментом и патчу уже недоступен.
                    if (buffer.Length > 0)
                    {
                        buffer.PatchAt(buffer.Length - 1, Chars.Nbsp);
                    }

                    buffer.Write(Chars.MDash);
                    continue;
                }

                buffer.Write(c);
                continue;
            }

            buffer.Write(c);

            if (afterComma && c == ',' && NeedsSpaceAfterComma(source, i, previous))
            {
                buffer.Write(' ');
            }
        }

        if (buffer.Length > 0)
        {
            state.Last = buffer.CharAt(buffer.Length - 1);
        }
    }

    /// <summary>
    /// Буфер оканчивается ровно двумя точками, то есть текущая точка — третья и последняя.
    /// Третий с конца символ проверяется по КЛАССУ: готовое многоточие — те же точки,
    /// иначе «……» получалось бы из шести точек за два прогона.
    /// </summary>
    /// <param name="buffer">Буфер сегмента.</param>
    /// <param name="beforeBuffer">Символ слева от буфера — последний символ прошлого сегмента.</param>
    private static bool ClosesEllipsis(ref CharBuffer buffer, char beforeBuffer)
    {
        int length = buffer.Length;
        if (length < 2 || buffer.CharAt(length - 1) != '.' || buffer.CharAt(length - 2) != '.')
        {
            return false;
        }

        char third = length >= 3 ? buffer.CharAt(length - 3) : beforeBuffer;
        return third is not ('.' or Chars.Hellip);
    }

    // Знак препинания — класс, а не только сырая форма во входном тексте: многоточие входит
    // сюда как готовый символ (Chars.Hellip), потому что правило hellip схлопывает "..." в
    // него ДО того, как другие правила решают, что рядом со знаком препинания.
    private static bool IsPunctuation(char c) => c is ',' or '.' or ';' or ':' or '!' or '?' or Chars.Hellip;

    private static bool IsNumberAhead(ReadOnlySpan<char> source, int index)
        => index < source.Length && char.IsDigit(source[index]);

    private static bool NeedsSpaceAfterComma(ReadOnlySpan<char> source, int index, char previous)
    {
        // Неразрывный пробел — уже пробел. Без этой проверки правило не узнаёт пробел,
        // ранее превращённый в nbsp другим правилом, и на повторном прогоне вставляет ещё
        // один — нарушая идемпотентность.
        if (index + 1 >= source.Length || source[index + 1] is ' ' or Chars.Nbsp)
        {
            return false;
        }

        char next = source[index + 1];

        // Между двумя соседними знаками препинания пробела не бывает: «текст,,ещё» — за
        // первой запятой сразу вторая, вставлять пробел некуда.
        if (IsPunctuation(next))
        {
            return false;
        }

        // Запятая внутри числа — десятичный разделитель, пробел не нужен.
        return !(char.IsDigit(previous) && char.IsDigit(next));
    }
}
