using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>Правила пробелов фазы Scan.</summary>
internal static class SpaceRules
{
    // floor этим правилам не нужен — они не патчят буфер задним числом, параметр в сигнатуре ради единообразия.
    public static bool TryApply(
        ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        char c = source[index];

        // «2026год» — числу и слову нужен пробел. Слово проверяется целиком: «2026годовой»
        // трогать не за что.
        if (c == 'г' && char.IsDigit(previous) && rules.Contains(RuleId.Ru.Space.Year)
            && IsYearWord(source, index))
        {
            buffer.Write(' ');
            buffer.Write(c);
            return true;
        }

        if (c is '(' or '[')
        {
            // Пробел дописывается только после буквы или цифры: после другого пробела он
            // был бы вторым, а после открывающей скобки или тире — лишним вовсе.
            if (rules.Contains(RuleId.Common.Space.BeforeBracket) && char.IsLetterOrDigit(previous))
            {
                buffer.Write(' ');
            }

            buffer.Write(c);
            return true;
        }

        // Ниже — правила самого пробела. Сюда доходят и другие символы: диспетчер шлёт
        // в этот файл и «г» ради пробела перед словом «год», и скобки. Если для них
        // правила не сработали, символ обязан достаться диспетчеру нетронутым — иначе
        // удаление «пробела» съест букву.
        if (c != ' ')
        {
            return false;
        }

        if (rules.Contains(RuleId.Common.Space.DelRepeatSpace)
            && index + 1 < source.Length && source[index + 1] == ' ')
        {
            return true;
        }

        if (index + 1 < source.Length && ShouldDeleteBeforeNext(source, index, previous, rules))
        {
            return true;
        }

        buffer.Write(c);
        return true;
    }

    /// <summary>
    /// Пробел удаляется, потому что следующий за ним символ его не терпит: знак препинания,
    /// точка, знак процента или второй восклицательный знак подряд.
    /// </summary>
    private static bool ShouldDeleteBeforeNext(
        ReadOnlySpan<char> source, int index, char previous, RuleSet rules)
    {
        char next = source[index + 1];

        // previous != '<' — единственный барьер во всём конвейере против «текст становится
        // разметкой» (гарантия 4). Без него «< ?» и «< !--» превращались бы в «<?» и «<!--»:
        // удалённый здесь пробел — единственный, что мешало «<» слипнуться со знаком
        // препинания. После перехода фаз на документные проходы (Bind, Layout, Emit)
        // пересканируют уже этот буфер через MarkupScanner и примут псевдотег за настоящий.
        if (previous == '<')
        {
            return false;
        }

        // «8 != 9»: восклицательный знак здесь часть оператора, а не конец предложения.
        // Признак — равенство сразу за ним; знак препинания перед оператором отбивается
        // пробелом с обеих сторон, и съедать его нельзя.
        if (index + 2 < source.Length && source[index + 2] == '=' && next is '!' or '?')
        {
            return false;
        }

        // Внутри скобок пробел к их содержимому не относится: «( текст )» — это «(текст)».
        if (previous == '(' || next == ')')
        {
            return rules.Contains(RuleId.Common.Space.Bracket);
        }

        if (previous == '[' || next == ']')
        {
            return rules.Contains(RuleId.Common.Space.SquareBracket);
        }

        if (next == '!' && previous == '!')
        {
            return rules.Contains(RuleId.Common.Space.DelBetweenExclamationMarks)
                || rules.Contains(RuleId.Common.Space.DelBeforePunctuation);
        }

        if (next == '%' || next == '‰' || next == '‱')
        {
            return rules.Contains(RuleId.Common.Space.DelBeforePercent);
        }

        if (next == '.')
        {
            // Четыре точки подряд — не многоточие, а обрыв цитаты или опечатка. Пробел перед
            // ними значащий: правило многоточия эту последовательность не собирает, и
            // приклеивать её к предыдущему слову не за что.
            return !StartsLongDotRun(source, index + 1)
                && rules.Contains(RuleId.Common.Space.DelBeforeDot);
        }

        return IsPunctuation(next) && rules.Contains(RuleId.Common.Space.DelBeforePunctuation);
    }

    /// <summary>Точек подряд начиная с <paramref name="start"/> четыре или больше.</summary>
    private static bool StartsLongDotRun(ReadOnlySpan<char> source, int start)
    {
        int dots = 0;
        while (start + dots < source.Length && source[start + dots] == '.' && dots < 4)
        {
            dots++;
        }

        return dots >= 4;
    }

    /// <summary>
    /// Дописывает пробел после знака препинания, если он там нужен. Вызывается диспетчером
    /// ПОСЛЕ того, как обычный символ уже записан в буфер — это не самостоятельное правило
    /// со своим символом-триггером, а довесок к записи знака.
    /// </summary>
    public static void WriteSpaceAfterPunctuation(
        ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        // Точка с запятой закрывает HTML-сущность — «&amp;», «&lt;», «&#160;», — а не
        // предложение. Пробел после неё разорвал бы сущность и превратил разметку в текст.
        // Типографские сущности к этому месту уже раскодированы фазой Prepare, а сущности
        // разметки остаются намеренно и доходят до правил как есть.
        if (source[index] == ';' && EndsHtmlEntity(ref buffer, floor))
        {
            return;
        }

        // Пробел после многоточия и его сочетаний со знаком конца предложения: «Что?..Как».
        if (source[index] == '.' && rules.Contains(RuleId.Ru.Space.AfterHellip)
            && EndsEllipsis(ref buffer, floor)
            && index + 1 < source.Length && char.IsLetter(source[index + 1]))
        {
            buffer.Write(' ');
            return;
        }

        RuleId rule = source[index] switch
        {
            ',' => RuleId.Common.Space.AfterComma,
            ':' => RuleId.Common.Space.AfterColon,
            ';' => RuleId.Common.Space.AfterSemicolon,
            '!' => RuleId.Common.Space.AfterExclamationMark,
            '?' => RuleId.Common.Space.AfterQuestionMark,
            _ => default,
        };

        if (rule.Index != 0 && rules.Contains(rule)
            && NeedsSpaceAfterPunctuation(source, index, previous, !state.Quotes.IsEmpty))
        {
            buffer.Write(' ');
        }
    }

