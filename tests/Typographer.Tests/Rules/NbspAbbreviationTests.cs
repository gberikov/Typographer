using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Склейка сокращения с тем, что за ним следует, — первая склейка ВПЕРЁД: решение
/// принимается по текущему токену, а неразрывным становится пробел, который диспетчер
/// запишет следующим шагом.
/// </summary>
public class NbspAbbreviationTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypographer(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("ул. Ленина", "ул. Ленина")]
    [InlineData("г. Москва", "г. Москва")]
    [InlineData("с. Никольское", "с. Никольское")]
    [InlineData("д. 5, кв. 12", "д. 5, кв. 12")]
    [InlineData("корп. 2", "корп. 2")]
    // Отличить сокращение «город» от слова «года» в такой позиции без разбора смысла
    // невозможно. Цена ошибки — неразрывный пробел там, где он не нужен; так же
    // поступает JS-typograf.
    [InlineData("или г. далее", "или г. далее")]
    [InlineData("улица широкая", "улица широкая")]
    public void AddressAbbreviationBindsForward(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.Addr));

    [Theory]
    [InlineData("стр. 15", "стр. 15")]
    [InlineData("рис. 3", "рис. 3")]
    [InlineData("гл. 2", "гл. 2")]
    [InlineData("табл. 7", "табл. 7")]
    public void PageAbbreviationBindsForward(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.Page));

    [Theory]
    [InlineData("см. таблицу", "см. таблицу")]
    [InlineData("им. Пушкина", "им. Пушкина")]
    [InlineData("смета готова", "смета готова")]
    public void ReferenceAbbreviationBindsForward(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.See));

    [Fact]
    public void ReferenceAndPageBindTogether()
        => Assert.Equal(
            "см. рис. 3",
            Run("см. рис. 3", RuleId.Ru.Nbsp.See, RuleId.Ru.Nbsp.Page));

    [Theory]
    [InlineData("ООО Ромашка", "ООО Ромашка")]
    [InlineData("НИИ Точмаш", "НИИ Точмаш")]
    [InlineData("ЗАО Вектор", "ЗАО Вектор")]
    // Границей оказался не пробел — связывать нечего.
    [InlineData("ООО, а также", "ООО, а также")]
    // Регистр значим: «ооо» строчными — не название формы собственности.
    [InlineData("ооо как страшно", "ооо как страшно")]
    public void OrganizationBindsForward(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.Ooo));
}
