using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Internal;

public class EmitterTests
{
    private static string Encode(string source, EntityMode mode)
    {
        var buffer = new CharBuffer(source.Length);
        try
        {
            Emitter.Encode(source.AsSpan(), mode, ref buffer);
            return buffer.AsSpan().ToString();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Fact]
    public void Symbols_LeavesCharsAsIs()
    {
        string input = $"{Chars.Laquo}а{Chars.Nbsp}б{Chars.Raquo}";
        Assert.Equal(input, Encode(input, EntityMode.Symbols));
    }

    [Fact]
    public void Named_EncodesWithNamedEntities()
    {
        string input = $"{Chars.Laquo}а{Chars.Nbsp}б{Chars.Raquo}";
        Assert.Equal("&laquo;а&nbsp;б&raquo;", Encode(input, EntityMode.Named));
    }

    [Fact]
    public void Numeric_EncodesWithNumericCodes()
    {
        string input = $"{Chars.Laquo}а{Chars.Nbsp}б{Chars.Raquo}";
        Assert.Equal("&#171;а&#160;б&#187;", Encode(input, EntityMode.Numeric));
    }

    [Fact]
    public void Mixed_EncodesOnlyInvisible()
    {
        string input = $"{Chars.Laquo}а{Chars.Nbsp}б{Chars.Raquo}";
        Assert.Equal($"{Chars.Laquo}а&nbsp;б{Chars.Raquo}", Encode(input, EntityMode.Mixed));
    }

    [Fact]
    public void LeavesOrdinaryCharsAlone()
        => Assert.Equal("<b>текст</b> & ещё", Encode("<b>текст</b> & ещё", EntityMode.Named));

    [Fact]
    public void DocumentEncodingSkipsMarkupAndProtectedZones()
    {
        // Кавычка-ёлочка в значении атрибута и внутри <code> сущностью не становится:
        // гарантия 3 обещает разметку и защищённые зоны байт в байт.
        string result = new HtmlTypograf(new HtmlOptions
        {
            Rules = RuleSet.None,
            Entities = EntityMode.Named,
        }).Process("<a title=\"«х»\">«текст»</a><code>«код»</code>");

        Assert.Equal(
            "<a title=\"«х»\">&laquo;текст&raquo;</a><code>«код»</code>",
            result);
    }
}
