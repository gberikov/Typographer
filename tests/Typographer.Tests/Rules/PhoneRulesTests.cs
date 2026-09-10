using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Телефонные номера. Правило в <see cref="RuleSet.Default"/> и переписывает цифры, поэтому
/// предикат узкий, а отрицательных случаев здесь больше, чем положительных.
/// </summary>
public class PhoneRulesTests
{
    private static readonly string Canonical =
        $"+7{Chars.Nbsp}999{Chars.Nbsp}123-45-67";

    private static string Run(string source)
        => new TextTypographer(new TextOptions
        {
            Rules = RuleSet.None.With(RuleId.Ru.Other.PhoneNumber),
        }).Process(source);

    [Theory]
    [InlineData("+7 (999) 123-45-67")]
    [InlineData("+79991234567")]
    [InlineData("+7-999-123-45-67")]
    [InlineData("+7 999 123 45 67")]
    public void RussianNumberBecomesCanonical(string source)
        => Assert.Equal(Canonical, Run(source));

    [Fact]
    public void EightPrefixKeepsItsForm()
    {
        string expected = $"8{Chars.Nbsp}999{Chars.Nbsp}123-45-67";
        Assert.Equal(expected, Run("8 (999) 123-45-67"));
        Assert.Equal(expected, Run("89991234567"));
    }

    [Fact]
    public void RuleIsIdempotent()
        => Assert.Equal(Canonical, Run(Canonical));

    [Theory]
    // Девять цифр и двенадцать — не номер.
    [InlineData("+7 999 123-45-6")]
    [InlineData("+7 999 123-45-678")]
    // Код страны не российский.
    [InlineData("+380 44 123 4567")]
    // Ни префикса, ни разделителей — просто число.
    [InlineData("1234567890")]
    // Двенадцатая цифра подряд отменяет разбор: это артикул.
    [InlineData("артикул 89991234567890")]
    // Цифры продолжаются за разделителем: длинное число, разбитое по разрядам.
    [InlineData("89 991 234 567 890")]
    // Одна цифра и слово: «8 марта» — дата, а не номер.
    [InlineData("8 марта")]
    public void OtherDigitRunsAreUntouched(string source)
        => Assert.Equal(source, Run(source));
}
