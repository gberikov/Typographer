using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Знак номера, параграфа и абзаца отбиваются от того, что за ними следует. Правило
/// ВСТАВЛЯЕТ символ, которого во входе не было, поэтому оно символьное: токеном знак не
/// выражается, а решение зависит от правого контекста в документе.
/// </summary>
public class MarkRulesTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypographer(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    private static string Html(string source, params RuleId[] rules)
        => new HtmlTypographer(new HtmlOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Fact]
    public void NumberSignGetsNarrowSpace()
    {
        Assert.Equal($"№{Chars.NarrowNbsp}5", Run("№ 5", RuleId.Ru.Nbsp.AfterNumberSign));
        Assert.Equal($"№{Chars.NarrowNbsp}5", Run("№5", RuleId.Ru.Nbsp.AfterNumberSign));
        Assert.Equal($"№{Chars.NarrowNbsp}дома", Run("№ дома", RuleId.Ru.Nbsp.AfterNumberSign));
    }

    [Fact]
    public void NumberSignRuleIsIdempotent()
    {
        string once = Run("№ 5", RuleId.Ru.Nbsp.AfterNumberSign);
        Assert.Equal(once, Run(once, RuleId.Ru.Nbsp.AfterNumberSign));
    }

    [Theory]
    // Справа ничего нет — отбивать нечего.
    [InlineData("№")]
    // Справа знак препинания: это не номер, а сам знак как слово.
    [InlineData("№, потом")]
    public void NumberSignStaysAlone(string source)
        => Assert.Equal(source, Run(source, RuleId.Ru.Nbsp.AfterNumberSign));

    [Fact]
    public void SectionMarkGetsNarrowSpace()
    {
        Assert.Equal($"§{Chars.NarrowNbsp}3", Run("§ 3", RuleId.Common.Nbsp.AfterSectionMark));
        Assert.Equal($"§{Chars.NarrowNbsp}3", Run("§3", RuleId.Common.Nbsp.AfterSectionMark));
    }

    // Знак абзаца получает ОБЫЧНЫЙ неразрывный пробел: узкая отбивка в ГОСТ 16.4 предписана
    // знакам номера и параграфа, про знак абзаца там нет ничего, а JS-typograf ставит обычный.
    [Fact]
    public void ParagraphMarkGetsRegularNbsp()
    {
        Assert.Equal($"¶{Chars.Nbsp}3", Run("¶ 3", RuleId.Common.Nbsp.AfterParagraphMark));
        Assert.Equal($"¶{Chars.Nbsp}3", Run("¶3", RuleId.Common.Nbsp.AfterParagraphMark));
    }

    [Fact]
    public void MarksAreUntouchedWithoutTheirRules()
        => Assert.Equal("№ 5 и § 3 и ¶ 3", Run("№ 5 и § 3 и ¶ 3", RuleId.Common.Punctuation.Quote));

    // Тег nobr уже запрещает перенос внутри себя, и неразрывный пробел там лишний: он
    // оставляет невидимый символ на месте пробела. Правило снимает те, что были во входе,
    // и не даёт правилам фазы поставить новые.
    [Fact]
    public void NoWrapZoneLosesNonBreakingSpaces()
        => Assert.Equal(
            "<nobr>в доме</nobr>",
            Html($"<nobr>в{Chars.Nbsp}доме</nobr>", RuleId.Common.Nbsp.Nowrap, RuleId.Common.Nbsp.AfterShortWord));

    [Fact]
    public void NoWrapZoneGetsNoNewGlue()
        => Assert.Equal(
            "<nowrap>в доме</nowrap>",
            Html("<nowrap>в доме</nowrap>", RuleId.Common.Nbsp.Nowrap, RuleId.Common.Nbsp.AfterShortWord));

    [Fact]
    public void GlueOutsideNoWrapZoneStillWorks()
        => Assert.Equal(
            $"<nobr>раз</nobr> в{Chars.Nbsp}доме",
            Html("<nobr>раз</nobr> в доме", RuleId.Common.Nbsp.Nowrap, RuleId.Common.Nbsp.AfterShortWord));

    // Цепочки, которые создаёт сама опция MaxNobr, правило не разбирает: она работает в фазе
    // Layout, уже после Bind, и её неразрывные пробелы правилу не видны.
    [Fact]
    public void NobrCreatedByOptionKeepsItsGlue()
    {
        string result = new HtmlTypographer(new HtmlOptions
        {
            Rules = RuleSet.None.With(RuleId.Common.Nbsp.Nowrap, RuleId.Common.Nbsp.AfterShortWord),
            MaxNobr = 3,
        }).Process("в доме на горе");

        Assert.Contains("<nobr>", result);
        Assert.Contains(Chars.Nbsp.ToString(), result);
    }
}
