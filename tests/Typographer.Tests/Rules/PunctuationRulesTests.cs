using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class PunctuationRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Punctuation.DelDoublePunctuation)
            .With(RuleId.Common.Punctuation.Hellip),
    }).Process(source);

    [Theory]
    [InlineData("слово,,ещё", "слово,ещё")]
    [InlineData("слово;;ещё", "слово;ещё")]
    [InlineData("слово::ещё", "слово:ещё")]
    public void RemovesDoubledPunctuation(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    // Три точки — многоточие, а не удвоение: правило их не трогает.
    [InlineData("текст...", "текст\u2026")]
    // Удвоение знаков конца предложения осмысленно в разговорной речи: решение о нём
    // принимает правило русского языка, а не общее правило двойной пунктуации.
    [InlineData("Ура!! и ??", "Ура!! и ??")]
    public void LeavesEllipsisAndSentenceMarksAlone(string source, string expected)
        => Assert.Equal(expected, Run(source));
}
