using Typographer.Rules;
using Typographer.Tests.Corpus;

namespace Typographer.Tests.Guarantees;

public class MarkupIntegrityTests
{
    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void TagCountUnchanged(string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        string result = typograf.Process(source);

        Assert.Equal(CountTags(source), CountTags(result));
    }

    [Fact]
    public void CodeContentUntouched()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });

        Assert.Contains("<code>a - b \"x\"</code>", typograf.Process("<code>a - b \"x\"</code>"));
    }

    [Fact]
    public void AttributeValuesUntouched()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });

        Assert.Contains("title=\"Не трогать - тире\"", typograf.Process("<a title=\"Не трогать - тире\">x</a>"));
    }

    // Правило delBeforePunctuation удаляет пробел перед «?»/«!». Если слева от пробела стоит
    // «<», результат — «<?»/«<!--» — псевдотег: сама фаза Scan работает по исходной строке, где
    // тега ещё нет, но следующие проходы пересканируют уже изменённый буфер и поверят ему.
    // CountTags псевдотег не ловит (он ищет «<буква» и «</»), поэтому проверяется явно.
    [Theory]
    [InlineData("дом < ? и лес", "<?")]
    [InlineData("текст < !-- не комментарий", "<!--")]
    public void SpaceRuleDoesNotCreateTagStart(string source, string forbidden)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        string result = typograf.Process(source);

        Assert.DoesNotContain(forbidden, result);
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
