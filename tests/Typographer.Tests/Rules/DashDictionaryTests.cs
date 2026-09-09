using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class DashDictionaryTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypograf(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("5-10 января", "5\u201410 января")]
    [InlineData("с 1-3 марта", "с 1\u20143 марта")]
    // Без месяца это диапазон чисел, а не дней.
    [InlineData("5-10 штук", "5-10 штук")]
    public void WritesDashBetweenDaysOfOneMonth(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Dash.DaysMonth));

    [Theory]
    [InlineData("январь-февраль", "январь\u2014февраль")]
    [InlineData("Май-июнь", "Май\u2014июнь")]
    // Одно слово словарное, второе нет — не интервал месяцев.
    [InlineData("январь-снег", "январь-снег")]
    public void WritesDashBetweenMonths(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Dash.Month));

    [Theory]
    [InlineData("понедельник-среда", "понедельник\u2014среда")]
    [InlineData("пятница-суббота", "пятница\u2014суббота")]
    public void WritesDashBetweenWeekdays(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Dash.Weekday));

    [Fact]
    public void KeepsHyphenInDoubleSurname()
    {
        // Двойная фамилия пишется через дефис, и ни одно правило тире не должно его трогать:
        // пробелов вокруг нет, словарных слов нет, цифр нет.
        var typograf = new TextTypograf();
        Assert.Equal("Салтыков-Щедрин", typograf.Process("Салтыков-Щедрин"));
        Assert.Equal("Римский-Корсаков", typograf.Process("Римский-Корсаков"));

        // А вот дефис с пробелами между теми же словами — это тире.
        Assert.Contains("\u2014", typograf.Process("Иванов - Петров"));
    }
}
