using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Проход фазы Layout, вставляющий разметку внутрь текстовых узлов: автоссылки и висячая
/// пунктуация.
/// </summary>
public class InlineMarkupTests
{
    private static string Run(string source, params RuleId[] rules)
        => new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    // Проход обязан вернуть документ байт в байт, если ни одно правило не сработало, и не
    // запускаться вовсе, когда его правила выключены.
    [Fact]
    public void PassCopiesEverythingItDoesNotChange()
    {
        const string source = "<p title=\"a - b\">раз<!-- к --><code>x  y</code>два</p>";
        Assert.Equal(source, Run(source, RuleId.Common.Html.Url));
        Assert.Equal(source, Run(source, RuleId.Common.Punctuation.Quote));
    }

    [Theory]
    [InlineData("http://example.com", "<a href=\"http://example.com\">http://example.com</a>")]
    [InlineData(
        "https://a.ru/b?x=1&y=2",
        "<a href=\"https://a.ru/b?x=1&amp;y=2\">https://a.ru/b?x=1&y=2</a>")]
    [InlineData("Сайт http://a.ru.", "Сайт <a href=\"http://a.ru\">http://a.ru</a>.")]
    // Внутри уже открытой ссылки правило не работает: вложенная ссылка невалидна.
    [InlineData("<a href=\"#\">http://a.ru</a>", "<a href=\"#\">http://a.ru</a>")]
    [InlineData("текст без адреса", "текст без адреса")]
    public void UrlBecomesLink(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Html.Url));

    [Theory]
    [InlineData("mail@example.com", "<a href=\"mailto:mail@example.com\">mail@example.com</a>")]
    [InlineData(
        "Пишите на mail@example.com.",
        "Пишите на <a href=\"mailto:mail@example.com\">mail@example.com</a>.")]
    [InlineData("@ и собака", "@ и собака")]
    // Домен без точки и без зоны адресом не является.
    [InlineData("mail@example", "mail@example")]
    [InlineData("<a href=\"#\">m@e.com</a>", "<a href=\"#\">m@e.com</a>")]
    public void EmailBecomesLink(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Html.EMail));

    // Прогон по собственному выводу вкладывал бы ссылку в ссылку.
    [Fact]
    public void LinkRulesAreIdempotent()
    {
        string once = Run("Сайт http://a.ru и почта m@e.com", RuleId.Common.Html.Url, RuleId.Common.Html.EMail);
        Assert.Equal(once, Run(once, RuleId.Common.Html.Url, RuleId.Common.Html.EMail));
    }

    // Защищённая зона остаётся байт в байт: внутри code ссылка не размечается.
    [Fact]
    public void ProtectedZoneKeepsItsText()
        => Assert.Equal(
            "<code>http://a.ru</code>",
            Run("<code>http://a.ru</code>", RuleId.Common.Html.Url));
}
