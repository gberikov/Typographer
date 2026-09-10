using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Группа <c>common/html/*</c>: правила уровня разметки. Все они, кроме
/// <c>common/html/quot</c>, вне <see cref="RuleSet.Default"/> — они делают тег из текста или
/// преобразуют разметку, а гарантия 4 обещает, что правила по умолчанию так не поступают.
/// </summary>
public class HtmlRulesTests
{
    private static string Run(string source, params RuleId[] rules)
        => new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Fact]
    public void QuotEntityBecomesQuoteCharacter()
    {
        Assert.Equal(
            "«цитата»",
            Run("&quot;цитата&quot;", RuleId.Common.Html.Quot, RuleId.Common.Punctuation.Quote));
        Assert.Equal(
            "«цитата»",
            Run("&#34;цитата&#34;", RuleId.Common.Html.Quot, RuleId.Common.Punctuation.Quote));
        Assert.Equal("\"текст\"", Run("&quot;текст&quot;", RuleId.Common.Html.Quot));
    }

    [Fact]
    public void QuotEntityStaysWithoutTheRule()
        => Assert.Equal(
            "&quot;текст&quot;",
            Run("&quot;текст&quot;", RuleId.Common.Punctuation.Quote));

    // Экранированный амперсанд сущностью не является: «&amp;quot;» — это текст «&quot;».
    [Fact]
    public void EscapedAmpersandIsNotAnEntity()
        => Assert.Equal(
            "&amp;quot;",
            Run("&amp;quot;", RuleId.Common.Html.Quot, RuleId.Common.Punctuation.Quote));

    // Значение атрибута — разметка, а гарантия 3 обещает её байт в байт.
    [Fact]
    public void QuotInsideAttributeIsUntouched()
        => Assert.Equal(
            "<a title=\"&quot;цитата&quot;\">x</a>",
            Run("<a title=\"&quot;цитата&quot;\">x</a>", RuleId.Common.Html.Quot));

    // Правило и опция говорят одно и то же и складываются по «или»: заводить второй
    // механизм для того, что уже написано и оттестировано, незачем.
    [Fact]
    public void NbrRuleReplacesNewlineLikeTheOption()
        => Assert.Equal("первая<br />\nвторая", Run("первая\nвторая", RuleId.Common.Html.Nbr));

    [Fact]
    public void PRuleWrapsParagraphsLikeTheOption()
        => Assert.Equal("<p>первый</p>\n<p>второй</p>", Run("первый\n\nвторой", RuleId.Common.Html.P));

    [Fact]
    public void PRuleDoesNotWrapExistingBlockMarkup()
        => Assert.Equal(
            "<ul>\n<li>раз</li>\n</ul>",
            Run("<ul>\n<li>раз</li>\n</ul>", RuleId.Common.Html.P));

    [Fact]
    public void NewlineStaysWithoutRuleAndOption()
        => Assert.Equal("первая\nвторая", Run("первая\nвторая", RuleId.Common.Punctuation.Quote));
}
