using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class RuleSetTests
{
    [Fact]
    public void Default_ВключаетТиреИКавычки()
    {
        Assert.True(RuleSet.Default.Contains(RuleId.Ru.Dash.Main));
        Assert.True(RuleSet.Default.Contains(RuleId.Common.Punctuation.Quote));
    }

    [Fact]
    public void Default_ИсключаетРискованныеПравила()
    {
        Assert.False(RuleSet.Default.Contains(RuleId.Ru.Typo.SwitchingKeyboardLayout));
        Assert.False(RuleSet.Default.Contains(RuleId.Ru.Punctuation.Ano));
    }

    [Fact]
    public void Without_НеМеняетИсходноеМножество()
    {
        RuleSet reduced = RuleSet.Default.Without(RuleId.Ru.Dash.Main);

        Assert.False(reduced.Contains(RuleId.Ru.Dash.Main));
        Assert.True(RuleSet.Default.Contains(RuleId.Ru.Dash.Main));
    }

    [Fact]
    public void With_ДобавляетВыключенноеПравило()
    {
        RuleSet extended = RuleSet.Default.With(RuleId.Ru.Typo.SwitchingKeyboardLayout);

        Assert.True(extended.Contains(RuleId.Ru.Typo.SwitchingKeyboardLayout));
    }

    [Fact]
    public void None_Пусто_All_Полно()
    {
#pragma warning disable xUnit2013
        Assert.Equal(0, RuleSet.None.Count);
#pragma warning restore xUnit2013
        Assert.True(RuleSet.All.Count >= RuleSet.Default.Count);
    }

    [Theory]
    [InlineData("ru/dash/main")]
    [InlineData("common/punctuation/quote")]
    public void TryParse_РазбираетИмяКакВJsTypograf(string name)
    {
        Assert.True(RuleId.TryParse(name, out RuleId rule));
        Assert.Equal(name, rule.Name);
    }

    [Fact]
    public void TryParse_ОтклоняетНеизвестноеИмя()
    {
        Assert.False(RuleId.TryParse("ru/nbsp/abr", out _));
    }
}
