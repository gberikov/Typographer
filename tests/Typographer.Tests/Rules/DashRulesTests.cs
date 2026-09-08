using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class DashRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Ru.Dash.Main)
            .With(RuleId.Ru.Dash.DirectSpeech)
            .With(RuleId.Ru.Dash.Years),
    }).Process(source);

    [Fact]
    public void ДефисМеждуСловамиСПробеламиСтановитсяТире()
        => Assert.Equal("Он — человек", Run("Он - человек"));

    [Theory]
    [InlineData("из-за")]
    [InlineData("по-русски")]
    [InlineData("кто-то")]
    [InlineData("из-под")]
    public void ДефисВнутриСловаНеТрогаем(string source)
        => Assert.Equal(source, Run(source));

    [Fact]
    public void ДиапазонГодовКороткимТиреБезОтбивки()
        => Assert.Equal("1941—1945", Run("1941-1945"));

    [Fact]
    public void ТиреПрямойРечиВНачалеСтроки()
        => Assert.Equal("— Привет, — сказал он.", Run("- Привет, - сказал он."));

    [Fact]
    public void МинусПередЧисломНеСтановитсяТире()
        => Assert.Equal("от -5 до +5", Run("от -5 до +5"));

    [Fact]
    public void НеТрогаетДефисВUrl()
        => Assert.Equal("http://example.com/a-b", Run("http://example.com/a-b"));
}
