using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class SpaceRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Space.DelRepeatSpace)
            .With(RuleId.Common.Space.DelBeforePunctuation)
            .With(RuleId.Common.Space.AfterComma)
            .With(RuleId.Common.Punctuation.Hellip),
    }).Process(source);

    [Theory]
    [InlineData("два  пробела", "два пробела")]
    [InlineData("три   пробела", "три пробела")]
    public void CollapsesRepeatedSpaces(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("слово , запятая", "слово, запятая")]
    [InlineData("слово !", "слово!")]
    [InlineData("слово ?", "слово?")]
    public void RemovesSpaceBeforePunctuation(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Fact]
    public void AddsSpaceAfterComma()
        => Assert.Equal("раз, два, три", Run("раз,два,три"));

    [Fact]
    public void LeavesCommaInNumberAlone()
        => Assert.Equal("3,14", Run("3,14"));

    [Fact]
    public void NoSpaceBetweenTwoPunctuationMarks()
        => Assert.Equal("текст,, ещё", Run("текст,,ещё"));

    [Fact]
    public void StableOnSecondPassForAdjacentPunctuation()
    {
        string once = Run("текст,,ещё");

        Assert.Equal(once, Run(once));
    }

    [Fact]
    public void NoSpaceBetweenCommaAndEllipsis()
        => Assert.Equal("раз,…два", Run("раз,...два"));

    [Fact]
    public void StableOnSecondPassForCommaBeforeEllipsis()
    {
        string once = Run("раз,...два");

        Assert.Equal(once, Run(once));
    }

    // Пробел не ставится слева от закрывающей скобки, кавычки и перевода строки: там его
    // не бывает, а перед кавычкой он к тому же делает её открывающей и сбивает счётчик
    // уровней до конца документа.
    [Theory]
    [InlineData("(а,) б", "(а,) б")]
    [InlineData("а,\nб", "а,\nб")]
    [InlineData("а,\tб", "а,\tб")]
    public void NoSpaceBeforeClosingCharsAndLineBreak(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Fact]
    public void ReplacesThreeDotsWithEllipsis()
        => Assert.Equal("вот…", Run("вот..."));

    [Fact]
    public void LeavesFourDotsAlone()
        => Assert.Equal("вот....", Run("вот...."));
}
