using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Typographer.AspNetCore;

namespace Typographer.Integrations.Tests;

public class AspNetCoreTests
{
    private const string Nbsp = "\u00A0";

    [Fact]
    public async Task ProcessAsync_ТипографируетСодержимое()
    {
        TagHelperOutput output = MakeOutput("<p>Он - человек</p>");

        await new TypografTagHelper(HtmlTypograf.Default).ProcessAsync(MakeContext(), output);

        string result = output.Content.GetContent();
        Assert.Contains("—", result);
        Assert.DoesNotContain(" - ", result);
    }

    // Сам элемент <typograf> — инструкция шаблонизатору, а не разметка страницы,
    // и в выводе его быть не должно.
    [Fact]
    public async Task ProcessAsync_УбираетСобственныйТег()
    {
        TagHelperOutput output = MakeOutput("текст");

        await new TypografTagHelper(HtmlTypograf.Default).ProcessAsync(MakeContext(), output);

        Assert.Null(output.TagName);
    }

    [Fact]
    public async Task ProcessAsync_РазметкуВнутриНеТрогает()
    {
        TagHelperOutput output = MakeOutput("<a href=\"http://a.example/x--y\">ссылка</a>");

        await new TypografTagHelper(HtmlTypograf.Default).ProcessAsync(MakeContext(), output);

        Assert.Contains("http://a.example/x--y", output.Content.GetContent());
    }

    [Fact]
    public void ToHtmlContent_ВозвращаетГотовуюРазметку()
    {
        IHtmlContent content = HtmlTypograf.Default.ToHtmlContent("Он - человек");

        Assert.Contains(Nbsp + "—", content.ToString());
    }

    [Fact]
    public void ToHtmlContent_NullДаётПустуюРазметку()
    {
        Assert.Equal(string.Empty, HtmlTypograf.Default.ToHtmlContent(null).ToString());
    }

    private static TagHelperOutput MakeOutput(string childHtml)
        => new(
            "typograf",
            [],
            (useCachedResult, encoder) =>
            {
                var content = new DefaultTagHelperContent();
                content.SetHtmlContent(childHtml);
                return Task.FromResult<TagHelperContent>(content);
            });

    private static TagHelperContext MakeContext()
        => new([], new Dictionary<object, object>(), "тест");
}
