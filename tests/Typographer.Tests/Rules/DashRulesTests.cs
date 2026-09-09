using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class DashRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Ru.Dash.Main)
            .With(RuleId.Ru.Dash.DirectSpeech)
            .With(RuleId.Ru.Dash.Years),
    }).Process(source);

    [Fact]
    public void SpacedHyphenBetweenWordsBecomesDash()
        => Assert.Equal("Он — человек", Run("Он - человек"));

    [Theory]
    [InlineData("из-за")]
    [InlineData("по-русски")]
    [InlineData("кто-то")]
    [InlineData("из-под")]
    public void HyphenInsideWordUntouched(string source)
        => Assert.Equal(source, Run(source));

    [Fact]
    public void YearRangeUsesEnDashWithoutSpaces()
        => Assert.Equal("1941—1945", Run("1941-1945"));

    [Fact]
    public void DirectSpeechDashAtLineStart()
        => Assert.Equal("— Привет, — сказал он.", Run("- Привет, - сказал он."));

    [Fact]
    public void MinusBeforeNumberDoesNotBecomeDash()
        => Assert.Equal("от -5 до +5", Run("от -5 до +5"));

    [Fact]
    public void LeavesHyphenInUrlAlone()
        => Assert.Equal("http://example.com/a-b", Run("http://example.com/a-b"));

    // Диапазон — это ГОДЫ: по четыре цифры с каждой стороны. Без счёта цифр правило
    // срабатывало на любой паре «цифра — дефис — цифра» и рвало телефоны, даты и ссылки.
    [Theory]
    [InlineData("+7-999-123-45-67")]
    [InlineData("01-01-2020")]
    [InlineData("http://example.com/2024-01-15/post")]
    [InlineData("ISBN 978-5-17-090000-0")]
    [InlineData("8-800")]
    public void NumericHyphensOutsideYearRangeUntouched(string source)
        => Assert.Equal(source, Run(source));

    // Правый контекст читался по фиксированному смещению, поэтому лишний пробел перед
    // числом молча превращал минус в тире: решение принималось до того, как правило
    // «удалить повторный пробел» схлопывало пробелы.
    [Theory]
    [InlineData("текст - 5")]
    [InlineData("текст -  5")]
    [InlineData("текст -   5")]
    public void SpacesBeforeNumberDoNotChangeHyphenDecision(string source)
        => Assert.Equal("текст - 5", new TextTypograf(new TextOptions
        {
            Rules = RuleSet.None
                .With(RuleId.Ru.Dash.Main)
                .With(RuleId.Common.Space.DelRepeatSpace),
        }).Process(source));

    [Fact]
    public void YearRangeInsideSentence()
        => Assert.Equal("война 1941—1945 годов", Run("война 1941-1945 годов"));

    [Fact]
    public void YearRangeRecognizedAcrossInlineTag()
        => Assert.Equal("<b>1941</b>—1945", HtmlTypograf.Default.Process("<b>1941</b>-1945"));

    [Fact]
    public void SpacedHyphenAfterTagDoesNotPatchIntoMarkup()
    {
        // Правило тире патчит пробел слева от дефиса, а он может лежать ЗА тегом — в чужой
        // части общего буфера. Патч левее своего текстового узла переписал бы «>» тега
        // неразрывным пробелом и сломал разметку, которую гарантия 3 обещает байт в байт.
        string result = new HtmlTypograf(new HtmlOptions
        {
            Rules = RuleSet.None.With(RuleId.Ru.Dash.Main),
        }).Process("раз <b>- два</b>");

        Assert.Equal("раз <b>— два</b>", result);
    }
}
