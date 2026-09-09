using Typographer.Internal;

namespace Typographer.Tests.Internal;

public class EntityTableTests
{
    [Theory]
    [InlineData("&laquo;текст", '\u00AB', 7)]
    [InlineData("&nbsp;", '\u00A0', 6)]
    [InlineData("&mdash;", '\u2014', 7)]
    [InlineData("&#171;", '\u00AB', 6)]
    [InlineData("&#x00AB;", '\u00AB', 8)]
    [InlineData("&ldquo;", '\u201C', 7)]
    [InlineData("&bdquo;", '\u201E', 7)]
    public void TryDecode_RecognizesTypographicEntities(string source, char expected, int expectedConsumed)
    {
        Assert.True(EntityTable.TryDecode(source.AsSpan(), out char value, out int consumed));
        Assert.Equal(expected, value);
        Assert.Equal(expectedConsumed, consumed);
    }

    [Theory]
    [InlineData("&amp;")]
    [InlineData("&lt;")]
    [InlineData("&gt;")]
    [InlineData("&nosuch;")]
    [InlineData("&nbsp")]
    [InlineData("&#34;")]
    [InlineData("&#38;")]
    [InlineData("&#60;")]
    [InlineData("&#62;")]
    [InlineData("&#x22;")]
    [InlineData("&#x26;")]
    [InlineData("&#x3C;")]
    [InlineData("&#x3E;")]
    [InlineData("&#+160;")]
    [InlineData("&#-160;")]
    [InlineData("&# 160;")]
    [InlineData("&#160 ;")]
    [InlineData("&#x A0;")]
    public void TryDecode_RejectsNonTypographicAndIncomplete(string source)
    {
        Assert.False(EntityTable.TryDecode(source.AsSpan(), out _, out _));
    }

    [Fact]
    public void NameOf_ReturnsNameForTypographicChar()
    {
        Assert.Equal("nbsp", EntityTable.NameOf('\u00A0'));
        Assert.Equal("laquo", EntityTable.NameOf('\u00AB'));
        Assert.Equal("ldquo", EntityTable.NameOf('\u201C'));
        Assert.Equal("bdquo", EntityTable.NameOf('\u201E'));
        Assert.Null(EntityTable.NameOf('а'));
        Assert.Null(EntityTable.NameOf('"'));
        Assert.Null(EntityTable.NameOf('&'));
        Assert.Null(EntityTable.NameOf('<'));
        Assert.Null(EntityTable.NameOf('>'));
    }

    [Fact]
    public void IsInvisible_OnlyWhitespaceChars()
    {
        Assert.True(EntityTable.IsInvisible('\u00A0'));
        Assert.True(EntityTable.IsInvisible('\u202F'));
        Assert.False(EntityTable.IsInvisible('\u00AB'));
    }

    [Fact]
    public void NarrowNbspHasNoNameButIsEncodable()
    {
        // Стандартного буквенного имени у U+202F нет, а невидимым символом в выводе
        // он остаться не должен: режим Named обязан дать числовой код.
        Assert.Null(EntityTable.NameOf(Chars.NarrowNbsp));
        Assert.True(EntityTable.IsEncodable(Chars.NarrowNbsp));
        Assert.True(EntityTable.IsInvisible(Chars.NarrowNbsp));
    }
}
