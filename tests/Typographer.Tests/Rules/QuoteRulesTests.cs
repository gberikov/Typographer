using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class QuoteRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Punctuation.Quote)
            .With(RuleId.Common.Punctuation.Apostrophe),
    }).Process(source);

    [Fact]
    public void ПростыеКавычкиСтановятсяЁлочками()
        => Assert.Equal("Он сказал «привет»", Run("Он сказал \"привет\""));

    [Fact]
    public void ВложенныеКавычкиСтановятсяЛапками()
        => Assert.Equal(
            "Эксперт уточнил: «в перечень включен закон „О стандартизации“»",
            Run("Эксперт уточнил: \"в перечень включен закон \"О стандартизации\"\""));

    [Fact]
    public void ТретийУровеньОдинарныеЛапки()
        => Assert.Equal("«а „б ‘в’ б“ а»", Run("\"а \"б \"в\" б\" а\""));

    [Theory]
    [InlineData("17\"", "17\"")]
    [InlineData("3' 25\"", "3' 25\"")]
    [InlineData("диагональ 15,6\"", "диагональ 15,6\"")]
    public void ШтрихиПослеЦифрОстаютсяПрямыми(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("д'Артаньян", "д’Артаньян")]
    [InlineData("O'Neil", "O’Neil")]
    public void АпострофМеждуБуквами(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Fact]
    public void ТочкаПередЗакрывающейПослеСокращения()
        => Assert.Equal("Он сказал: «Это важно, и т. д.»", Run("Он сказал: \"Это важно, и т. д.\""));
}
