using Typographer.Internal;

namespace Typographer.Tests.Internal;

public class EntityTableTests
{
    [Theory]
    [InlineData("&laquo;текст", '«', 7)]
    [InlineData("&nbsp;", '\u00A0', 6)]
    [InlineData("&mdash;", '—', 7)]
    [InlineData("&#171;", '«', 6)]
    [InlineData("&#x00AB;", '«', 8)]
    public void TryDecode_РаспознаётТипографскиеСущности(string source, char expected, int expectedConsumed)
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
    public void TryDecode_ОтклоняетНетипографскиеИНезавершённые(string source)
    {
        Assert.False(EntityTable.TryDecode(source.AsSpan(), out _, out _));
    }

    [Fact]
    public void NameOf_ВозвращаетИмяДляТипографскогоСимвола()
    {
        Assert.Equal("nbsp", EntityTable.NameOf('\u00A0'));
        Assert.Equal("laquo", EntityTable.NameOf('«'));
        Assert.Null(EntityTable.NameOf('а'));
    }

    [Fact]
    public void IsInvisible_ТолькоПробельныеСимволы()
    {
        Assert.True(EntityTable.IsInvisible('\u00A0'));
        Assert.True(EntityTable.IsInvisible('\u202F'));
        Assert.False(EntityTable.IsInvisible('«'));
    }
}
