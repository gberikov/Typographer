using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class SymbolRulesTests
{
    private static string Run(string source) => new TextTypographer(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Symbols.Copy)
            .With(RuleId.Common.Symbols.Arrow)
            .With(RuleId.Common.Symbols.Cf)
            .With(RuleId.Ru.Symbols.NN),
    }).Process(source);

    [Theory]
    [InlineData("(c) 2026", "\u00a9 2026")]
    [InlineData("(C) 2026", "\u00a9 2026")]
    [InlineData("Марка (tm) и (r)", "Марка \u2122 и \u00ae")]
    public void WritesSignsFromBracketNotation(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("вперёд -> назад <- сюда", "вперёд \u2192 назад \u2190 сюда")]
    public void WritesArrows(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("25 C", "25 \u00b0C")]
    [InlineData("451 F", "451 \u00b0F")]
    // Справа буква: это не градусы, а начало слова.
    [InlineData("10 Cm", "10 Cm")]
    // Слева не цифра: тоже не градусы.
    [InlineData("шкала C", "шкала C")]
    public void AddsDegreeOnlyToTemperature(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Fact]
    public void CollapsesDoubleNumeroSign()
        => Assert.Equal($"{Chars.Numero} 5", Run($"{Chars.Numero}{Chars.Numero} 5"));

    [Fact]
    public void DoesNotEatLetterAfterReadySign()
    {
        // Готовый знак во входе не должен съедать следующую букву: правило узнаёт образец
        // по исходной строке, а не по последнему символу буфера.
        Assert.Equal("\u00a9cat", Run("\u00a9cat"));
        Assert.Equal("\u2192нет", Run("\u2192нет"));
    }
}
