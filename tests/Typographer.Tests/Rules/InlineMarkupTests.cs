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
}
