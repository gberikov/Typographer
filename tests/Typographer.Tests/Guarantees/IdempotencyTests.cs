using Typographer.Internal;
using Typographer.Rules;
using Typographer.Tests.Corpus;

namespace Typographer.Tests.Guarantees;

public class IdempotencyTests
{
    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void ПовторныйПрогонНичегоНеМеняет_Html(string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        string once = typograf.Process(source);

        Assert.Equal(once, typograf.Process(once));
    }

    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void ПовторныйПрогонНичегоНеМеняет_PlainText(string source)
    {
        var typograf = new TextTypograf(new TextOptions { Rules = RuleSet.Default });
        string once = typograf.Process(source);

        Assert.Equal(once, typograf.Process(once));
    }

    [Theory]
    [InlineData(EntityMode.Named)]
    [InlineData(EntityMode.Numeric)]
    [InlineData(EntityMode.Mixed)]
    public void ИдемпотентностьСохраняетсяПриКодированииСущностей(EntityMode mode)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default, Entities = mode });
        string once = typograf.Process("Он сказал: \"да\" - и ушёл");

        Assert.Equal(once, typograf.Process(once));
    }

    // Гарантия 5 намеренно НЕ распространяется на UseBr/UseP/MaxNobr (см. docs/spec.md, §8,
    // и XML-комментарии на этих опциях в HtmlOptions): они оборачивают текст в разметку один
    // раз, и прогон по собственному выводу типографа вкладывает разметку в саму себя. Эти три
    // теста закрепляют РЕАЛЬНОЕ поведение на повторном прогоне — оно должно быть известным и
    // проверяемым, а не сюрпризом при следующей правке.

    [Fact]
    public void UseBr_ПовторныйПрогонВкладываетТегПереноса()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None, UseBr = true });
        string once = typograf.Process("первая\nвторая");
        string twice = typograf.Process(once);

        Assert.Equal("первая<br />\nвторая", once);
        Assert.Equal("первая<br /><br />\nвторая", twice);
        Assert.NotEqual(once, twice);
    }

    [Fact]
    public void UseP_ПовторныйПрогонВкладываетТегАбзаца()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None, UseP = true });
        string once = typograf.Process("первый\n\nвторой");
        string twice = typograf.Process(once);

        Assert.Equal("<p>первый</p>\n<p>второй</p>", once);
        // Разделяющий "\n" между <p> тоже становится отдельным (пустым по тексту) абзацем,
        // так как второй прогон видит его как обычный текст между двумя защищёнными зонами.
        Assert.Equal("<p><p>первый</p></p><p>\n</p><p><p>второй</p></p>", twice);
        Assert.NotEqual(once, twice);
    }

    [Fact]
    public void MaxNobr_ПовторныйПрогонВкладываетТегNobr()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 });
        string once = typograf.Process($"в{Chars.Nbsp}доме на горе");
        string twice = typograf.Process(once);

        Assert.Equal($"<nobr>в{Chars.Nbsp}доме</nobr> на горе", once);
        Assert.Equal($"<nobr><nobr>в{Chars.Nbsp}доме</nobr></nobr> на горе", twice);
        Assert.NotEqual(once, twice);
    }
}
