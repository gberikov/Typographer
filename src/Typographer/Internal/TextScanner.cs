using Typographer.Internal.Scan;
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
    /// Сколько ДОПОЛНИТЕЛЬНЫХ символов исходной строки проглотило последнее сработавшее
    /// правило. Правило, собравшее знак из нескольких символов («(c)», «-&gt;», «!=»),
    /// выставляет это поле, а диспетчер на столько же двигает курсор. Обнуляется сразу
    /// после сдвига: значение живёт ровно один шаг цикла.
    /// </summary>
    public int Skip;

    /// <summary>
    /// Число цифр подряд в конце предыдущего текстового сегмента. Значение ограничено
    /// пятью: для проверки четырёхзначного года важно лишь наличие лишней пятой цифры.
    /// </summary>
    public int TrailingDigits;

    /// <summary>
    /// Курсор внутри веб-адреса: слева был «://», справа ещё не встретился пробел.
    /// Знаки препинания внутри адреса — его часть, а не конец предложения, и пробел
    /// после них ставить нельзя: «a-b?x=1» превращалось в «a-b? x=1».
    /// </summary>
    public bool InsideUrl;
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

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            char previous = buffer.Length > floor ? buffer.CharAt(buffer.Length - 1) : state.Last;

            // Границы веб-адреса. Признак начала — «://», признак конца — пробельный
            // символ или угловая скобка. Флаг вместо взгляда назад по буферу: искать
            // «://» от каждого знака препинания до ближайшего пробела — это O(n²) на
            // входе без пробелов, а гарантия 1 обещает один линейный проход.
            if (state.InsideUrl)
            {
                if (char.IsWhiteSpace(c) || c is '<' or '>' or '"')
                {
                    state.InsideUrl = false;
                }
            }
            else if (c == ':' && i + 2 < source.Length && source[i + 1] == '/' && source[i + 2] == '/')
            {
                state.InsideUrl = true;
            }

            // switch по символу-триггеру: компилятор строит по нему таблицу переходов, и
            // цена диспетчеризации не растёт с числом правил. Порядок правил на один и тот
            // же символ задан порядком вызовов внутри ветки — в одном месте и явно.
            bool handled = c switch
            {
                // Точка спорна: между цифрами это десятичный разделитель, иначе — знак
                // препинания или часть многоточия.
                '.' => NumberRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer)
                    || PunctuationRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                ',' or ';' or ':' => PunctuationRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                'г' => SpaceRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                // «!=» — знак сравнения, а не конец предложения: число спрашивается первым.
                '!' => NumberRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer)
                    || PunctuationRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                '?' or Chars.Hellip
                    => PunctuationRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                'C' or 'F' or Chars.Numero
                    => SymbolRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                // Угловая скобка спорна между стрелкой и знаком сравнения: «<-» и «<=».
                '<' => SymbolRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer)
                    || NumberRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                '+' or '~' or 'x' or '1' or '3'
                    => NumberRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                '0' or '2' or '4' or '5' or '6' or '7' or '8' or '9'
                    => NumberRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                '"' or '\'' or Chars.Laquo or Chars.Raquo or Chars.Bdquo or Chars.Ldquo
                    or Chars.Lsquo or Chars.Rsquo
                    => QuoteRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                // Пробел спорен: между «кое» и «что» он должен стать дефисом. Правило
                // частиц спрашивается первым и почти всегда отказывается.
                ' ' => DashRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer)
                    || PunctuationRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer)
                    || SpaceRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                '[' => SpaceRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                // Скобка и дефис спорны: «(c)» — знак, а не скобка со словом; «->» — стрелка,
                // а не тире. Правило символов спрашивается первым, и только если оно не
                // узнало свой образец, символ достаётся правилу пробелов или тире.
                '(' => SymbolRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer)
                    || SpaceRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                // Дефис спорен трижды: стрелка «->», наращение «25-й» и тире.
                '-' => SymbolRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer)
                    || NumberRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer)
                    || DashRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                '>' => NumberRules.TryApply(source, i, previous, floor, rules, ref state, ref buffer),
                _ => false,
            };

            if (handled)
            {
                // Правило могло проглотить не один символ, а несколько: «(c)» — три,
                // «->» — два. Курсор двигается на съеденное, чтобы хвост знака не попал
                // в буфер отдельными буквами.
                i += state.Skip;
                state.Skip = 0;
                continue;
            }

            buffer.Write(c);
            SpaceRules.WriteSpaceAfterPunctuation(source, i, previous, floor, rules, ref state, ref buffer);
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

                // Тег обрывает веб-адрес: «http://a<b>?x» — это не адрес с вопросом.
                state.InsideUrl = false;

                hasBlockMarkup |= segment.PreventsParagraphWrapping;
                buffer.Write(slice);
                continue;
            }

            Run(slice, rules, ref state, ref buffer);
        }

        return !hasBlockMarkup && !scanner.HasUnclosedMarkup;
    }

    /// <summary>Число цифр подряд в конце текстового узла — учёт состояния, а не правило.</summary>
    private static int CountTrailingDigits(ref CharBuffer buffer, int floor, int beforeBuffer)
    {
        int written = buffer.Length - floor;
        int digits = 0;
        while (digits < written && digits <= DashRules.YearDigits
               && char.IsDigit(buffer.CharAt(buffer.Length - digits - 1)))
        {
            digits++;
        }

        return digits == written
            ? Math.Min(DashRules.YearDigits + 1, digits + beforeBuffer)
            : Math.Min(DashRules.YearDigits + 1, digits);
    }
}
