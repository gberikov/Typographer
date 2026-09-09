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

    /// <summary>
    /// Число цифр подряд в конце предыдущего текстового сегмента. Значение ограничено
    /// пятью: для проверки четырёхзначного года важно лишь наличие лишней пятой цифры.
    /// </summary>
    public int TrailingDigits;
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
    /// <summary>Длина года в цифрах — правило диапазона годов работает только с ней.</summary>
    private const int YearDigits = 4;

    /// <summary>Фаза Scan для одного текстового узла.</summary>
    /// <param name="source">Текстовый узел после фазы Prepare.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние, живущее сквозь все узлы документа.</param>
    /// <param name="buffer">Приёмник; узел дописывается в конец уже накопленного.</param>
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref ScanState state, ref CharBuffer buffer)
    {
        // Позиция, с которой начинаются записи ЭТОГО узла. Нулём её считать нельзя: буфер
        // общий на документ, и левее floor лежат чужие символы — прошлые узлы и разметка.
        // Отсюда два запрета. Левый контекст первого символа узла берётся из state.Last, а не
        // из буфера: там стоит «>» закрывающей скобки тега, которого сканер не видел. И ни
        // PatchAt, ни Truncate не имеют права уйти левее floor — патч переписал бы символ
        // тега, а гарантия 3 обещает разметку байт в байт.
        int floor = buffer.Length;

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
            char previous = buffer.Length > floor ? buffer.CharAt(buffer.Length - 1) : state.Last;

            // Многоточие схлопывается ЗАДНИМ ЧИСЛОМ: третья подряд точка в буфере забирает
            // две предыдущие. По исходной строке это не решается — точки становятся соседями
            // уже в буфере, после того как удалён пробел между ними («текст. ..»).
            if (hellip && c == '.'
                && (i + 1 >= source.Length || source[i + 1] != '.')
                && ClosesEllipsis(ref buffer, floor, state.Last))
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

                bool opening = IsOpeningContext(previous)
                    || (state.Quotes.IsEmpty && previous == ':');
                buffer.Write(opening ? state.Quotes.Open() : state.Quotes.Close());
                continue;
            }

            // Готовая типографская кавычка обязана двигать тот же счётчик уровней. Без
            // этого простая кавычка внутри неё открывает уровень 0 повторно.
            if (quotes && IsReadyOpeningQuote(c))
            {
                state.Quotes.Open();
                buffer.Write(c);
                continue;
            }

            if (quotes && IsReadyClosingQuote(c))
            {
                // Правая одинарная кавычка одновременно служит апострофом. Между буквами
                // она не закрывает уровень: «д’Артаньян и "цитата"» должен сохранить
                // внешний уровень для вложенных двойных кавычек.
                bool apostropheBetweenLetters = c == Chars.Rsquo
                    && char.IsLetter(previous)
                    && i + 1 < source.Length
                    && char.IsLetter(source[i + 1]);
                if (!apostropheBetweenLetters)
                {
                    state.Quotes.Close();
                }

                buffer.Write(c);
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

                // Диапазон годов: ровно по четыре цифры с каждой стороны и ни одной лишней
                // цифры рядом. Без счёта цифр правило срабатывало на любой паре «цифра —
                // дефис — цифра» и рвало телефоны (+7-999-123-45-67), даты (01-01-2020) и
                // пути в ссылках (/2024-01-15/).
                if (dashYears && IsYearBefore(ref buffer, floor, state.TrailingDigits) && IsYearAfter(source, i + 1))
                {
                    buffer.Write(Chars.MDash);
                    continue;
                }

                // Тире между словами: пробел с обеих сторон, справа не число. Слева годится и
                // неразрывный пробел: в буфере он мог оказаться от предыдущего правила, и по
                // классу это тот же пробел — иначе тире молча не ставится.
                if (dashMain && (previous is ' ' or Chars.Nbsp) && next == ' ' && !IsNumberAhead(source, i + 2))
                {
                    // Патчится ровно та позиция, по которой принято решение. Если сегмент
                    // пуст, пробел уехал в вывод с предыдущим сегментом и патчу недоступен.
                    if (buffer.Length > floor)
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

            if (afterComma && c == ',' && NeedsSpaceAfterComma(source, i, previous, !state.Quotes.IsEmpty))
            {
                buffer.Write(' ');
            }
        }

        if (buffer.Length > floor)
        {
            state.Last = buffer.CharAt(buffer.Length - 1);
            state.TrailingDigits = CountTrailingDigits(ref buffer, floor, state.TrailingDigits);
        }
    }

    /// <summary>
    /// Фаза Scan по документу: правила применяются к текстовым узлам, разметка копируется.
    /// </summary>
    /// <param name="html">Документ после фазы Prepare.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="buffer">Приёмник.</param>
    /// <returns>Можно ли оборачивать документ в абзацы: нет блочной и незакрытой разметки.</returns>
    public static bool RunDocument(ReadOnlySpan<char> html, RuleSet rules, ref CharBuffer buffer)
    {
        var scanner = new MarkupScanner(html);

        // Состояние сканера создаётся ОДИН раз на документ и протягивается через все
        // текстовые сегменты: тег внутри предложения не должен обнулять разбор кавычек
        // и не должен выглядеть для правил как начало строки.
        var state = new ScanState();
        bool hasBlockMarkup = false;

        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = html.Slice(segment.Start, segment.Length);
            if (segment.Kind != SegmentKind.Text)
            {
                // Блочный тег разрывает предложение: за </p> начинается новая строка, и
                // правила обязаны видеть её начало, а не последний символ прошлого абзаца.
                if (segment.IsBlock)
                {
                    state.Last = '\n';
                    state.TrailingDigits = 0;
                }
                else if (segment.Kind == SegmentKind.Protected && segment.Length > 0)
                {
                    // Содержимое защищённой зоны не анализируется и потому не может
                    // прозрачно соединять числовой контекст по обе стороны от неё.
                    state.TrailingDigits = 0;
                }

                hasBlockMarkup |= segment.PreventsParagraphWrapping;
                buffer.Write(slice);
                continue;
            }

            Run(slice, rules, ref state, ref buffer);
        }

        return !hasBlockMarkup && !scanner.HasUnclosedMarkup;
    }

    /// <summary>
    /// Буфер оканчивается ровно двумя точками, то есть текущая точка — третья и последняя.
    /// Третий с конца символ проверяется по КЛАССУ: готовое многоточие — те же точки,
    /// иначе «……» получалось бы из шести точек за два прогона.
    /// </summary>
    /// <param name="buffer">Буфер документа.</param>
    /// <param name="floor">Позиция в буфере, с которой начинается текущий сегмент.</param>
    /// <param name="beforeBuffer">Символ слева от сегмента — последний символ прошлого сегмента.</param>
    private static bool ClosesEllipsis(ref CharBuffer buffer, int floor, char beforeBuffer)
    {
        int written = buffer.Length - floor;
        if (written < 2 || buffer.CharAt(buffer.Length - 1) != '.' || buffer.CharAt(buffer.Length - 2) != '.')
        {
            return false;
        }

        char third = written >= 3 ? buffer.CharAt(buffer.Length - 3) : beforeBuffer;
        return third is not ('.' or Chars.Hellip);
    }

    // Знак препинания — класс, а не только сырая форма во входном тексте: многоточие входит
    // сюда как готовый символ (Chars.Hellip), потому что правило hellip схлопывает "..." в
    // него ДО того, как другие правила решают, что рядом со знаком препинания.
    private static bool IsPunctuation(char c) => c is ',' or '.' or ';' or ':' or '!' or '?' or Chars.Hellip;

    /// <summary>
    /// Первый непробельный символ справа — цифра.
    /// </summary>
    /// <remarks>
    /// Пробелы пропускаются, а не проверяется одна фиксированная позиция. Правило «удалить
    /// повторный пробел» схлопнет их позже, и решение по дефису обязано быть одинаковым для
    /// «текст - 5» и «текст -  5»: иначе лишний пробел во входе молча превращал минус перед
    /// числом в тире.
    /// </remarks>
    private static bool IsNumberAhead(ReadOnlySpan<char> source, int index)
    {
        while (index < source.Length && source[index] == ' ')
        {
            index++;
        }

        return index < source.Length && char.IsDigit(source[index]);
    }

    /// <summary>
    /// Кавычка открывающая, если слева начало документа, пробел любого вида (обычный,
    /// неразрывный, табуляция, перевод строки, возврат каретки) или открывающая скобка
    /// либо открывающая кавычка внешнего уровня.
    /// </summary>
    private static bool IsOpeningContext(char previous)
        => previous is '\0' or '(' or '[' or '{' or Chars.Laquo or Chars.Bdquo or Chars.Lsquo
           || char.IsWhiteSpace(previous);

    private static bool IsReadyOpeningQuote(char c)
        => c is Chars.Laquo or Chars.Bdquo or Chars.Lsquo;

    private static bool IsReadyClosingQuote(char c)
        => c is Chars.Raquo or Chars.Ldquo or Chars.Rsquo;

    /// <summary>Символы, слева от которых пробел не ставится: закрывающие скобки и кавычки.</summary>
    private static bool IsClosing(char c)
        => c is ')' or ']' or '}' or Chars.Raquo or Chars.Ldquo or Chars.Rsquo;

    private static bool NeedsSpaceAfterComma(
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
        // первой запятой сразу вторая, вставлять пробел некуда.
        if (IsPunctuation(next))
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

        // Запятая внутри числа — десятичный разделитель, пробел не нужен.
        return !(char.IsDigit(previous) && char.IsDigit(next));
    }

    /// <summary>Четыре цифры подряд перед дефисом и ни одной пятой — это год.</summary>
    private static bool IsYearBefore(ref CharBuffer buffer, int floor, int beforeBuffer)
    {
        int written = buffer.Length - floor;
        int digits = 0;
        while (digits < written && digits <= YearDigits
               && char.IsDigit(buffer.CharAt(buffer.Length - digits - 1)))
        {
            digits++;
        }

        if (digits == written)
        {
            digits = Math.Min(YearDigits + 1, digits + beforeBuffer);
        }

        return digits == YearDigits;
    }

    private static int CountTrailingDigits(ref CharBuffer buffer, int floor, int beforeBuffer)
    {
        int written = buffer.Length - floor;
        int digits = 0;
        while (digits < written && digits <= YearDigits
               && char.IsDigit(buffer.CharAt(buffer.Length - digits - 1)))
        {
            digits++;
        }

        return digits == written
            ? Math.Min(YearDigits + 1, digits + beforeBuffer)
            : Math.Min(YearDigits + 1, digits);
    }

    /// <summary>Четыре цифры подряд после дефиса и ни одной пятой — это год.</summary>
    private static bool IsYearAfter(ReadOnlySpan<char> source, int start)
    {
        if (start + YearDigits > source.Length)
        {
            return false;
        }

        for (int k = 0; k < YearDigits; k++)
        {
            if (!char.IsDigit(source[start + k]))
            {
                return false;
            }
        }

        return start + YearDigits == source.Length || !char.IsDigit(source[start + YearDigits]);
    }
}
