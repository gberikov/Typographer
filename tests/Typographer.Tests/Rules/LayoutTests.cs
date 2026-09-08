using Typographer.Internal;
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
    public void UseP_РаспознаётWindowsПереводСтроки()
        => Assert.Equal(
            "<p>первый</p>\n<p>второй</p>",
            Run("первый\r\n\r\nвторой", new HtmlOptions { Rules = RuleSet.None, UseP = true }));

    [Fact]
    public void БезФлаговНичегоНеДобавляется()
        => Assert.Equal(
            "первая\nвторая",
            Run("первая\nвторая", new HtmlOptions { Rules = RuleSet.None }));

    [Fact]
    public void ВозвратКаретки_СчитаетсяГраницейСловаВNobr()
        => Assert.Equal(
            $"<nobr>текст{Chars.Nbsp}слово</nobr>\rконец",
            Run($"текст{Chars.Nbsp}слово\rконец", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 }));

    [Fact]
    public void MaxNobr_ОборачиваетНеразрывныеГруппы()
        => Assert.Equal(
            $"<nobr>в{Chars.Nbsp}доме</nobr> на горе",
            Run($"в{Chars.Nbsp}доме на горе", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 }));

    [Fact]
    public void MaxNobrРавныйЕдинице_НеСоздаётБлоковИЗавершается()
        => Assert.Equal(
            $"a{Chars.Nbsp}b{Chars.Nbsp}c",
            Run($"a{Chars.Nbsp}b{Chars.Nbsp}c", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 1 }));

    [Fact]
    public void ЦепочкаДлиннееПредела_РежетсяМеждуСловами()
        => Assert.Equal(
            $"<nobr>a{Chars.Nbsp}b</nobr>{Chars.Nbsp}<nobr>c{Chars.Nbsp}d</nobr>",
            Run($"a{Chars.Nbsp}b{Chars.Nbsp}c{Chars.Nbsp}d", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 }));

    [Fact]
    public void ОдиночныйНеразрывныйПробел_НеОборачивается()
        => Assert.Equal(
            Chars.Nbsp.ToString(),
            Run(Chars.Nbsp.ToString(), new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 }));

    [Fact]
    public void ТекстБезНеразрывныхПробелов_НеМеняется()
        => Assert.Equal(
            "просто слова",
            Run("просто слова", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 3 }));

    [Fact]
    public void ПустойАбзацНеСоздаётся()
        => Assert.Equal(
            "<p>текст</p>",
            Run("\n\nтекст\n\n", new HtmlOptions { Rules = RuleSet.None, UseP = true }));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ДлиннаяЦепочка_ЗавершаетсяИНеТеряетСимволы(int maxNobr)
    {
        string chain = string.Join(Chars.Nbsp.ToString(), Enumerable.Range(0, 50).Select(n => $"w{n}"));

        string result = Run(chain, new HtmlOptions { Rules = RuleSet.None, MaxNobr = maxNobr });

        string stripped = result.Replace("<nobr>", string.Empty).Replace("</nobr>", string.Empty);
        Assert.Equal(chain, stripped);
    }
}
