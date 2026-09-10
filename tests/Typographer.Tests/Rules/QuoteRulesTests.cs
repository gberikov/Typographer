using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class QuoteRulesTests
{
    private static string Run(string source) => new TextTypographer(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Punctuation.Quote)
            .With(RuleId.Common.Punctuation.Apostrophe),
    }).Process(source);

    [Fact]
    public void StraightQuotesBecomeGuillemets()
        => Assert.Equal("Он сказал «привет»", Run("Он сказал \"привет\""));

    [Fact]
    public void NestedQuotesBecomeGermanQuotes()
        => Assert.Equal(
            "Эксперт уточнил: «в перечень включен закон „О стандартизации“»",
            Run("Эксперт уточнил: \"в перечень включен закон \"О стандартизации\"\""));

    [Fact]
    public void ThirdLevelUsesSingleGermanQuotes()
        => Assert.Equal("«а „б ‘в’ б“ а»", Run("\"а \"б \"в\" б\" а\""));

    [Theory]
    [InlineData("17\"", "17\"")]
    [InlineData("3' 25\"", "3' 25\"")]
    [InlineData("диагональ 15,6\"", "диагональ 15,6\"")]
    public void PrimesAfterDigitsStayStraight(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("д'Артаньян", "д’Артаньян")]
    [InlineData("O'Neil", "O’Neil")]
    public void ApostropheBetweenLetters(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("\t\"цитата\"", "\t«цитата»")]
    [InlineData("\r\"цитата\"", "\r«цитата»")]
    [InlineData("{\"цитата\"}", "{«цитата»}")]
    public void OpeningContextNotLimitedToSpaceAndBrackets(string source, string expected)
        => Assert.Equal(expected, Run(source));

    // Ёлочка, уже стоящая во входе, обязана двигать счётчик уровней: иначе кавычка внутри
    // неё открывает уровень 0 повторно и вложенная цитата получает ёлочки вместо лапок.
    [Fact]
    public void ExistingGuillemetCountsAsLevel()
        => Assert.Equal("«закон „О стандартизации“»", Run("«закон \"О стандартизации\"»"));

    [Fact]
    public void AllExistingQuotesMoveLevelStack()
        => Assert.Equal(
            "«внешняя „внутренняя ‘третья’“»",
            Run("«внешняя „внутренняя \"третья\"“»"));

    [Fact]
    public void ApostropheInExistingQuotesDoesNotCloseLevel()
        => Assert.Equal(
            "«д’Артаньян и „цитата“»",
            Run("«д’Артаньян и \"цитата\"»"));

    [Fact]
    public void MixedExistingQuotesStayIdempotent()
    {
        string once = Run("\"цитата «внутри\" конец\" 15\"");

        Assert.Equal(once, Run(once));
    }

    [Fact]
    public void PeriodBeforeClosingQuoteAfterAbbreviation()
        => Assert.Equal("Он сказал: «Это важно, и т. д.»", Run("Он сказал: \"Это важно, и т. д.\""));
}
