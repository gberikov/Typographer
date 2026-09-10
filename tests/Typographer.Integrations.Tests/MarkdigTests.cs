using Markdig;
using Typographer.Markdig;

namespace Typographer.Integrations.Tests;

public class MarkdigTests
{
    private const string Nbsp = "\u00A0";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseTypograf()
        .Build();

    [Fact]
    public void UseTypograf_СтавитТиреИНеразрывныйПробел()
    {
        Assert.Equal(
            $"<p>Он{Nbsp}— человек</p>\n",
            Markdown.ToHtml("Он - человек", Pipeline));
    }

    // Ради этого случая куски абзаца и собираются в одну строку: поодиночке вторая
    // кавычка не знает, что слева от неё что-то было, и открылась бы второй раз.
    [Fact]
    public void UseTypograf_КавычкиВокругРазметкиСмотрятВРазныеСтороны()
    {
        Assert.Equal(
            "<p>«<strong>слово</strong>»</p>\n",
            Markdown.ToHtml("\"**слово**\"", Pipeline));
    }

    [Fact]
    public void UseTypograf_КодНеТрогает()
    {
        Assert.Equal(
            "<p><code>a - b</code></p>\n",
            Markdown.ToHtml("`a - b`", Pipeline));
    }

    [Fact]
    public void UseTypograf_БлокКодаНеТрогает()
    {
        Assert.Contains("a - b", Markdown.ToHtml("```\na - b\n```", Pipeline));
    }

    [Fact]
    public void UseTypograf_АдресСсылкиНеТрогает()
    {
        string html = Markdown.ToHtml("[текст - тут](http://a.example/x--y)", Pipeline);

        Assert.Contains("http://a.example/x--y", html);
        Assert.Contains("текст" + Nbsp + "—", html);
    }

    [Fact]
    public void БезРасширенияНичегоНеМеняется()
    {
        Assert.Equal("<p>Он - человек</p>\n", Markdown.ToHtml("Он - человек"));
    }
}
