using Typographer.Internal.Bind;
using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>Вид токена: из чего он состоит.</summary>
internal enum TokenKind
{
    /// <summary>Только буквы, возможно с точками и дефисами: «дом», «т.», «из-за».</summary>
    Word,

    /// <summary>Только цифры, возможно с точками и дефисами: «2026», «2018-10-10».</summary>
    Number,

    /// <summary>И буквы, и цифры: «5-й», «м2», «A4».</summary>
    Mixed,
}

/// <summary>Состояние фазы Bind, живущее сквозь текстовые сегменты документа.</summary>
internal struct BindState
{
    /// <summary>Позиция начала текущего токена в буфере ВЫВОДА.</summary>
    public int TokenStart;

    /// <summary>Сколько символов текущего токена накоплено в стековом буфере.</summary>
    public int TokenLength;

    /// <summary>Токен длиннее стекового буфера: словарная проверка ему заведомо не нужна.</summary>
    public bool TokenOverflow;

    /// <summary>В текущем токене есть буква.</summary>
    public bool HasLetter;

    /// <summary>В текущем токене есть цифра.</summary>
    public bool HasDigit;

    /// <summary>Позиция пробела перед текущим токеном в буфере вывода, -1 — связывать нечего.</summary>
    public int SpaceIndex;

    /// <summary>Длина предыдущего токена.</summary>
    public int PrevLength;

    /// <summary>Позиция начала предыдущего токена в буфере вывода.</summary>
    public int PrevTokenStart;

    /// <summary>Позиция пробела перед ПРЕДЫДУЩИМ токеном, -1 — связывать нечего.</summary>
    public int PrevSpaceIndex;

    /// <summary>Вид предыдущего токена: правила «число и слово» смотрят именно на него.</summary>
    public TokenKind PrevKind;

    /// <summary>
    /// Позиция в буфере вывода, левее которой переписывать нельзя: там лежит уже
    /// скопированная разметка или то, что записало символьное правило.
    /// </summary>
    public int SafeFrom;

    /// <summary>Пробел, закрывающий текущий токен, писать неразрывным.</summary>
    public bool GlueForward;

    /// <summary>
    /// Разбор идёт внутри элемента nobr или nowrap. Тег там уже запрещает перенос, и
    /// неразрывный пробел не нужен: он лишь оставляет невидимый символ на месте пробела.
    /// Признак живёт по сегментам документа, а не по токенам, и <see cref="Reset"/> его
    /// не трогает.
    /// </summary>
    public bool NoWrap;

    /// <summary>Сколько ДОПОЛНИТЕЛЬНЫХ символов документа проглотило символьное правило.</summary>
    public int Skip;

    /// <summary>Создаёт состояние в начале документа: токена нет, связывать нечего.</summary>
    public BindState()
    {
        TokenStart = 0;
        TokenLength = 0;
        TokenOverflow = false;
        HasLetter = false;
        HasDigit = false;
        SpaceIndex = -1;
        PrevLength = 0;
        PrevTokenStart = 0;
        PrevSpaceIndex = -1;
        PrevKind = TokenKind.Word;
        SafeFrom = 0;
        GlueForward = false;
        NoWrap = false;
        Skip = 0;
    }

    /// <summary>Из чего состоит текущий токен.</summary>
    public readonly TokenKind Kind => HasLetter
        ? HasDigit ? TokenKind.Mixed : TokenKind.Word
        : TokenKind.Number;

    /// <summary>Граница, за которой ни токен, ни пробел перед ним не продолжаются.</summary>
    public void Reset()
    {
        ResetToken();
        SpaceIndex = -1;
        PrevLength = 0;
        PrevTokenStart = 0;
        PrevSpaceIndex = -1;
        PrevKind = TokenKind.Word;
        GlueForward = false;
    }

    /// <summary>Токен закрыт: накопитель пуст, предыдущий токен остаётся.</summary>
    public void ResetToken()
    {
        TokenLength = 0;
        TokenOverflow = false;
        HasLetter = false;
        HasDigit = false;
    }
}

/// <summary>Фаза Bind: словарный проход по токенам.</summary>
/// <remarks>
/// Фаза ПИШЕТ документ, а не патчит его на месте: одиннадцать её правил меняют длину текста
/// («г.г.» — «гг.», «1 руб.» — «1 ₽», «$100» — «100 $»), и патчем это не выражается.
/// Четвёртого буфера на документ при этом нет: буфер фазы Prepare после фазы Scan мёртв, и
/// вызывающий код отдаёт его сюда под приёмник.
/// Проход держит окно из ДВУХ токенов. Токен — последовательность букв, цифр, точек и
/// дефисов; точка и дефис токен не НАЧИНАЮТ, иначе «.дом» перестаёт быть коротким словом
/// «дом», а «-5» становится словом. Токены копятся в два стековых буфера, которые на каждой
/// границе меняются местами — копирования нет, меняются местами два спана.
/// Точек входа две — <see cref="Run"/> для обычного текста и <see cref="RunDocument"/> для
/// HTML, — но разбор у них общий, в <see cref="BindSegment"/>. Пока разбор жил в двух телах,
/// они разъезжались.
/// </remarks>
internal static class WordBinder
{
    /// <summary>
    /// Максимальная длина токена. Самое длинное словарное слово — «понедельник»
    /// (11 символов), самое длинное сокращение — «ПБОЮЛ». Тридцать два взяты с запасом:
    /// всё длиннее не короткое слово, не сокращение и не дата, и накопление прекращается.
    /// </summary>
    private const int MaxToken = 32;

