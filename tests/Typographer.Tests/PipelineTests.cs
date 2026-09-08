using System.Buffers;
using Typographer.Rules;

namespace Typographer.Tests;

public class PipelineTests
{
    [Fact]
    public void БезПравилВозвращаетТотЖеЭкземплярСтроки()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None });
        string source = "<p>Он - человек</p>";

        Assert.Same(source, typograf.Process(source));
    }

    [Fact]
    public void БезПравилРазметкаКопируетсяБайтВБайт()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None });
        string source = "<a title=\"a>b\">x</a><code>a - b</code><!-- c -->";

        Assert.Equal(source, typograf.Process(source));
    }

    [Fact]
    public void ПишетВBufferWriter()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None });
        var writer = new ArrayBufferWriter<char>();

        typograf.Process("<p>текст</p>".AsSpan(), writer);

        Assert.Equal("<p>текст</p>", writer.WrittenSpan.ToString());
    }

    [Fact]
    public void ФасадРаботаетБезНастроек()
    {
        Assert.NotNull(Typograf.Html("текст"));
        Assert.NotNull(Typograf.PlainText("текст"));
    }

    [Fact]
    public void NullБросаетArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Typograf.Html(null!));
    }
}
