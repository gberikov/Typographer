using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Даты. Правило ISO-формата меняет ДАННЫЕ, а не оформление, поэтому его предикат жёсткий, и
/// отрицательных случаев здесь больше, чем положительных.
/// </summary>
public class DateRulesTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypographer(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("2018-10-10", "10.10.2018")]
    [InlineData("2026-09-10", "10.09.2026")]
    [InlineData("Дата 2018-10-10.", "Дата 10.10.2018.")]
    // Месяц больше двенадцати и день больше тридцати одного датой не бывают.
    [InlineData("2018-13-10", "2018-13-10")]
    [InlineData("2018-10-32", "2018-10-32")]
    // Диапазон лет, адрес и число из пяти цифр — не даты.
    [InlineData("1941-1945", "1941-1945")]
    [InlineData("192.168.0.1", "192.168.0.1")]
    [InlineData("12018-10-10", "12018-10-10")]
    [InlineData("2018-10-100", "2018-10-100")]
    public void IsoDateBecomesRussianForm(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Date.FromIso));

    [Theory]
    [InlineData("2 Мая", "2 мая")]
    [InlineData("9 Сентября 2026", "9 сентября 2026")]
    [InlineData("2 мая, Понедельник", "2 мая, понедельник")]
    // В начале предложения прописная буква стоит законно: признак «справа запятая»
    // понижал бы регистр и здесь.
    [InlineData("Понедельник, 9 сентября", "Понедельник, 9 сентября")]
    // Слева не число и справа не запятая: «Мая» может быть именем собственным.
    [InlineData("Мая много", "Мая много")]
    [InlineData("Москва", "Москва")]
    // Прописными набрано всё слово — это заголовок, а не ошибка регистра.
    [InlineData("2 МАЯ", "2 МАЯ")]
    public void MonthAndWeekdayGoLowercase(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Date.Weekday));
}
