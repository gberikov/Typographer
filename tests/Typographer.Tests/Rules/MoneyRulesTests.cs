using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Деньги. Оба правила вне <see cref="RuleSet.Default"/>: замена «руб.» знаком рубля и
/// перестановка символа валюты меняют запись суммы, а не её оформление.
/// </summary>
public class MoneyRulesTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypographer(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Fact]
    public void CurrencyMovesBehindTheNumber()
    {
        Assert.Equal($"100{Chars.Nbsp}$", Run("$100", RuleId.Ru.Money.Currency));
        Assert.Equal($"50{Chars.Nbsp}€", Run("€50", RuleId.Ru.Money.Currency));
        Assert.Equal($"5,50{Chars.Nbsp}£", Run("£5,50", RuleId.Ru.Money.Currency));
    }

    // Порядок уже правильный: правило только делает пробел неразрывным. На этом же держится
    // его идемпотентность.
    [Fact]
    public void CurrencyAfterNumberOnlyGetsGlue()
    {
        Assert.Equal($"100{Chars.Nbsp}$", Run("100 $", RuleId.Ru.Money.Currency));
        Assert.Equal(
            $"100{Chars.Nbsp}$",
            Run($"100{Chars.Nbsp}$", RuleId.Ru.Money.Currency));
    }

    [Fact]
    public void CurrencyWithoutNumberStaysPut()
        => Assert.Equal("$ и €", Run("$ и €", RuleId.Ru.Money.Currency));

    [Fact]
    public void RubleAbbreviationBecomesSign()
    {
        Assert.Equal($"1{Chars.Nbsp}₽", Run("1 руб.", RuleId.Ru.Money.Ruble));
        Assert.Equal($"100{Chars.Nbsp}₽", Run("100 руб", RuleId.Ru.Money.Ruble));
    }

    [Fact]
    public void RubleAbbreviationNeedsANumber()
        => Assert.Equal("руб. за штуку", Run("руб. за штуку", RuleId.Ru.Money.Ruble));
}
