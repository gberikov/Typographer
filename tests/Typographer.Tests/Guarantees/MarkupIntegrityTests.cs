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

    // Правила фазы Bind УСЕКАЮТ буфер («г.г.» в «гг.», повтор слова, знак рубля), и усечение
    // мимо границы съело бы байты тега. Набор All включает их все разом — именно на нём
    // нарушение гарантии 3 вероятнее всего.
    // Правила из Registry.MarkupChanging из набора исключены: тест проверяет сохранность
    // СУЩЕСТВУЮЩЕЙ разметки, а автоссылки, висячая пунктуация, переносы и абзацы теги
    // добавляют законно — это их работа, и она разрешена только вне Default (гарантия 4).
    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void TagCountUnchanged_AllRules(string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions
        {
            Rules = RuleSet.All.Without(RuleId.Registry.MarkupChanging),
        });
        string result = typograf.Process(source);

        Assert.Equal(CountTags(source), CountTags(result));
    }

    // Токен, разорванный тегом, правилу-перезаписи недоступен вовсе: усечение до его начала
    // прошло бы сквозь разметку.
    [Theory]
    // Проверяется именно НЕИЗМЕННОСТЬ отрезка вокруг тега: пробелы снаружи правила
    // склейки трогают законно, а вот слить «г.» и «г.» через тег или стереть повтор
    // они не имеют права.
    [InlineData("1990 г.<b>г.</b>", "г.<b>г.</b>")]
    [InlineData("раз <b>раз</b>", "<b>раз</b>")]
    [InlineData("P.<b>S.</b> текст", "P.<b>S.</b> текст")]
    public void RewriteRulesStopAtMarkup(string source, string untouched)
        => Assert.Contains(
            untouched,
            new HtmlTypograf(new HtmlOptions
            {
                Rules = RuleSet.All.Without(RuleId.Registry.MarkupChanging),
            }).Process(source),
            StringComparison.Ordinal);

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
