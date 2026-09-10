using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Висячая пунктуация. Правило только размечает знак; выносит его за край набора CSS на
/// стороне сайта, поэтому имена классов взяты у JS-typograf дословно.
/// </summary>
public class OptAlignTests
{
    private static string Run(string source, params RuleId[] rules)
        => new HtmlTypographer(new HtmlOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("«цитата»", "<span class=\"typograf-oa-n-lquote\">«</span>цитата»")]
    [InlineData(
        "он сказал «да»",
        "он сказал<span class=\"typograf-oa-sp-lquote\"> </span><span class=\"typograf-oa-lquote\">«</span>да»")]
    [InlineData("„второй уровень“", "<span class=\"typograf-oa-n-lquote\">„</span>второй уровень“")]
    // Закрывающая кавычка не висит: за край выносится ЛЕВЫЙ знак.
    [InlineData("текст» дальше", "текст» дальше")]
    public void OpeningQuoteHangs(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.OptAlign.Quote));

    [Theory]
    [InlineData("(текст)", "<span class=\"typograf-oa-n-lbracket\">(</span>текст)")]
    [InlineData(
        "слово (текст)",
        "слово<span class=\"typograf-oa-sp-lbracket\"> </span><span class=\"typograf-oa-lbracket\">(</span>текст)")]
    public void OpeningBracketHangs(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.OptAlign.Bracket));

    [Theory]
    [InlineData(
        "раз, два",
        "раз<span class=\"typograf-oa-comma\">,</span><span class=\"typograf-oa-comma-sp\"> </span>два")]
    // За запятой нет пробела — вешать нечего.
    [InlineData("раз,два", "раз,два")]
    // Слева нет буквы: запятая в начале строки не край набора.
    [InlineData(", два", ", два")]
    public void CommaHangs(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.OptAlign.Comma));

    // Второй прогон не должен вкладывать теги друг в друга.
    [Fact]
    public void OptAlignIsIdempotent()
    {
        RuleId[] all = [RuleId.Ru.OptAlign.Quote, RuleId.Ru.OptAlign.Bracket, RuleId.Ru.OptAlign.Comma];
        string once = Run("«Цитата» и (скобка), раз, два", all);

        Assert.Equal(once, Run(once, all));
    }

    // Пробел слева от знака лежит в ПРЕДЫДУЩЕМ текстовом узле, а между ними тег: забирать
    // его из буфера нельзя — там байты открывающего тега. Правило отказывается целиком.
    [Fact]
    public void HangingDoesNotReachBehindMarkup()
        => Assert.Equal(
            "слово <b>«цитата»</b>",
            Run("слово <b>«цитата»</b>", RuleId.Ru.OptAlign.Quote));

    // А вот пробел, записанный этим же проходом в том же узле, забирать можно: он текст.
    [Fact]
    public void HangingTakesTheSpaceOfItsOwnNode()
        => Assert.Equal(
            "<b>слово</b><span class=\"typograf-oa-sp-lquote\"> </span>"
            + "<span class=\"typograf-oa-lquote\">«</span>цитата»",
            Run("<b>слово</b> «цитата»", RuleId.Ru.OptAlign.Quote));
}
