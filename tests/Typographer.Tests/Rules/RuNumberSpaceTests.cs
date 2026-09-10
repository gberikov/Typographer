using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class RuNumberSpaceTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypographer(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("3.14", "3,14")]
    [InlineData("цена 19.99 рубля", "цена 19,99 рубля")]
    // Дата и номер версии: точек несколько, это не десятичный разделитель.
    [InlineData("09.09.2026", "09.09.2026")]
    [InlineData("версия 1.2.3", "версия 1.2.3")]
    public void WritesDecimalComma(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Number.Comma));

    [Theory]
    [InlineData("25-ый дом", "25-й дом")]
    [InlineData("2-ой этаж", "2-й этаж")]
    [InlineData("3-ая полоса", "3-я полоса")]
    [InlineData("5-ое место", "5-е место")]
    [InlineData("7-ым путём", "7-м путём")]
    [InlineData("10-ых годов", "10-х годов")]
    public void ShortensOrdinals(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Number.Ordinals));

    [Theory]
    [InlineData("Что?..Как", "Что?.. Как")]
    [InlineData("Текст...Ещё", "Текст... Ещё")]
    public void AddsSpaceAfterEllipsis(string source, string expected)
        => Assert.Equal(
            expected,
            Run(source, RuleId.Ru.Space.AfterHellip, RuleId.Ru.Punctuation.HellipQuestion));

    [Theory]
    [InlineData("2026год", "2026 год")]
    [InlineData("в 1941году", "в 1941 году")]
    // Слово не «год»: пробел не нужен.
    [InlineData("2026годовой", "2026годовой")]
    public void AddsSpaceBeforeYearWord(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Space.Year));
}
