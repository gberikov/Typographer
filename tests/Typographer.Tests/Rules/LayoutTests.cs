using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class LayoutTests
{
    private static string Run(string source, HtmlOptions options) => new HtmlTypograf(options).Process(source);

    [Fact]
    public void UseBr_ReplacesLineBreak()
        => Assert.Equal(
            "первая<br />\nвторая",
            Run("первая\nвторая", new HtmlOptions { Rules = RuleSet.None, UseBr = true }));

    [Fact]
    public void UseP_WrapsParagraphs()
        => Assert.Equal(
            "<p>первый</p>\n<p>второй</p>",
            Run("первый\n\nвторой", new HtmlOptions { Rules = RuleSet.None, UseP = true }));

    [Fact]
    public void UseP_RecognizesWindowsLineBreak()
        => Assert.Equal(
            "<p>первый</p>\n<p>второй</p>",
            Run("первый\r\n\r\nвторой", new HtmlOptions { Rules = RuleSet.None, UseP = true }));

    [Fact]
    public void NoFlagsAddNothing()
        => Assert.Equal(
            "первая\nвторая",
            Run("первая\nвторая", new HtmlOptions { Rules = RuleSet.None }));

    [Fact]
    public void CarriageReturn_CountsAsWordBoundaryInNobr()
        => Assert.Equal(
            $"<nobr>текст{Chars.Nbsp}слово</nobr>\rконец",
            Run($"текст{Chars.Nbsp}слово\rконец", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 }));

    [Fact]
    public void MaxNobr_WrapsNonBreakingGroups()
        => Assert.Equal(
            $"<nobr>в{Chars.Nbsp}доме</nobr> на горе",
            Run($"в{Chars.Nbsp}доме на горе", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 }));

    [Fact]
    public void MaxNobrOfOne_CreatesNoBlocksAndTerminates()
        => Assert.Equal(
            $"a{Chars.Nbsp}b{Chars.Nbsp}c",
            Run($"a{Chars.Nbsp}b{Chars.Nbsp}c", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 1 }));

    [Fact]
    public void ChainLongerThanLimit_SplitsBetweenWords()
        => Assert.Equal(
            $"<nobr>a{Chars.Nbsp}b</nobr>{Chars.Nbsp}<nobr>c{Chars.Nbsp}d</nobr>",
            Run($"a{Chars.Nbsp}b{Chars.Nbsp}c{Chars.Nbsp}d", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 }));

    [Fact]
    public void SingleNonBreakingSpace_NotWrapped()
        => Assert.Equal(
            Chars.Nbsp.ToString(),
            Run(Chars.Nbsp.ToString(), new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 }));

    [Fact]
    public void TextWithoutNonBreakingSpaces_Unchanged()
        => Assert.Equal(
            "просто слова",
            Run("просто слова", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 3 }));

    // Тег переноса ставится перед переводом строки ЦЕЛИКОМ: между \r и \n он разрывал бы
    // windows-перевод и оставлял в выводе одинокий возврат каретки.
    [Fact]
    public void UseBr_DoesNotSplitWindowsLineBreak()
        => Assert.Equal(
            "первая<br />\r\nвторая",
            Run("первая\r\nвторая", new HtmlOptions { Rules = RuleSet.None, UseBr = true }));

    [Theory]
    [InlineData("code")]
    [InlineData("pre")]
    [InlineData("script")]
    [InlineData("style")]
    [InlineData("textarea")]
    [InlineData("kbd")]
    [InlineData("samp")]
    public void UseBr_KeepsLineBreaksInProtectedElements(string tag)
    {
        string element = $"<{tag}>first\nsecond\r\n\r\nthird</{tag}>";

        Assert.Equal(
            element + "outside<br />\nend",
            Run(element + "outside\nend", new HtmlOptions { UseBr = true }));
    }

    [Theory]
    [InlineData("<a title=\"first\n\nsecond\">link</a>")]
    [InlineData("<a\r\n title='first\r\n\r\nsecond'>link</a>")]
    [InlineData("<!-- first\n\nsecond -->")]
    [InlineData("<![CDATA[first >\n\nsecond]]>")]
    [InlineData("<?example first\n\nsecond?>")]
    public void LineBreaksInMarkupBecomeNeitherBrNorParagraphBoundaries(string markup)
    {
        string source = markup + "\n\nlast\nline";

        Assert.Equal(
            markup + "<br />\n<br />\nlast<br />\nline",
            Run(source, new HtmlOptions { UseBr = true }));
        Assert.Equal(
            $"<p>{markup}</p>\n<p>last\nline</p>",
            Run(source, new HtmlOptions { UseP = true }));
        Assert.Equal(
            $"<p>{markup}</p>\n<p>last<br />\nline</p>",
            Run(source, new HtmlOptions { UseP = true, UseBr = true }));
    }

    [Theory]
    [InlineData("code")]
    [InlineData("script")]
    [InlineData("style")]
    [InlineData("textarea")]
    [InlineData("kbd")]
    [InlineData("samp")]
    public void UseP_DoesNotSplitProtectedElement(string tag)
    {
        string element = $"<{tag}>first\n\nsecond</{tag}>";
        string source = "first\n\n" + element + "\n\nlast";
        string expected = $"<p>first</p>\n<p>{element}</p>\n<p>last</p>";
        var typograf = new HtmlTypograf(new HtmlOptions { UseP = true });

        Assert.Equal(expected, typograf.Process(source));
        Assert.Equal(expected, typograf.Process(expected));
        Assert.Equal(expected, Run(source, new HtmlOptions { UseP = true, UseBr = true }));
    }

    [Fact]
    public void UseP_WithUseBr_KeepsProtectionWithExistingBlockMarkup()
        => Assert.Equal(
            "<pre>first\n\nsecond</pre>last<br />\nline",
            Run("<pre>first\n\nsecond</pre>last\nline", new HtmlOptions { UseP = true, UseBr = true }));

    [Theory]
    [InlineData("<script>first\n\nsecond")]
    [InlineData("<code>first\n\nsecond")]
    [InlineData("<a title=\"first\n\nsecond>")]
    [InlineData("<!-- first\n\nsecond")]
    [InlineData("<![CDATA[first >\n\nsecond")]
    public void UseP_DoesNotAppendTagIntoUnclosedMarkup(string markup)
    {
        string source = "first\n\n" + markup;

        Assert.Equal(source, Run(source, new HtmlOptions { UseP = true }));
        Assert.Equal(
            "first<br />\n<br />\n" + markup,
            Run(source, new HtmlOptions { UseP = true, UseBr = true }));
    }

    // Граница абзаца съедается целиком: лишний перевод строки не должен всплывать тегом
    // переноса в начале следующего абзаца.
    [Fact]
    public void UseP_WithUseBr_AddsNoBreakAtParagraphStart()
        => Assert.Equal(
            "<p>раз</p>\n<p>два</p>",
            Run("раз\n\n\nдва", new HtmlOptions { Rules = RuleSet.None, UseP = true, UseBr = true }));

    // Неразрывный блок живёт ВНУТРИ абзаца: абзацы размечаются по документу, а цепочка
    // слов — по текстовому узлу, поэтому одно другое больше не выталкивает.
    [Fact]
    public void UseP_WithMaxNobr_KeepsChainInsideParagraph()
        => Assert.Equal(
            $"<p><nobr>и{Chars.Nbsp}в</nobr>{Chars.Nbsp}доме</p>",
            Run($"и{Chars.Nbsp}в{Chars.Nbsp}доме", new HtmlOptions { Rules = RuleSet.None, UseP = true, MaxNobr = 2 }));

    [Fact]
    public void UseP_CreatesNoOverlappingTagsAtBoundaryInsideInlineElement()
    {
        const string source = "<b>первый\n\nвторой</b>";

        Assert.Equal(source, Run(source, new HtmlOptions { Rules = RuleSet.None, UseP = true }));
        Assert.Equal(
            "<b>первый<br />\n<br />\nвторой</b>",
            Run(source, new HtmlOptions { Rules = RuleSet.None, UseP = true, UseBr = true }));
    }

    [Fact]
    public void BrStartsLineButDoesNotDisableUseP()
        => Assert.Equal(
            "<p>первый<br>продолжение</p>\n<p>второй</p>",
            Run(
                "первый<br>продолжение\n\nвторой",
                new HtmlOptions { Rules = RuleSet.None, UseP = true }));

    [Fact]
    public void UseP_DoesNotWrapDetailsAsInlineElement()
    {
        const string source = "<details><summary>заголовок</summary>текст</details>";

        Assert.Equal(source, Run(source, new HtmlOptions { Rules = RuleSet.None, UseP = true }));
    }

    [Fact]
    public void EmptyParagraphIsNotCreated()
        => Assert.Equal(
            "<p>текст</p>",
            Run("\n\nтекст\n\n", new HtmlOptions { Rules = RuleSet.None, UseP = true }));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LongChain_TerminatesAndLosesNoChars(int maxNobr)
    {
        string chain = string.Join(Chars.Nbsp.ToString(), Enumerable.Range(0, 50).Select(n => $"w{n}"));

        string result = Run(chain, new HtmlOptions { Rules = RuleSet.None, MaxNobr = maxNobr });

        string stripped = result.Replace("<nobr>", string.Empty).Replace("</nobr>", string.Empty);
        Assert.Equal(chain, stripped);
    }
}
