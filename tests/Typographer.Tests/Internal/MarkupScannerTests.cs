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
    public void SeparatesTagsAndText()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<p>"), (SegmentKind.Text, "Он - человек"), (SegmentKind.Markup, "</p>")],
            Scan("<p>Он - человек</p>"));
    }

    [Fact]
    public void CodeContentIsProtected()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<code>"), (SegmentKind.Protected, "a - b"), (SegmentKind.Markup, "</code>")],
            Scan("<code>a - b</code>"));
    }

    [Fact]
    public void AngleBracketInAttributeValueDoesNotEndTag()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<a title=\"a>b\">"), (SegmentKind.Text, "x"), (SegmentKind.Markup, "</a>")],
            Scan("<a title=\"a>b\">x</a>"));
    }

    [Fact]
    public void LoneAngleBracketStaysText()
    {
        Assert.Equal([(SegmentKind.Text, "если a < b, то")], Scan("если a < b, то"));
    }

    [Fact]
    public void CommentIsProtected()
    {
        Assert.Equal(
            [(SegmentKind.Protected, "<!--[if IE]>только тут<![endif]-->")],
            Scan("<!--[if IE]>только тут<![endif]-->"));
    }

    [Fact]
    public void UnclosedTagRunsToEndOfInput()
    {
        Assert.Equal([(SegmentKind.Text, "текст "), (SegmentKind.Markup, "<b")], Scan("текст <b"));
    }

    [Fact]
    public void TagInsideCodeStaysProtected()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<code>"), (SegmentKind.Protected, "<b>x</b>"), (SegmentKind.Markup, "</code>")],
            Scan("<code><b>x</b></code>"));
    }

    [Fact]
    public void SimilarClosingTagNameDoesNotEndProtection()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<code>"), (SegmentKind.Protected, "a</codex>b"), (SegmentKind.Markup, "</code>")],
            Scan("<code>a</codex>b</code>"));
    }

    [Fact]
    public void ClosingTagWithSpaceBeforeAngleBracket()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<code>"), (SegmentKind.Protected, "a"), (SegmentKind.Markup, "</code >")],
            Scan("<code>a</code >"));
    }

    // Имя пользовательского элемента обязано содержать дефис по спецификации HTML, поэтому
    // <code-block> — обычный тег, а не <code>. Раньше имя читалось до первого не-буквенного
    // символа, зона считалась защищённой, а закрывающий </code> не находился никогда — без
    // типографики оставался весь остаток документа.
    [Fact]
    public void CustomElementNotConfusedWithProtectedTag()
    {
        Assert.Equal(
            [
                (SegmentKind.Markup, "<code-block>"),
                (SegmentKind.Text, "a - b"),
                (SegmentKind.Markup, "</code-block>"),
                (SegmentKind.Text, " ещё"),
            ],
            Scan("<code-block>a - b</code-block> ещё"));
    }

    // Текстовый поиск закрывающего тега обрывал зону на первом совпадении, где бы оно ни
    // стояло: в комментарии, в значении атрибута или у вложенного одноимённого элемента.
    // Остаток зоны после этого молча уходил в типографирование.
    [Fact]
    public void ClosingTagInCommentDoesNotEndProtection()
    {
        Assert.Equal(
            [
                (SegmentKind.Markup, "<pre>"),
                (SegmentKind.Protected, "<!-- </pre> -->a - b"),
                (SegmentKind.Markup, "</pre>"),
            ],
            Scan("<pre><!-- </pre> -->a - b</pre>"));
    }

    [Fact]
    public void ClosingTagInAttributeValueDoesNotEndProtection()
    {
        Assert.Equal(
            [
                (SegmentKind.Markup, "<pre>"),
                (SegmentKind.Protected, "<a title=\"</pre>\">x</a> a - b"),
                (SegmentKind.Markup, "</pre>"),
            ],
            Scan("<pre><a title=\"</pre>\">x</a> a - b</pre>"));
    }

    [Fact]
    public void NestedSameNameElementDoesNotEndProtection()
    {
        Assert.Equal(
            [
                (SegmentKind.Markup, "<code>"),
                (SegmentKind.Protected, "outer <code>inner</code> a - b"),
                (SegmentKind.Markup, "</code>"),
            ],
            Scan("<code>outer <code>inner</code> a - b</code>"));
    }

    // Содержимое script — сырой текст: вложить <script> в <script> нельзя, и зону
    // закрывает первый же закрывающий тег.
    [Fact]
    public void RawTextClosesAtFirstClosingTag()
    {
        Assert.Equal(
            [
                (SegmentKind.Markup, "<script>"),
                (SegmentKind.Protected, "var a = \"<script>\";"),
                (SegmentKind.Markup, "</script>"),
                (SegmentKind.Text, " a - b"),
            ],
            Scan("<script>var a = \"<script>\";</script> a - b"));
    }

    [Theory]
    [InlineData("textarea", "<pre>")]
    [InlineData("script", "const s = '<pre>';")]
    [InlineData("style", "x::before { content: '<pre>'; }")]
    [InlineData("textarea", "</pre>")]
    [InlineData("SCRIPT", "const s = '</pre>';")]
    [InlineData("style", "x::before { content: '</pre>'; }")]
    public void NestedRawTextDoesNotChangeProtectedElementDepth(string tag, string content)
    {
        string protectedContent = $"<{tag}>{content}</{tag}>x - y";
        string source = $"<pre>{protectedContent}</pre>слово - слово";

        Assert.Equal(
            [
                (SegmentKind.Markup, "<pre>"),
                (SegmentKind.Protected, protectedContent),
                (SegmentKind.Markup, "</pre>"),
                (SegmentKind.Text, "слово - слово"),
            ],
            Scan(source));
        Assert.Equal(
            $"<pre>{protectedContent}</pre>слово{Chars.Nbsp}{Chars.MDash} слово",
            HtmlTypographer.Default.Process(source));
    }

    [Fact]
    public void UnclosedNestedRawTextStaysProtectedToEnd()
    {
        const string content = "<textarea></pre>слово - слово";

        Assert.Equal(
            [(SegmentKind.Markup, "<pre>"), (SegmentKind.Protected, content)],
            Scan("<pre>" + content));
    }

    // Внутри CDATA угловая скобка — обычный символ, секция кончается на "]]>".
    [Fact]
    public void CDataSectionIsFullyProtected()
    {
        Assert.Equal(
            [(SegmentKind.Protected, "<![CDATA[ x > a - b ]]>"), (SegmentKind.Text, " конец")],
            Scan("<![CDATA[ x > a - b ]]> конец"));
    }

    [Fact]
    public void UnclosedCDataSectionRunsToEndOfInput()
    {
        Assert.Equal(
            [(SegmentKind.Protected, "<![CDATA[ x > a - b")],
            Scan("<![CDATA[ x > a - b"));
    }

    [Fact]
    public void TagCaseDoesNotMatter()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<CODE>"), (SegmentKind.Protected, "a - b"), (SegmentKind.Markup, "</Code>")],
            Scan("<CODE>a - b</Code>"));
    }
}
