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
    public void УдаляетПовторяющиесяПробелы(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("слово , запятая", "слово, запятая")]
    [InlineData("слово !", "слово!")]
    [InlineData("слово ?", "слово?")]
    public void УдаляетПробелПередПунктуацией(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Fact]
    public void ДобавляетПробелПослеЗапятой()
        => Assert.Equal("раз, два, три", Run("раз,два,три"));

    [Fact]
    public void НеТрогаетЗапятуюВЧисле()
        => Assert.Equal("3,14", Run("3,14"));

    [Fact]
    public void ЗаменяетТриТочкиНаМноготочие()
        => Assert.Equal("вот…", Run("вот..."));

    [Fact]
    public void НеТрогаетЧетыреТочки()
        => Assert.Equal("вот....", Run("вот...."));
}
