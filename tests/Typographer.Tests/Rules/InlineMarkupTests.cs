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

    // Амперсанд, который сущность уже начинает, второй раз не экранируется: «&amp;amp;»
    // превращалось в «&amp;amp;amp;», и после разбора браузером параметр «b» назывался
    // «amp;b» — адрес в href переставал совпадать с адресом в тексте ссылки.
    [Theory]
    [InlineData(
        "https://a.ru/?x=1&amp;y=2",
        "<a href=\"https://a.ru/?x=1&amp;y=2\">https://a.ru/?x=1&amp;y=2</a>")]
    [InlineData(
        "https://a.ru/?x=&#34;q&#34;",
        "<a href=\"https://a.ru/?x=&#34;q&#34;\">https://a.ru/?x=&#34;q&#34;</a>")]
    // Амперсанд без точки с запятой сущности не начинает и экранируется как прежде.
    [InlineData("https://a.ru/?x=1&y=2", "<a href=\"https://a.ru/?x=1&amp;y=2\">https://a.ru/?x=1&y=2</a>")]
    [InlineData("https://a.ru/&;x", "<a href=\"https://a.ru/&amp;;x\">https://a.ru/&;x</a>")]
    // «&#;» и «&#x;» — не сущности ни с какой стороны: амперсанд экранируется, а точка с
    // запятой у них хвостовая и к адресу не относится.
    [InlineData("https://a.ru/?x=&#;", "<a href=\"https://a.ru/?x=&amp;#\">https://a.ru/?x=&#</a>;")]
    [InlineData("https://a.ru/?x=&#x;", "<a href=\"https://a.ru/?x=&amp;#x\">https://a.ru/?x=&#x</a>;")]
    public void ExistingEntityInAddressIsNotEscapedTwice(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Html.Url));

    // Точка с запятой, закрывающая сущность, принадлежит адресу: без этого хвост записи
    // оставался снаружи ссылки, и адрес обрывался внутри сущности.
    [Fact]
    public void SemicolonOfEntityStaysInsideAddress()
        => Assert.Equal(
            "<a href=\"https://a.ru/?q=&quot;x&quot;\">https://a.ru/?q=&quot;x&quot;</a>",
            Run("https://a.ru/?q=&quot;x&quot;", RuleId.Common.Html.Url));

    // А обычная точка с запятой после адреса по-прежнему к нему не относится.
    [Fact]
    public void SemicolonAfterAddressIsStillTrimmed()
        => Assert.Equal(
            "Сайт <a href=\"http://a.ru\">http://a.ru</a>; далее",
            Run("Сайт http://a.ru; далее", RuleId.Common.Html.Url));

    // Границы адреса у автоссылки те же, что у фазы Scan: закрывающая кавычка адрес
    // завершает. Иначе «»» уходила в href, и ссылка вела в никуда.
    [Theory]
    [InlineData("«https://a.ru» и", "«<a href=\"https://a.ru\">https://a.ru</a>» и")]
    [InlineData("‘https://a.ru’", "‘<a href=\"https://a.ru\">https://a.ru</a>’")]
    // Английская закрывающая кавычка: типограф её не ставит, но во входе она встречается.
    [InlineData("“https://a.ru”", "“<a href=\"https://a.ru\">https://a.ru</a>”")]
    // Правая одинарная кавычка между буквами — апостроф, часть адреса.
    [InlineData(
        "https://a.ru/d’Artagnan",
        "<a href=\"https://a.ru/d’Artagnan\">https://a.ru/d’Artagnan</a>")]
    // Прямой апостроф внутри адреса законен (RFC 3986) и границей не является; с хвоста
    // автоссылка отрезает его как знак препинания.
    [InlineData("'https://a.ru'", "'<a href=\"https://a.ru\">https://a.ru</a>'")]
    // Парный апостроф внутри параметра принадлежит самому адресу и остаётся в ссылке.
    [InlineData(
        "https://a.ru/?q='hello'",
        "<a href=\"https://a.ru/?q='hello'\">https://a.ru/?q='hello'</a>")]
    // Внешняя кавычка отрезается и при наличии парных апострофов внутри адреса.
    [InlineData(
        "'https://a.ru/?q='x''",
        "'<a href=\"https://a.ru/?q='x'\">https://a.ru/?q='x'</a>'")]
    [InlineData(
        "https://a.ru/?q='x y'",
        "<a href=\"https://a.ru/?q='x\">https://a.ru/?q='x</a> y'")]
    public void ClosingQuoteEndsTheAutolink(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Html.Url));
}
