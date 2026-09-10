using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Склейка числа с тем, что за ним следует. Неразрывный пробел в ожиданиях записан
/// escape-последовательностью: в <c>[InlineData]</c> интерполяция недоступна, а сам символ
/// в исходнике невидим и ревью его не поймает.
/// </summary>
public class NbspNumberTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypograf(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("10 кг", "10 кг")]
    [InlineData("100 км/ч", "100 км/ч")]
    [InlineData("30 мин.", "30 мин.")]
    [InlineData("дом 5", "дом 5")]
    [InlineData("2026 2027", "2026 2027")]
    public void NumberBindsToFollowingWord(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Nbsp.AfterNumber));

    [Theory]
    [InlineData("5 января", "5 января")]
    [InlineData("31 декабря", "31 декабря")]
    [InlineData("5 январь", "5 январь")]
    [InlineData("5 яблок", "5 яблок")]
    public void DayBindsToMonthWithoutAfterNumber(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.DayMonth));

    [Theory]
    [InlineData("2012 г.", "2012 г.")]
    [InlineData("1990 гг.", "1990 гг.")]
    [InlineData("дом г.", "дом г.")]
    [InlineData("5 г.", "5 г.")]
    public void YearBindsToAbbreviation(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.Year));
}
