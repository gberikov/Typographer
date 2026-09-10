using System.Text;
using Typographer.Rules;
using Typographer.Tests.Corpus;
using Typographer.Tests.Rules;

namespace Typographer.Tests.Guarantees;

/// <summary>Инварианты, которые держатся сразу для всего реестра.</summary>
public class InvariantTests
{
    /// <summary>Входы-ловушки и корпус: правило проверяется по одному, перебор дешёвый.</summary>
    private static readonly string[] Samples = BuildSamples();

    private static string[] BuildSamples()
    {
        var samples = new List<string>(RulePairTests.Samples);
        foreach (string name in CorpusFiles.EnumerateNames())
        {
            samples.Add(CorpusFiles.ReadInput(name));
        }

        return [.. samples];
    }

    // Гарантия 6: если правок нет, возвращается ТОТ ЖЕ экземпляр строки. Проверяется на
    // каждом правиле поодиночке — именно так ловится правило, которое «меняет» текст на
    // тот же самый и молча заставляет вызывающий код держать вторую копию.
    [Fact]
    public void UnchangedInputReturnsSameInstance()
    {
        var report = new StringBuilder();

        foreach (RuleId rule in RuleSet.All)
        {
            var html = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None.With(rule) });
            var text = new TextTypograf(new TextOptions { Rules = RuleSet.None.With(rule) });

            foreach (string sample in Samples)
            {
                string byHtml = html.Process(sample);
                if (string.Equals(byHtml, sample, StringComparison.Ordinal) && !ReferenceEquals(byHtml, sample))
                {
                    report.AppendLine($"HTML {rule.Name}: {RuleCatalog.Show(sample)}");
                    break;
                }

                string byText = text.Process(sample);
                if (string.Equals(byText, sample, StringComparison.Ordinal) && !ReferenceEquals(byText, sample))
                {
                    report.AppendLine($"текст {rule.Name}: {RuleCatalog.Show(sample)}");
                    break;
                }
            }
        }

        Assert.True(report.Length == 0, report.ToString());
    }

    // Два пути к одному результату: текст без разметки обязан выглядеть одинаково после
    // TextTypograf и после HtmlTypograf. Пока пути расходятся, один из них неверен, и
    // отличить какой на глаз нельзя — это ловилось только по жалобам.
    [Fact]
    public void TextAndHtmlAgreeWhenThereIsNoMarkup()
    {
        // Сущности и правила разметки из набора исключены: в обычном тексте «&nbsp;» —
        // шесть символов, и расхождение там не дефект, а определение режима.
        RuleSet rules = RuleSet.Default
            .Without(RuleId.Registry.MarkupChanging)
            .Without(RuleId.Common.Html.Quot);

        var html = new HtmlTypograf(new HtmlOptions { Rules = rules });
        var text = new TextTypograf(new TextOptions { Rules = rules });
        var report = new StringBuilder();

        foreach (string sample in Samples)
        {
            if (sample.Contains('<') || sample.Contains('&'))
            {
                continue;
            }

            string byHtml = html.Process(sample);
            string byText = text.Process(sample);
            if (!string.Equals(byHtml, byText, StringComparison.Ordinal))
            {
                report.AppendLine($"вход:   {RuleCatalog.Show(sample)}");
                report.AppendLine($"  html: {RuleCatalog.Show(byHtml)}");
                report.AppendLine($"  текст:{RuleCatalog.Show(byText)}");
            }
        }

        Assert.True(report.Length == 0, report.ToString());
    }
}
