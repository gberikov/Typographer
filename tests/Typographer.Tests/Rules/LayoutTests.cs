using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class LayoutTests
{
    private static string Run(string source, HtmlOptions options) => new HtmlTypograf(options).Process(source);

    [Fact]
    public void UseBr_ЗаменяетПереводСтроки()
        => Assert.Equal(
            "первая<br />\nвторая",
            Run("первая\nвторая", new HtmlOptions { Rules = RuleSet.None, UseBr = true }));

    [Fact]
    public void UseP_ОборачиваетАбзацы()
        => Assert.Equal(
            "<p>первый</p>\n<p>второй</p>",
            Run("первый\n\nвторой", new HtmlOptions { Rules = RuleSet.None, UseP = true }));

    [Fact]
    public void БезФлаговНичегоНеДобавляется()
        => Assert.Equal(
            "первая\nвторая",
            Run("первая\nвторая", new HtmlOptions { Rules = RuleSet.None }));

    [Fact]
    public void MaxNobr_ОборачиваетНеразрывныеГруппы()
        => Assert.Equal(
            "<nobr>в\u00a0доме</nobr> на горе",
            Run("в\u00a0доме на горе", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 }));
}
