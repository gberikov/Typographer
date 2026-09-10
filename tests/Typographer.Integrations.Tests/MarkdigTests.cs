using Markdig;
using Typographer.Markdig;
using Typographer.Rules;

namespace Typographer.Integrations.Tests;

public class MarkdigTests
{
    private const string Nbsp = "\u00A0";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseTypograf()
        .Build();

    [Fact]
    public void UseTypograf_AddsDashAndNbsp()
    {
        Assert.Equal(
            $"<p>Он{Nbsp}— человек</p>\n",
            Markdown.ToHtml("Он - человек", Pipeline));
    }

    // Ради этого случая куски абзаца и собираются в одну строку: поодиночке вторая
    // кавычка не знает, что слева от неё что-то было, и открылась бы второй раз.
    [Fact]
    public void UseTypograf_QuotesAroundMarkupFaceOppositeWays()
    {
        Assert.Equal(
            "<p>«<strong>слово</strong>»</p>\n",
            Markdown.ToHtml("\"**слово**\"", Pipeline));
    }

    [Fact]
    public void UseTypograf_LeavesCodeIntact()
    {
        Assert.Equal(
            "<p><code>a - b</code></p>\n",
            Markdown.ToHtml("`a - b`", Pipeline));
    }

    [Fact]
    public void UseTypograf_LeavesCodeBlockIntact()
    {
        Assert.Contains("a - b", Markdown.ToHtml("```\na - b\n```", Pipeline));
    }

    [Fact]
    public void UseTypograf_LeavesLinkUrlIntact()
    {
        string html = Markdown.ToHtml("[текст - тут](http://a.example/x--y)", Pipeline);

        Assert.Contains("http://a.example/x--y", html);
        Assert.Contains("текст" + Nbsp + "—", html);
    }

    /// <summary>
    /// HTML-разметка внутри абзаца разбирается Markdig на теги и текст между ними, поэтому
    /// «&lt;code&gt;a - b&lt;/code&gt;» — три узла, средний из которых обычный текст. Без учёта
    /// защищённых зон типограф правил содержимое кода и клавиш: Markdown-код (`a - b`)
    /// защищён другим типом узла, а HTML-код не был защищён ничем.
    /// </summary>
    [Theory]
    [InlineData("<code>a - b</code>")]
    [InlineData("<kbd>Ctrl - C</kbd>")]
    [InlineData("<samp>x - y</samp>")]
    // Вложенный одноимённый элемент зону раньше времени не закрывает.
    [InlineData("<code>вло<code>a - b</code>жен</code>")]
    public void UseTypograf_LeavesProtectedHtmlZoneIntact(string markdown)
    {
        Assert.Contains(markdown, Markdown.ToHtml(markdown, Pipeline), StringComparison.Ordinal);
    }

    // Текст ВОКРУГ защищённой зоны типографируется как обычно.
    [Fact]
    public void UseTypograf_ProcessesTextAroundProtectedZone()
    {
        string html = Markdown.ToHtml("Слово - слово и <code>a - b</code> и снова - снова", Pipeline);

        Assert.Contains("<code>a - b</code>", html, StringComparison.Ordinal);
        Assert.Contains($"Слово{Nbsp}— слово", html, StringComparison.Ordinal);
        Assert.Contains($"снова{Nbsp}— снова", html, StringComparison.Ordinal);
    }

    // Прочая строчная разметка прозрачна: зоной она не является.
    [Fact]
    public void UseTypograf_TreatsPlainInlineMarkupAsUnprotected()
    {
        Assert.Contains(
            $"<b>жир{Nbsp}— жир</b>",
            Markdown.ToHtml("<b>жир - жир</b>", Pipeline),
            StringComparison.Ordinal);
    }

    // Одиночный тег содержимого не открывает и защиту не включает.
    [Fact]
    public void UseTypograf_SelfClosingTagOpensNoZone()
    {
        Assert.Contains(
            $"слово{Nbsp}— слово",
            Markdown.ToHtml("<code/>слово - слово", Pipeline),
            StringComparison.Ordinal);
    }

    // Закрывающий тег без открывающего не создаёт защищённую зону для следующего текста.
    [Fact]
    public void UseTypograf_StrayClosingTagOpensNoZone()
    {
        Assert.Contains(
            $"слово{Nbsp}— слово",
            Markdown.ToHtml("</code>слово - слово", Pipeline),
            StringComparison.Ordinal);
    }

