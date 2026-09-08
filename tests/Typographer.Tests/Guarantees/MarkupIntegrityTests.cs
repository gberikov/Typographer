using Typographer.Rules;
using Typographer.Tests.Corpus;

namespace Typographer.Tests.Guarantees;

public class MarkupIntegrityTests
{
    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void КоличествоТеговНеМеняется(string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        string result = typograf.Process(source);

        Assert.Equal(CountTags(source), CountTags(result));
    }

    [Fact]
    public void СодержимоеCodeНеТрогается()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });

        Assert.Contains("<code>a - b \"x\"</code>", typograf.Process("<code>a - b \"x\"</code>"));
    }

    [Fact]
    public void ЗначенияАтрибутовНеТрогаются()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });

        Assert.Contains("title=\"Не трогать - тире\"", typograf.Process("<a title=\"Не трогать - тире\">x</a>"));
    }

    private static int CountTags(string value)
    {
        int count = 0;
        for (int i = 0; i < value.Length - 1; i++)
        {
            if (value[i] == '<' && (char.IsLetter(value[i + 1]) || value[i + 1] == '/'))
            {
                count++;
            }
        }

        return count;
    }
}
