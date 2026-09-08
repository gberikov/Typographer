using Typographer.Internal;

namespace Typographer.Tests.Internal;

public class MarkupScannerTests
{
    private static List<(SegmentKind Kind, string Text)> Scan(string source)
    {
        var result = new List<(SegmentKind, string)>();
        var scanner = new MarkupScanner(source.AsSpan());
        while (scanner.TryRead(out Segment segment))
        {
            result.Add((segment.Kind, source.Substring(segment.Start, segment.Length)));
        }

        return result;
    }

    [Fact]
    public void РазделяетТегиИТекст()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<p>"), (SegmentKind.Text, "Он - человек"), (SegmentKind.Markup, "</p>")],
            Scan("<p>Он - человек</p>"));
    }

    [Fact]
    public void СодержимоеCodeЗащищено()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<code>"), (SegmentKind.Protected, "a - b"), (SegmentKind.Markup, "</code>")],
            Scan("<code>a - b</code>"));
    }

    [Fact]
    public void УгловаяСкобкаВЗначенииАтрибутаНеЗавершаетТег()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<a title=\"a>b\">"), (SegmentKind.Text, "x"), (SegmentKind.Markup, "</a>")],
            Scan("<a title=\"a>b\">x</a>"));
    }

    [Fact]
    public void ОдинокаяУгловаяСкобкаОстаётсяТекстом()
    {
        Assert.Equal([(SegmentKind.Text, "если a < b, то")], Scan("если a < b, то"));
    }

    [Fact]
    public void КомментарийЗащищён()
    {
        Assert.Equal(
            [(SegmentKind.Protected, "<!--[if IE]>только тут<![endif]-->")],
            Scan("<!--[if IE]>только тут<![endif]-->"));
    }

    [Fact]
    public void НезакрытыйТегДоходитДоКонцаВвода()
    {
        Assert.Equal([(SegmentKind.Text, "текст "), (SegmentKind.Markup, "<b")], Scan("текст <b"));
    }

    [Fact]
    public void ТегВнутриCodeОстаётсяЗащищённым()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<code>"), (SegmentKind.Protected, "<b>x</b>"), (SegmentKind.Markup, "</code>")],
            Scan("<code><b>x</b></code>"));
    }
}
