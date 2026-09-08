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
}
