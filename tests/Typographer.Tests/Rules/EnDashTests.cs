using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class EnDashTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypograf(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Fact]
    public void BritishDashKeepsSpacing()
        => Assert.Equal("word \u2013 word", Run("word - word", RuleId.EnGb.Dash.Main));

    [Fact]
    public void AmericanDashRemovesSpacing()
        => Assert.Equal("word\u2014word", Run("word - word", RuleId.EnUs.Dash.Main));

    [Fact]
    public void RussianDashWinsWhenEnabledTogether()
    {
        // Язык библиотеки русский: при конфликте побеждает русское правило, ставящее
        // длинное тире с неразрывным пробелом слева.
        string result = Run("word - word", RuleId.Ru.Dash.Main, RuleId.EnGb.Dash.Main);
        Assert.Equal($"word\u00a0\u2014 word", result);
    }

    [Fact]
    public void BothAreOutOfDefault()
    {
        Assert.False(RuleSet.Default.Contains(RuleId.EnGb.Dash.Main));
        Assert.False(RuleSet.Default.Contains(RuleId.EnUs.Dash.Main));
    }
}