    /// <summary>Хоть одно правило фазы включено, и проход имеет смысл запускать.</summary>
    public static bool IsEnabled(RuleSet rules) => rules.Overlaps(RuleSet.BindPhase);

    /// <summary>Фаза Bind по обычному тексту: весь вход — один текстовый сегмент.</summary>
    /// <param name="source">Текст после фазы Scan.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="buffer">Приёмник.</param>
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)
    {
        Span<char> token = stackalloc char[MaxToken];
        Span<char> previous = stackalloc char[MaxToken];
        var state = new BindState();

        BindSegment(source, 0, source.Length, rules, ref token, ref previous, ref state, ref buffer);
        FlushToken('\0', rules, ref token, ref previous, ref state, ref buffer);
    }

    /// <summary>
    /// Фаза Bind по документу: слово, разорванное СТРОЧНЫМ тегом, остаётся одним словом.
    /// </summary>
    /// <remarks>
    /// «&lt;b&gt;сло&lt;/b&gt;во» — одно слово, и словарные правила обязаны видеть его целиком;
    /// иначе «во» выглядит коротким словом и получает неразрывный пробел, которого в тексте нет.
    /// Блочный тег и защищённая зона слово, наоборот, завершают: за ними начинается новая строка
    /// или содержимое, которого фаза не касается.
    /// </remarks>
    /// <param name="document">Документ после фазы Scan.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="buffer">Приёмник.</param>
    public static void RunDocument(ReadOnlySpan<char> document, RuleSet rules, ref CharBuffer buffer)
    {
        Span<char> token = stackalloc char[MaxToken];
        Span<char> previous = stackalloc char[MaxToken];
        var state = new BindState();
        var scanner = new MarkupScanner(document);
        bool nowrapRule = rules.Contains(RuleId.Common.Nbsp.Nowrap);
        int nowrapDepth = 0;

        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = document.Slice(segment.Start, segment.Length);

            if (segment.Kind == SegmentKind.Markup && IsNoWrapTag(slice, ref nowrapDepth))
            {
                // Тег nobr завершает слово, как блочный: он граница ЗОНЫ, а решение о
                // неразрывном пробеле принимается по зоне. Иначе слово, начатое внутри
                // nobr, уносило бы это решение за закрывающий тег.
                FlushToken('\0', rules, ref token, ref previous, ref state, ref buffer);
                state.NoWrap = nowrapRule && nowrapDepth > 0;
                buffer.Write(slice);
                state.Reset();
                state.SafeFrom = buffer.Length;
                continue;
            }

            if (segment.Kind == SegmentKind.Markup && !segment.IsBlock)
            {
                // Строчный тег для слова прозрачен: ни буквы, ни границы он не даёт. Но
                // байты тега уже в выводе между частями слова — переписывать токен через
                // них нельзя, и SafeFrom это запрещает.
                buffer.Write(slice);
                state.SafeFrom = buffer.Length;
                continue;
            }

            if (segment.Kind != SegmentKind.Text)
            {
                FlushToken('\0', rules, ref token, ref previous, ref state, ref buffer);
                buffer.Write(slice);
                state.Reset();
                state.SafeFrom = buffer.Length;
                continue;
            }

            BindSegment(
                document, segment.Start, segment.Start + segment.Length,
                rules, ref token, ref previous, ref state, ref buffer);
        }

        FlushToken('\0', rules, ref token, ref previous, ref state, ref buffer);
    }

    /// <summary>
    /// Разбирает один текстовый сегмент, продолжая токен, начатый в предыдущем.
    /// </summary>
    private static void BindSegment(
        ReadOnlySpan<char> document, int start, int end, RuleSet rules,
        ref Span<char> token, ref Span<char> previous, ref BindState state, ref CharBuffer buffer)
    {
        for (int i = start; i < end; i++)
        {
            char c = document[i];

            if (IsTokenChar(c) && (state.TokenLength > 0 || c is not ('.' or '-')))
            {
                // Телефон начинается цифрой и читает вперёд через пробелы и скобки —
                // токеном он не выражается, поэтому спрашивается на первом символе токена.
                if (state.TokenLength == 0
                    && PhoneRules.TryApply(document, i, end, rules, ref state, ref buffer))
                {
                    i += state.Skip;
                    state.Skip = 0;
                    state.Reset();
                    state.SafeFrom = buffer.Length;
                    continue;
                }

                if (state.TokenLength == 0)
                {
                    state.TokenStart = buffer.Length;
                }

                if (state.TokenLength < token.Length)
                {
                    token[state.TokenLength++] = c;
                    state.HasLetter |= char.IsLetter(c);
                    state.HasDigit |= char.IsDigit(c);
                }
                else
                {
                    state.TokenOverflow = true;
                }

                buffer.Write(c);
                continue;
            }

            bool flushed = FlushToken(c, rules, ref token, ref previous, ref state, ref buffer);

            // Символьные правила: они пишут сами и сообщают, сколько символов документа
            // проглотили. То, что они записали, для токенного окна непрозрачно — окно
            // сбрасывается, а SafeFrom запрещает переписывать записанное.
            if (MarkRules.TryApply(document, i, end, rules, ref state, ref buffer)
                || MoneyRules.TryApply(document, i, end, rules, ref state, ref buffer)
                || PhoneRules.TryApply(document, i, end, rules, ref state, ref buffer))
            {
                i += state.Skip;
                state.Skip = 0;
                state.Reset();
                state.SafeFrom = buffer.Length;
                continue;
            }

            if (c is ' ' or Chars.Nbsp)
            {
                // Позиция пробела запоминается ДО записи: правило-склейка следующего токена
                // патчит именно её. Пробел, слева от которого токена не было, кандидатом не
                // становится: «дом . А.» — связывать пробел после одинокой точки не с чем.
                state.SpaceIndex = flushed ? buffer.Length : -1;
                buffer.Write(state.NoWrap ? ' ' : state.GlueForward ? Chars.Nbsp : c);
                state.GlueForward = false;
                continue;
            }

            buffer.Write(c);
            state.SpaceIndex = -1;
            state.GlueForward = false;
        }
    }

    /// <summary>
    /// Токен закрыт символом <paramref name="boundary"/> (ноль — концом сегмента или
    /// документа): спрашиваются правила, затем окно сдвигается на один токен.
    /// </summary>
    /// <returns>Токен был и правила его видели.</returns>
    private static bool FlushToken(
        char boundary, RuleSet rules,
        ref Span<char> token, ref Span<char> previous, ref BindState state, ref CharBuffer buffer)
    {
        if (state.TokenLength == 0)
        {
            return false;
        }

        // Перезаписи спрашиваются ПЕРВЫМИ: склейка обязана видеть уже исправленный токен.
        // «1990 г.г.» — сперва «гг.», и только потом решение о неразрывном пробеле перед ним.
        RewriteRules.TryRewrite(
            token, previous.Slice(0, state.PrevLength), boundary, rules, ref state, ref buffer);

        MoneyRules.TryRewrite(token, rules, ref state, ref buffer);

        DateRules.TryRewrite(
            token, previous.Slice(0, state.PrevLength), boundary, rules, ref state, ref buffer);

        NbspRules.TryGlue(
            token.Slice(0, state.TokenLength), previous.Slice(0, state.PrevLength),
            boundary, rules, ref state, ref buffer);

        // Окно сдвигается сменой спанов, а не копированием: спан — два машинных слова.
        Span<char> finished = token;
        token = previous;
        previous = finished;
        state.PrevLength = state.TokenLength;
        state.PrevTokenStart = state.TokenStart;
        state.PrevSpaceIndex = state.SpaceIndex;
        state.PrevKind = state.Kind;
        state.ResetToken();
        return true;
    }

    /// <summary>
    /// Тег открывает или закрывает элемент nobr или nowrap; вложенность при этом
    /// пересчитывается. Внутри такого элемента правила неразрывного пробела не работают, а
    /// уже стоящие неразрывные пробелы становятся обычными: перенос запрещён самим тегом.
    /// </summary>
    private static bool IsNoWrapTag(ReadOnlySpan<char> tag, ref int depth)
    {
        if (!Tags.TryReadName(tag, out ReadOnlySpan<char> name, out bool closing)
            || !(Tags.NameIs(name, "nobr") || Tags.NameIs(name, "nowrap")))
        {
            return false;
        }

        depth = closing ? Math.Max(0, depth - 1) : depth + 1;
        return true;
    }

    /// <summary>
    /// Символ продолжает токен. Точка и дефис входят, потому что живут ВНУТРИ токена —
    /// «т. д.», «из-за», «2018-10-10», — но токен не начинают: это решает вызывающий код.
    /// </summary>
    private static bool IsTokenChar(char c)
        => char.IsLetter(c) || char.IsDigit(c) || c is '.' or '-';
}
