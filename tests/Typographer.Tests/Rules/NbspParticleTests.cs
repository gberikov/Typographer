using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Частица склеивается НАЗАД, служебное слово — ВПЕРЁД. В одном файле они потому, что
/// бьются об одну ловушку: слово, которое лишь НАЧИНАЕТСЯ с частицы. «бытие» начинается на
/// «бы», «лишь» — на «ли», и правило обязано сверять токен целиком.
/// </summary>
public class NbspParticleTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypograf(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("так ли это", "так ли это")]
    [InlineData("он же", "он же")]
    [InlineData("если бы", "если бы")]
    [InlineData("пошёл бы я", "пошёл бы я")]
    [InlineData("лишь бытие", "лишь бытие")]
    [InlineData("вот жизнь", "вот жизнь")]
    public void ParticleBindsBackward(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.BeforeParticle));

    [Theory]
    [InlineData("через дорогу", "через дорогу")]
    [InlineData("между нами", "между нами")]
    [InlineData("чтобы успеть", "чтобы успеть")]
    [InlineData("дерево стоит", "дерево стоит")]
    [InlineData("междуречье широко", "междуречье широко")]
    public void FunctionWordBindsForward(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Nbsp.AfterShortWordByList));
}
