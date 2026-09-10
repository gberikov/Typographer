using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class DashIntervalTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypographer(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("XIX-XX вв.", "XIX\u2014XX вв.")]
    [InlineData("V-X века", "V\u2014X века")]
    public void WritesDashBetweenCenturies(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Dash.Centuries));

    [Theory]
    [InlineData("80-90-е гг.", "80\u201490-е гг.")]
    [InlineData("20-30-х годов", "20\u201430-х годов")]
    // Без наращения это диапазон чисел, а не десятилетий.
    [InlineData("10-15 штук", "10-15 штук")]
    public void WritesDashBetweenDecades(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Dash.Decade));

    [Theory]
    [InlineData("10:00-11:00", "10:00\u201411:00")]
    [InlineData("с 9:30-10:45", "с 9:30\u201410:45")]
    public void WritesDashBetweenTimes(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Dash.Time));

    [Fact]
    public void DictionariesKnowMonthsAndWeekdays()
    {
        Assert.True(Dictionaries.IsMonth("января".AsSpan()));
        Assert.True(Dictionaries.IsMonth("Январь".AsSpan()));
        Assert.False(Dictionaries.IsMonth("январей".AsSpan()));
        Assert.True(Dictionaries.IsWeekday("среда".AsSpan()));
        Assert.False(Dictionaries.IsWeekday("средa".AsSpan()));
    }
}
