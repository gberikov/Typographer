using System.Buffers;
using Typographer.Internal;
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

    [Fact]
    public void КодированиеТолькоВТекстовыхСегментахНamedМодус()
    {
        // Тест проверяет, что кодирование применяется ТОЛЬКО к текстовым сегментам,
        // и не трогает разметку и защищённые зоны. Это критично для корректности.
        var typograf = new HtmlTypograf(new HtmlOptions
        {
            Rules = RuleSet.None,
            Entities = EntityMode.Named
        });

        // HTML содержит ОДИН И ТОТ ЖЕ типографский символ (NBSP) в трёх местах:
        // 1. В текстовом узле — ДОЛЖЕН быть закодирован в &nbsp;
        // 2. В атрибуте тега — НЕ ДОЛЖЕН быть закодирован (разметка)
        // 3. В защищённой зоне <code> — НЕ ДОЛЖЕН быть закодирован (защита)
        string input = $"text{Chars.Nbsp}end<a title=\"attr{Chars.Nbsp}value\">link</a><code>code{Chars.Nbsp}here</code>";
        string expected = $"text&nbsp;end<a title=\"attr{Chars.Nbsp}value\">link</a><code>code{Chars.Nbsp}here</code>";

        Assert.Equal(expected, typograf.Process(input));
    }

    [Fact]
    public void СимволыРежимНеМенятРазметкуИЗащиту()
    {
        // Режим Symbols не должен ничего менять, даже если есть типографские символы.
        var typograf = new HtmlTypograf(new HtmlOptions
        {
            Rules = RuleSet.None,
            Entities = EntityMode.Symbols
        });

        string input = $"text{Chars.Nbsp}end<a title=\"attr{Chars.Nbsp}value\">link</a><code>code{Chars.Nbsp}here</code>";

        Assert.Equal(input, typograf.Process(input));
    }
}
