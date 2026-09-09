using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class RuPunctuationTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypograf(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("Ура!!", "Ура!")]
    [InlineData("Ура!!!", "Ура!")]
    public void CollapsesDoubledExclamation(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Punctuation.Exclamation));

    [Theory]
    [InlineData("Что!?", "Что?!")]
    public void PutsQuestionMarkFirst(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Punctuation.ExclamationQuestion));

    [Theory]
    // Готовое многоточие во входе.
    [InlineData("Что?\u2026", "Что?..")]
    [InlineData("Ура!\u2026", "Ура!..")]
    // Многоточие, собранное правилом из трёх точек.
    [InlineData("Что?...", "Что?..")]
    [InlineData("Ура!...", "Ура!..")]
    // Запятая после многоточия не нужна.
    [InlineData("Текст\u2026, ещё", "Текст\u2026 ещё")]
    public void ShortensEllipsisAfterSentenceMark(string source, string expected)
        => Assert.Equal(
            expected,
            Run(source, RuleId.Ru.Punctuation.HellipQuestion, RuleId.Common.Punctuation.Hellip));

    [Theory]
    [InlineData("Пришёл а ушёл", "Пришёл, а ушёл")]
    [InlineData("Хотел но не смог", "Хотел, но не смог")]
    // Запятая уже есть — второй не появляется.
    [InlineData("Пришёл, а ушёл", "Пришёл, а ушёл")]
    public void InsertsCommaBeforeConjunction(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Punctuation.Ano));

    [Fact]
    public void AnoIsOutOfDefault()
    {
        // Правило само расставляет знаки, то есть меняет текст, а не оформление.
        Assert.False(RuleSet.Default.Contains(RuleId.Ru.Punctuation.Ano));
        Assert.DoesNotContain(",", new TextTypograf().Process("Пришёл а ушёл"));
    }
}
