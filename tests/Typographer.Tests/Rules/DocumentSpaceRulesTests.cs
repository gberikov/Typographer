using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class DocumentSpaceRulesTests
{
    private static string Run(string html, params RuleId[] rules)
        => new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None.With(rules) }).Process(html);

    private static string Text(string text, params RuleId[] rules)
        => new TextTypograf(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(text);

    [Fact]
    public void CollapsesRepeatedLineBreaks()
    {
        // Пустая строка между абзацами остаётся: схлопывается третий перевод и дальше.
        Assert.Equal("текст\n\nдалее", Run("текст\n\n\n\nдалее", RuleId.Common.Space.DelRepeatN));
        Assert.Equal("текст\n\nдалее", Run("текст\n\nдалее", RuleId.Common.Space.DelRepeatN));
    }

    [Fact]
    public void TrimsDocumentEdgesOnlyWhenAsked()
    {
        Assert.Equal("  текст  ", Run("  текст  ", RuleId.Common.Punctuation.Quote));
        Assert.Equal(
            "текст",
            Run("  текст  ", RuleId.Common.Space.TrimLeft, RuleId.Common.Space.TrimRight));
    }

    [Fact]
    public void ReplacesTabWithFourSpaces()
        => Assert.Equal("текст    ещё", Run("текст\tещё", RuleId.Common.Space.ReplaceTab));

    // Вместе с удалением повторов пробелов табуляция даёт один пробел, а не четыре:
    // четыре схлопнулись бы на следующем прогоне и нарушили идемпотентность.
    [Fact]
    public void ReplacesTabWithSingleSpaceWhenRepeatsAreDeleted()
    {
        Assert.Equal(
            "текст ещё",
            Run("текст\tещё", RuleId.Common.Space.ReplaceTab, RuleId.Common.Space.DelRepeatSpace));
        Assert.Equal(
            "текст ещё",
            Text("текст\tещё", RuleId.Common.Space.ReplaceTab, RuleId.Common.Space.DelRepeatSpace));
    }

    [Fact]
    public void RemovesBlanksAtLineEdges()
    {
        Assert.Equal(
            "текст\nдалее",
            Run("текст   \nдалее", RuleId.Common.Space.DelTrailingBlanks));
        Assert.Equal(
            "текст\nдалее",
            Run("текст\n   далее", RuleId.Common.Space.DelLeadingBlanks));
    }

    [Fact]
    public void InsertsFinalNewlineOnce()
    {
        Assert.Equal("текст\n", Run("текст", RuleId.Common.Space.InsertFinalNewline));
        Assert.Equal("текст\n", Run("текст\n", RuleId.Common.Space.InsertFinalNewline));
    }

    [Fact]
    public void DoesNotTouchProtectedContent()
    {
        // Перевод строки внутри защищённой зоны — часть её содержимого, гарантия 3.
        Assert.Equal(
            "<pre>текст\n\n\n\nдалее</pre>",
            Run("<pre>текст\n\n\n\nдалее</pre>", RuleId.Common.Space.DelRepeatN));
        Assert.Equal(
            "<pre>  текст  </pre>",
            Run("<pre>  текст  </pre>", RuleId.Common.Space.TrimLeft, RuleId.Common.Space.TrimRight));
    }

    [Fact]
    public void TrimsEdgesOfDocumentNotOfNode()
    {
        // Пробел между тегом и словом поставил автор — это не край документа.
        Assert.Equal(
            "<b>раз</b> два",
            Run("<b>раз</b> два", RuleId.Common.Space.TrimLeft, RuleId.Common.Space.TrimRight));
    }

    [Fact]
    public void WorksInPlainTextToo()
    {
        Assert.Equal("текст", Text("  текст  ", RuleId.Common.Space.TrimLeft, RuleId.Common.Space.TrimRight));
        Assert.Equal("текст    ещё", Text("текст\tещё", RuleId.Common.Space.ReplaceTab));
    }

    [Fact]
    public void NormalizationIsOutOfDefault()
    {
        // Тот, кто вызвал типограф с настройками по умолчанию, не должен обнаружить
        // обрезанный фрагмент, развёрнутые табы и схлопнутые пустые строки.
        // Повторяющиеся ПРОБЕЛЫ в источнике намеренно отсутствуют: их Default схлопывает
        // законно, это типографика, а не нормализация пробельного письма.
        const string source = " текст\t\n\n\n\nдальше ";
        Assert.Equal(source, new HtmlTypograf().Process(source));
    }
}
