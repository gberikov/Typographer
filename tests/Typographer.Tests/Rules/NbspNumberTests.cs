using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Склейка числа с тем, что за ним следует. Неразрывный пробел в ожиданиях записан
/// escape-последовательностью: в <c>[InlineData]</c> интерполяция недоступна, а сам символ
/// в исходнике невидим и ревью его не поймает.
/// </summary>
public class NbspNumberTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypograf(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("10 кг", "10 кг")]
    [InlineData("100 км/ч", "100 км/ч")]
    [InlineData("30 мин.", "30 мин.")]
    [InlineData("дом 5", "дом 5")]
    [InlineData("2026 2027", "2026 2027")]
    public void NumberBindsToFollowingWord(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Nbsp.AfterNumber));

    [Theory]
    [InlineData("5 января", "5 января")]
    [InlineData("31 декабря", "31 декабря")]
    [InlineData("5 январь", "5 январь")]
    [InlineData("5 яблок", "5 яблок")]
    public void DayBindsToMonthWithoutAfterNumber(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.DayMonth));

    [Theory]
    [InlineData("2012 г.", "2012 г.")]
    [InlineData("1990 гг.", "1990 гг.")]
    [InlineData("дом г.", "дом г.")]
    [InlineData("5 г.", "5 г.")]
    public void YearBindsToAbbreviation(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.Year));

    [Theory]
    [InlineData("10 млн", "10 млн")]
    [InlineData("5 тыс. рублей", "5 тыс. рублей")]
    [InlineData("3 млрд", "3 млрд")]
    [InlineData("много млн", "много млн")]
    public void NumberBindsToMagnitude(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.Mln));

    [Theory]
    [InlineData("100 руб.", "100 руб.")]
    [InlineData("50 коп.", "50 коп.")]
    [InlineData("5 000 р.", "5 000 р.")]
    [InlineData("руб. за штуку", "руб. за штуку")]
    public void NumberBindsToMoneyAbbreviation(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.RubleKopek));

    [Theory]
    [InlineData("600 dpi", "600 dpi")]
    [InlineData("150 lpi", "150 lpi")]
    [InlineData("600 dpiX", "600 dpiX")]
    public void NumberBindsToResolution(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Nbsp.Dpi));

    /// <summary>
    /// Разряды, разбитые пробелами самим автором. Правило числится за фазой Scan, но вторая
    /// его половина работает в фазе Bind: пробел разделяет два токена, а токенов фаза Scan
    /// не знает. Поэтому правило проверяется В ОДИНОЧКУ — так ловится случай, когда проход
    /// фазы Bind вовсе не запускался и правило молча ничего не делало.
    /// </summary>
    [Theory]
    [InlineData("1 000 000", "1\u00A0000\u00A0000")]
    [InlineData("Цена 1 000 рублей", "Цена 1\u00A0000 рублей")]
    // Точка конца предложения входит в токен, но разряд от этого разрядом быть не перестаёт.
    [InlineData("1 000 000.", "1\u00A0000\u00A0000.")]
    // Перечисление чисел разрядами не является: рвать его можно.
    [InlineData("в 1941 1945", "в 1941 1945")]
    // Нумерованный пункт и следующее за ним число — тоже не разряды одного числа.
    [InlineData("1. 000", "1. 000")]
    // Разряд — ровно три цифры; «1 0000» набрано с ошибкой, и додумывать её мы не беремся.
    [InlineData("1 0000", "1 0000")]
    public void AuthorsDigitGroupsBecomeNonBreaking(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Number.DigitGrouping));

    /// <summary>
    /// Знак единицы измерения после числа (ГОСТ 9.6). Обычная склейка сюда не достаёт: знак
    /// не буква и не цифра, токеном он не становится, а буква за ним от числа уже отрезана.
    /// </summary>
    [Theory]
    [InlineData("25 °C", "25\u00A0°C")]
    [InlineData("50 %", "50\u00A0%")]
    [InlineData("10 ‰", "10\u00A0‰")]
    // Пробел обязан стоять вплотную к знаку: в «30 15°» градус относится к «15».
    [InlineData("угол 30 15°", "угол 30 15°")]
    // Слева не число — связывать нечего.
    [InlineData("около °C", "около °C")]
    public void UnitSignBindsToNumber(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Nbsp.AfterNumber));
}