    /// <summary>
    /// Последний записанный символ — точка с запятой, закрывающая HTML-сущность: слева от
    /// неё имя из букв, цифр или решётки, а перед именем амперсанд. Поиск не уходит левее
    /// <paramref name="floor"/> — там чужой текстовый узел или разметка.
    /// </summary>
    private static bool EndsHtmlEntity(ref CharBuffer buffer, int floor)
    {
        // Самое длинное имя типографской сущности — «thinsp» (6 символов), самое длинное
        // числовое — «#65535» (6). Десяти хватает с запасом, а ограничение не даёт правилу
        // уползти в начало абзаца на входе вида «раз ; два».
        const int MaxEntityName = 10;

        int i = buffer.Length - 2;
        int name = 0;
        while (i >= floor && name < MaxEntityName)
        {
            char c = buffer.CharAt(i);
            if (c == '&')
            {
                return name > 0;
            }

            if (!char.IsLetterOrDigit(c) && c != '#')
            {
                return false;
            }

            name++;
            i--;
        }

        return false;
    }

    // Знак препинания — класс, а не только сырая форма во входном тексте: многоточие входит
    // сюда как готовый символ (Chars.Hellip), потому что правило hellip схлопывает "..." в
    // него ДО того, как другие правила решают, что рядом со знаком препинания.
    private static bool IsPunctuation(char c) => c is ',' or '.' or ';' or ':' or '!' or '?' or Chars.Hellip;

    /// <summary>Символы, слева от которых пробел не ставится: закрывающие скобки и кавычки.</summary>
    private static bool IsClosing(char c)
        => c is ')' or ']' or '}' or Chars.Raquo or Chars.Ldquo or Chars.Rsquo;

    private static bool NeedsSpaceAfterPunctuation(
        ReadOnlySpan<char> source, int index, char previous, bool insideQuotes)
    {
        if (index + 1 >= source.Length)
        {
            return false;
        }

        char next = source[index + 1];

        // Пробел уже есть. Проверяется КЛАСС символа, а не один только обычный пробел:
        // неразрывный пробел мог быть поставлен другим правилом на прошлом прогоне, и без
        // этого правило дописывало бы рядом ещё один — нарушая идемпотентность.
        if (char.IsWhiteSpace(next))
        {
            return false;
        }

        // Между двумя соседними знаками препинания пробела не бывает: «текст,,ещё» — за
        // первой запятой сразу вторая, вставлять пробел некуда. Сюда же попадают «?..»,
        // «!..» и «?!» — сочетания, а не два предложения подряд.
        if (IsPunctuation(next))
        {
            return false;
        }

        // Знак препинания оказался частью оператора или адреса: «8 != 9», «http://». Пробел
        // после него не ставится — это не конец предложения.
        if (next is '=' or '/')
        {
            return false;
        }

        // Слева от закрывающей скобки или кавычки пробела тоже не бывает. Простая кавычка
        // закрывающая ровно тогда, когда открыт хотя бы один уровень: вставленный перед ней
        // пробел сделал бы её открывающей, и уровни кавычек разъезжались бы до конца
        // документа — «Да,» превращалось в «Да, „».
        if (IsClosing(next) || (next is '"' or '\'' && insideQuotes))
        {
            return false;
        }

        // Цифры по обе стороны: десятичная запятая «3,14», время «10:30», счёт «2:1».
        // Разделитель внутри числа, а не знак конца предложения.
        return !(char.IsDigit(previous) && char.IsDigit(next));
    }

    /// <summary>Со слова начинается «год» в любой падежной форме.</summary>
    private static bool IsYearWord(ReadOnlySpan<char> source, int index)
    {
        int length = 0;
        while (index + length < source.Length && char.IsLetter(source[index + length]))
        {
            length++;
        }

        ReadOnlySpan<char> word = source.Slice(index, length);
        return Is(word, "год") || Is(word, "года") || Is(word, "году")
               || Is(word, "годы") || Is(word, "годов") || Is(word, "годах");
    }

    private static bool Is(ReadOnlySpan<char> word, string sample)
    {
        if (word.Length != sample.Length)
        {
            return false;
        }

        for (int i = 0; i < sample.Length; i++)
        {
            if (char.ToLowerInvariant(word[i]) != sample[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Буфер оканчивается многоточием: тремя точками, «?..», «!..» или знаком.</summary>
    private static bool EndsEllipsis(ref CharBuffer buffer, int floor)
    {
        int written = buffer.Length - floor;
        if (written < 2)
        {
            return false;
        }

        return buffer.CharAt(buffer.Length - 1) == '.' && buffer.CharAt(buffer.Length - 2) == '.';
    }
}
