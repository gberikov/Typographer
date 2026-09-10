using Typographer.Rules;

namespace Typographer.Internal.Layout;

/// <summary>Состояние прохода, вставляющего разметку внутрь текстовых узлов.</summary>
internal struct InlineState
{
    /// <summary>Сколько ДОПОЛНИТЕЛЬНЫХ символов узла проглотило сработавшее правило.</summary>
    public int Skip;

    /// <summary>Разбор идёт внутри элемента ссылки: вкладывать в него ещё одну нельзя.</summary>
    public bool InsideLink;

    /// <summary>
    /// Разбор идёт внутри тега, поставленного правилом висячей пунктуации на прошлом
    /// прогоне. Повторно оборачивать тот же символ нельзя — иначе теги вкладываются друг в
    /// друга, и правило теряет идемпотентность.
    /// </summary>
    public bool InsideOptAlign;

    /// <summary>Текущая позиция — начало строки документа.</summary>
    public bool AtLineStart;

    /// <summary>
    /// Позиция в буфере вывода, левее которой забирать записанное нельзя: там лежит уже
    /// скопированная разметка. Правило висячей пунктуации забирает из буфера пробел слева от
    /// знака, и без этой границы оно съело бы байты закрывающего тега.
    /// </summary>
    public int SafeFrom;
}

/// <summary>
/// Проход фазы Layout, вставляющий разметку ВНУТРЬ текстовых узлов: автоссылки и висячая
/// пунктуация.
/// </summary>
/// <remarks>
/// Соглашение о правиле прохода: <c>TryApply(node, index, rules, ref state, ref buffer)</c>,
/// <c>true</c> означает «символ обработан и записан», <c>false</c> — «символ пишет
/// диспетчер». Правило, съевшее больше одного символа, сообщает об этом через
/// <see cref="InlineState.Skip"/>.
/// Правила прохода СОЗДАЮТ разметку из текста, поэтому все они вне
/// <see cref="RuleSet.Default"/> (гарантия 4), а сам проход не запускается, пока ни одно из
/// них не включено, — и не стоит тогда ни буфера, ни копии документа.
/// </remarks>
internal static class InlineMarkupWriter
{
    /// <summary>Хоть одно правило прохода включено.</summary>
    public static bool IsEnabled(RuleSet rules)
        => rules.Contains(RuleId.Common.Html.Url)
           || rules.Contains(RuleId.Common.Html.EMail)
           || rules.Contains(RuleId.Ru.OptAlign.Quote)
           || rules.Contains(RuleId.Ru.OptAlign.Bracket)
           || rules.Contains(RuleId.Ru.OptAlign.Comma);

    /// <summary>Проход по документу.</summary>
    /// <param name="document">Документ после фаз Scan и Bind.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="buffer">Приёмник.</param>
    public static void Run(ReadOnlySpan<char> document, RuleSet rules, ref CharBuffer buffer)
    {
        var scanner = new MarkupScanner(document);
        var state = new InlineState { AtLineStart = true };

        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = document.Slice(segment.Start, segment.Length);

            if (segment.Kind != SegmentKind.Text)
            {
                if (segment.Kind == SegmentKind.Markup)
                {
                    UpdateElementFlags(slice, ref state);
                    state.AtLineStart |= segment.IsBlock;
                }

                buffer.Write(slice);
                state.SafeFrom = buffer.Length;
                continue;
            }

            WriteNode(slice, rules, ref state, ref buffer);
        }
    }

    private static void WriteNode(
        ReadOnlySpan<char> node, RuleSet rules, ref InlineState state, ref CharBuffer buffer)
    {
        for (int i = 0; i < node.Length; i++)
        {
            if (LinkRules.TryApply(node, i, rules, ref state, ref buffer)
                || OptAlignRules.TryApply(node, i, rules, ref state, ref buffer))
            {
                i += state.Skip;
                state.Skip = 0;
                state.AtLineStart = false;
                continue;
            }

            char c = node[i];
            buffer.Write(c);
            state.AtLineStart = c == '\n';
        }
    }

    /// <summary>
    /// Отмечает вход в элемент ссылки и в тег висячей пунктуации. Закрывающий тег снимает
    /// признак: вложенных ссылок в валидном HTML не бывает, а тег висячей пунктуации
    /// содержит ровно один символ и вложить в себя ничего не может.
    /// </summary>
    private static void UpdateElementFlags(ReadOnlySpan<char> tag, ref InlineState state)
    {
        if (!Tags.TryReadName(tag, out ReadOnlySpan<char> name, out bool closing))
        {
            return;
        }

        if (Tags.NameIs(name, "a"))
        {
            state.InsideLink = !closing;
            return;
        }

        if (Tags.NameIs(name, "span"))
        {
            state.InsideOptAlign = !closing && OptAlignRules.IsOptAlignTag(tag);
        }
    }
}
