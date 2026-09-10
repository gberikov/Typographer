using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Последнее слово предложения не отрывается от предпоследнего. Конец предложения — это
/// знак «!», «?», многоточие, точка внутри самого токена или конец документа; запятая
/// границей предложения не является.
/// </summary>
public class NbspLastWordTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypographer(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("Дом стоит там.", "Дом стоит там.")]
    [InlineData("Кто это был?", "Кто это был?")]
    [InlineData("Беги вон!", "Беги вон!")]
    [InlineData("Он ушёл домой.", "Он ушёл домой.")]
    // Запятая конец предложения не образует: «дом» не склеивается, а «всё» — склеивается,
    // потому что точку за ним видит уже сам токен.
    [InlineData("Там был дом, и всё.", "Там был дом, и всё.")]
    // Сокращение «т. д.» правило склеивает тоже — действие совпадает с ru/nbsp/abbr,
    // конфликта нет.
    [InlineData("и т. д.", "и т. д.")]
    public void ShortLastWordBindsBackward(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Nbsp.BeforeShortLastWord));

    [Theory]
    [InlineData("Всего 5.", "Всего 5.")]
    [InlineData("Итого 42!", "Итого 42!")]
    [InlineData("Год 2026.", "Год 2026.")]
    public void ShortLastNumberBindsBackward(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Nbsp.BeforeShortLastNumber));
}
