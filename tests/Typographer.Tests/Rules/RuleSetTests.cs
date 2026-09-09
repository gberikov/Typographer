using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class RuleSetTests
{
    [Fact]
    public void Default_IncludesDashesAndQuotes()
    {
        Assert.True(RuleSet.Default.Contains(RuleId.Ru.Dash.Main));
        Assert.True(RuleSet.Default.Contains(RuleId.Common.Punctuation.Quote));
    }

    [Fact]
    public void Default_ExcludesRiskyRules()
    {
        Assert.False(RuleSet.Default.Contains(RuleId.Ru.Typo.SwitchingKeyboardLayout));
        Assert.False(RuleSet.Default.Contains(RuleId.Ru.Punctuation.Ano));
    }

    [Fact]
    public void Without_DoesNotMutateSourceSet()
    {
        RuleSet reduced = RuleSet.Default.Without(RuleId.Ru.Dash.Main);

        Assert.False(reduced.Contains(RuleId.Ru.Dash.Main));
        Assert.True(RuleSet.Default.Contains(RuleId.Ru.Dash.Main));
    }

    [Fact]
    public void With_AddsDisabledRule()
    {
        RuleSet extended = RuleSet.Default.With(RuleId.Ru.Typo.SwitchingKeyboardLayout);

        Assert.True(extended.Contains(RuleId.Ru.Typo.SwitchingKeyboardLayout));
    }

    [Fact]
    public void None_IsEmpty_All_IsFull()
    {
#pragma warning disable xUnit2013
        Assert.Equal(0, RuleSet.None.Count);
#pragma warning restore xUnit2013
        Assert.True(RuleSet.All.Count >= RuleSet.Default.Count);
    }

    [Theory]
    [InlineData("ru/dash/main")]
    [InlineData("common/punctuation/quote")]
    public void TryParse_ParsesJsTypografStyleName(string name)
    {
        Assert.True(RuleId.TryParse(name, out RuleId rule));
        Assert.Equal(name, rule.Name);
    }

    [Fact]
    public void TryParse_RejectsUnknownName()
    {
        Assert.False(RuleId.TryParse("ru/nbsp/abr", out _));
    }

    [Fact]
    public void Default_IsNotEmpty()
    {
        // Регресс на сдвиг индексов реестра: если ноль случайно снова окажется занят
        // настоящим правилом или маска съедет, этот тест ловит эффект первым.
        Assert.True(RuleSet.Default.Count > 0);
    }

    [Fact]
    public void DefaultRuleId_MatchesNoRule()
    {
        var empty = default(RuleId);

        Assert.False(RuleSet.All.Contains(empty));
        Assert.NotEqual(RuleId.Common.Punctuation.Quote, empty);
    }

    [Fact]
    public void DefaultRuleId_NameIsEmptyStringNotNull()
    {
        var empty = default(RuleId);

        Assert.Equal(string.Empty, empty.Name);
        Assert.Equal(string.Empty, empty.ToString());
    }

    [Fact]
    public void With_OnNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleSet.Default.With(null!));
    }

    [Fact]
    public void Without_OnNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleSet.Default.Without(null!));
    }

    [Fact]
    public void RegistryIndicesAreUniqueAndFitTheMask()
    {
        // Индекс правила — позиция бита в маске RuleSet: два ulong, 128 бит, индекс 0
        // зарезервирован за default(RuleId). Дубликат индекса молча склеил бы два правила
        // в одно, а индекс за пределом маски — включил бы чужое правило.
        var seen = new HashSet<int>();
        foreach (RuleId rule in RuleId.Registry.All)
        {
            Assert.InRange(rule.Index, 1, 127);
            Assert.True(seen.Add(rule.Index), $"индекс {rule.Index} занят дважды: {rule.Name}");
        }

        Assert.Equal(RuleId.Registry.All.Length, seen.Count);
    }

    [Fact]
    public void RegistryNamesAreUnique()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (RuleId rule in RuleId.Registry.All)
        {
            Assert.True(seen.Add(rule.Name), $"имя {rule.Name} занято дважды");
        }
    }

    [Fact]
    public void RegistryHasEveryScanRuleOfTheSpec()
    {
        // Реестр — источник имён для docs/rules.md и для RuleId.TryParse. Пропущенное имя
        // означает, что правило нельзя включить по имени, даже если код его реализует.
        Assert.Equal(65, RuleId.Registry.All.Length);
        Assert.True(RuleId.TryParse("common/space/afterColon", out _));
        Assert.True(RuleId.TryParse("ru/dash/kakto", out _));
        Assert.True(RuleId.TryParse("en-GB/dash/main", out _));
    }

    [Fact]
    public void NormalizationRulesAreOutOfDefault()
    {
        // Обрезка краёв и замена табов меняют текст за пределами оформления: тот, кто
        // вызвал Typograf.Html(text), такого не ожидает.
        foreach (RuleId rule in RuleId.Registry.Normalization)
        {
            Assert.False(RuleSet.Default.Contains(rule), rule.Name);
            Assert.True(RuleSet.All.Contains(rule), rule.Name);
        }
    }
}