    // Имя пользовательского элемента читается целиком, включая дефис: code-example не
    // является элементом code и содержимое не защищает.
    [Fact]
    public void UseTypograf_SimilarTagNameOpensNoProtectedZone()
    {
        Assert.Contains(
            $"<code-example>слово{Nbsp}— слово</code-example>",
            Markdown.ToHtml("<code-example>слово - слово</code-example>", Pipeline),
            StringComparison.Ordinal);
    }

    // В сыром содержимом script похожая на тег строка не меняет границы защищённой зоны.
    [Fact]
    public void UseTypograf_TagInsideScriptDoesNotExtendProtectedZone()
    {
        string markdown = "До <script>var x = '<code>';</script> после - после";
        string html = Markdown.ToHtml(markdown, Pipeline);

        Assert.Contains("<script>var x = '<code>';</script>", html, StringComparison.Ordinal);
        Assert.Contains($"после{Nbsp}— после", html, StringComparison.Ordinal);
    }

    // Сырой элемент, вложенный в другую защищённую зону, сам задаёт границы своего
    // содержимого: похожий на тег текст внутри JavaScript не меняет глубину внешнего code.
    [Fact]
    public void UseTypograf_TagInsideNestedScriptDoesNotExtendProtectedZone()
    {
        const string markdown =
            "До <code><script>var x = '<code>';</script>a - b</code> после - после";
        string html = Markdown.ToHtml(markdown, Pipeline);

        Assert.Contains("<code><script>var x = '<code>';</script>a - b</code>", html, StringComparison.Ordinal);
        Assert.Contains($"после{Nbsp}— после", html, StringComparison.Ordinal);
    }

    // Закрывающий тег внешней зоны, записанный строкой внутри script, не снимает защиту:
    // JavaScript и следующий текст code остаются байт в байт.
    [Fact]
    public void UseTypograf_ClosingTagInsideNestedScriptDoesNotEndProtectedZone()
    {
        const string markdown =
            "До <code><script>var x = '</code>'; a - b;</script>a - b</code> после - после";
        string html = Markdown.ToHtml(markdown, Pipeline);

        Assert.Contains(
            "<code><script>var x = '</code>'; a - b;</script>a - b</code>",
            html,
            StringComparison.Ordinal);
        Assert.Contains($"после{Nbsp}— после", html, StringComparison.Ordinal);
    }

    // HTML в подписи изображения живёт внутри собственного контейнера дерева и не может
    // открыть защищённую зону для текста, который следует после изображения.
    [Fact]
    public void UseTypograf_HtmlInImageLabelDoesNotProtectFollowingText()
    {
        string html = Markdown.ToHtml("![<code>подпись](image.png) текст - текст", Pipeline);

        Assert.Contains($"текст{Nbsp}— текст", html, StringComparison.Ordinal);
    }

    // Вырезанная защищённая зона остаётся объектом в связном тексте: правила обрезки краёв
    // не принимают пробел рядом с ней за начало или конец блока.
    [Theory]
    [InlineData("<code>x</code> текст", "<code>x</code> текст")]
    [InlineData("текст <code>x</code>", "текст <code>x</code>")]
    [InlineData("<code></code> текст", "<code></code> текст")]
    public void UseTypograf_TrimKeepsSpaceNextToProtectedZone(string markdown, string expected)
    {
        var typograf = new TextTypograf(new TextOptions
        {
            Rules = RuleSet.Default.With(RuleId.Common.Space.TrimLeft, RuleId.Common.Space.TrimRight),
        });
        MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseTypograf(typograf).Build();

        Assert.Contains(expected, Markdown.ToHtml(markdown, pipeline), StringComparison.Ordinal);
    }

    // Защищённый объект слева даёт закрывающий контекст прямой кавычке.
    [Fact]
    public void UseTypograf_ProtectedZoneProvidesContextForQuote()
        => Assert.Contains("<code>x</code>»", Markdown.ToHtml("<code>x</code>\"", Pipeline));

    [Fact]
    public void WithoutExtensionNothingChanges()
    {
        Assert.Equal("<p>Он - человек</p>\n", Markdown.ToHtml("Он - человек"));
    }
}
