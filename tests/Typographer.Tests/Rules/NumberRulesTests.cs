using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class NumberRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Number.Fraction)
            .With(RuleId.Common.Number.MathSigns)
            .With(RuleId.Common.Number.Times),
    }).Process(source);

    private static string Grouped(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None.With(RuleId.Common.Number.DigitGrouping),
    }).Process(source);

    [Theory]
    [InlineData("1/2 стакана", "\u00bd стакана")]
    [InlineData("1/4 и 3/4", "\u00bc и \u00be")]
    // Дата — не дробь: слева и справа цифры.
    [InlineData("01/02 число", "01/02 число")]
    // Такой дроби в Юникоде нет — оставляем как есть.
    [InlineData("5/7 доли", "5/7 доли")]
    public void WritesFractions(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("8 != 9", "8 \u2260 9")]
    [InlineData("5 <= 6 и 7 >= 3", "5 \u2264 6 и 7 \u2265 3")]
    [InlineData("2 ~= 2 и 5 +- 1", "2 \u2245 2 и 5 \u00b1 1")]
    public void WritesMathSigns(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("10 x 5", "10\u00d75")]
    [InlineData("Формула 3 x 4 = 12", "Формула 3\u00d74 = 12")]
    // Кириллическая «х» — буква, а не знак умножения.
    [InlineData("5 х 4", "5 х 4")]
    // Слева не число: это слово, начинающееся с латинской буквы.
    [InlineData("икс x игрек", "икс x игрек")]
    public void WritesTimesOnlyBetweenNumbers(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("1000000 рублей", "1\u00a0000\u00a0000 рублей")]
    [InlineData("12345 штук", "12\u00a0345 штук")]
    // Четырёхзначное число ещё читается — не трогаем.
    [InlineData("1941 год", "1941 год")]
    // Дробная часть разрядами не разбивается.
    [InlineData("3.14159", "3.14159")]
    public void GroupsLongNumbers(string source, string expected)
        => Assert.Equal(expected, Grouped(source));
}
