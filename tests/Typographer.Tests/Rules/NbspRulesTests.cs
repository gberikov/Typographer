using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class NbspRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Nbsp.AfterShortWord)
            .With(RuleId.Ru.Nbsp.Abbr)
            .With(RuleId.Ru.Nbsp.Initials),
    }).Process(source);

    [Fact]
    public void ПослеКороткогоСловаНеразрывныйПробел()
        => Assert.Equal("в доме на горе", Run("в доме на горе"));

    [Fact]
    public void ДлинноеСловоНеТрогаем()
        => Assert.Equal("дерево стоит", Run("дерево стоит"));

    [Fact]
    public void СокращениеТДСклеивается()
        => Assert.Equal("и т. д.", Run("и т. д."));

    [Fact]
    public void ИнициалыПривязываютсяКФамилии()
        => Assert.Equal("А. С. Пушкин", Run("А. С. Пушкин"));

    [Fact]
    public void ФамилияПередИнициаламиТоже()
        => Assert.Equal("Пушкин А. С.", Run("Пушкин А. С."));
}
