using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Фаза Prepare: вход приводится к одному представлению до того, как правила начнут читать
/// соседние символы. Без неё правило видит «амперсанд» там, где стоит неразрывный пробел,
/// и «метку порядка байт» там, где начинается документ.
/// </summary>
/// <remarks>
/// Метка порядка байт невидима глазом, поэтому строится из константы
/// (<see cref="Chars.Bom"/>), а не вставляется литералом в исходник.
/// </remarks>
public class PrepareTests
{
    private static string Html(string source) => new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.Default,
    }).Process(source);

    private static string Text(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.Default,
    }).Process(source);

    [Fact]
    public void NbspEntityVisibleToDashRule()
        => Assert.Equal($"слово{Chars.Nbsp}{Chars.MDash} слово", Html("слово&nbsp;- слово"));

    [Fact]
    public void NumericEntityDecodesTheSame()
        => Assert.Equal($"слово{Chars.Nbsp}{Chars.MDash} слово", Html("слово&#160;- слово"));

    [Theory]
    [InlineData("&#+160;")]
    [InlineData("&# 160;")]
    public void MalformedNumericEntityStaysText(string source)
        => Assert.Equal(source, Html(source));

    // Сущности разметки несут смысл разметки и проходят фазу насквозь: декодированный
    // «&lt;» стал бы началом тега, а «&amp;» — началом другой сущности.
    [Fact]
    public void MarkupEntityIsNotDecoded()
        => Assert.Equal("клён &amp; берёза &lt;тут&gt;", Html("клён &amp; берёза &lt;тут&gt;"));

    [Fact]
    public void BomIsRemovedAndDoesNotHideDocumentStart()
        => Assert.Equal(
            $"{Chars.MDash} Привет,{Chars.Nbsp}{Chars.MDash} сказал он.",
            Text(Chars.Bom + "- Привет, - сказал он."));

    [Fact]
    public void BomIsRemovedInHtmlToo()
        => Assert.Equal("текст", Html(Chars.Bom + "текст"));

    [Theory]
    [InlineData("<\uFEFFb>text</b>")]
    [InlineData("<\uFEFFscript>alert(1)</script>")]
    [InlineData("&\uFEFFnbsp;")]
    [InlineData("<b>\uFEFFtext</b>")]
    [InlineData("text\uFEFFword")]
    public void InnerBomCreatesNoMarkupOrEntities(string source)
    {
        // Набор правил намеренно узкий. Предмет теста — метка порядка байт: она не должна
        // склеить соседние символы в тег или сущность. Полный Default сюда не годится: на
        // этих патологических входах есть что менять и другим правилам (например, «alert(1)»
        // законно получает пробел перед скобкой), и тест перестал бы говорить о метке.
        // Пустой набор тоже не годится: фаза Prepare при нулевом наборе не запускается вовсе.
        RuleSet rules = RuleSet.None.With(RuleId.Common.Punctuation.Quote);
        var html = new HtmlTypograf(new HtmlOptions { Rules = rules });
        var text = new TextTypograf(new TextOptions { Rules = rules });

        Assert.Equal(source, html.Process(source));
        Assert.Equal(source, html.Process(html.Process(source)));
        Assert.Equal(source, text.Process(source));
        Assert.Equal(
            $"<p>{source}</p>",
            new HtmlTypograf(new HtmlOptions { Rules = rules, UseP = true }).Process(source));
    }

    [Fact]
    public void LeadingBomsRemovedBeforeMarkup()
    {
        string source = $"{Chars.Bom}{Chars.Bom}<b>text</b>";

        Assert.Equal("<b>text</b>", Html(source));
        Assert.Equal("<b>text</b>", Text(source));
    }

    // Фаза существует ради правил: без правил готовить вход не для кого, и типограф обязан
    // вернуть его байт в байт.
    [Fact]
    public void NoRulesSkipsPreparation()
    {
        string source = Chars.Bom + "слово&nbsp;- слово";

        Assert.Equal(source, new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None }).Process(source));
    }

    [Fact]
    public void EntityDecodedInEverySegmentNotOnlyTheFirst()
    {
        // Сущность во ВТОРОМ текстовом узле доходит до правила тире так же, как в первом:
        // документный проход не должен зависеть от номера сегмента.
        string result = new HtmlTypograf(new HtmlOptions
        {
            Rules = RuleSet.None.With(RuleId.Ru.Dash.Main),
        }).Process("<b>раз</b> два&nbsp;- три");

        // Правило тире отбивает левый пробел неразрывным — им и оказывается
        // раскодированный &nbsp;.
        Assert.Equal($"<b>раз</b> два{Chars.Nbsp}{Chars.MDash} три", result);
    }

    [Fact]
    public void BomInsideSecondSegmentIsKept()
    {
        // Метка порядка байт снимается только в начале ДОКУМЕНТА: внутри текста её
        // удаление могло бы склеить соседние символы в тег или сущность.
        string result = new HtmlTypograf(new HtmlOptions
        {
            Rules = RuleSet.Default,
        }).Process($"<b>раз</b>{Chars.Bom}два");

        Assert.Contains(Chars.Bom, result);
    }
}
