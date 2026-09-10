using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class DashParticleTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypographer(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("скажи ка", "скажи-ка")]
    [InlineData("ну кась", "ну-кась")]
    public void HyphenatesKa(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Dash.Ka));

    [Fact]
    public void HyphenatesTaki()
        => Assert.Equal("верно-таки", Run("верно таки", RuleId.Ru.Dash.Taki));

    [Theory]
    [InlineData("кое что", "кое-что")]
    [InlineData("кой какой", "кой-какой")]
    public void HyphenatesKoe(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Dash.Koe));

    [Fact]
    public void HyphenatesIzpod()
        => Assert.Equal("из-под стола", Run("из под стола", RuleId.Ru.Dash.Izpod));

    [Fact]
    public void HyphenatesIzza()
        => Assert.Equal("из-за угла", Run("из за угла", RuleId.Ru.Dash.Izza));

    [Theory]
    [InlineData("кто то", "кто-то")]
    [InlineData("где либо", "где-либо")]
    [InlineData("что нибудь", "что-нибудь")]
    public void HyphenatesToWhenAskedExplicitly(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Dash.To));

    [Fact]
    public void AmbiguousParticlesAreOutOfDefault()
    {
        // «что то» бывает союзом с указательным местоимением, и дефис сменил бы смысл фразы.
        // Правило есть, но включает его только тот, кто знает свой текст.
        // Проверяется отсутствие дефиса, а не побайтовое равенство: Default законно ставит
        // неразрывные пробелы после коротких слов, и они здесь ни при чём.
        var typographer = new TextTypographer();
        Assert.DoesNotContain("-", typographer.Process("Я знал, что то было ошибкой"));
        Assert.DoesNotContain("-", typographer.Process("Шарль де Голль"));
        Assert.DoesNotContain("-", typographer.Process("как то так"));
    }

    [Fact]
    public void HyphenatesInsideDefaultWhereUnambiguous()
    {
        var typographer = new TextTypographer();
        Assert.StartsWith("кое-что", typographer.Process("кое что"));
        Assert.StartsWith("из-под", typographer.Process("из под стола"));
    }
}
