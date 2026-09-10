using BenchmarkDotNet.Attributes;
using Typographer.Rules;

namespace Typographer.Bench;

/// <summary>
/// Из чего складывается цена фазы Bind: сама машинерия прохода или словарная работа
/// на каждом токене. Ответ решает, что оптимизировать.
/// </summary>
[MemoryDiagnoser]
public class BindPhaseBenchmarks
{
    private const string Fragment =
        "<p>Он сказал: \"Это важно, и т. д.\" - и ушёл в 1941-1945 гг. " +
        "А.С. Пушкин писал про 10 км/ч и 100 % на 25 °C.</p>";

    private string _text = string.Empty;
    private HtmlTypographer _typographer = null!;

    /// <summary>Какой набор правил включён: имя набора задаёт объём словарной работы.</summary>
    [Params("scan-only", "bind-one-cheap", "bind-nbsp", "bind-all")]
    public string Set { get; set; } = "scan-only";

    [GlobalSetup]
    public void Setup()
    {
        _text = string.Concat(Enumerable.Repeat(Fragment, 200));
        _typographer = new HtmlTypographer(new HtmlOptions { Rules = Rules() });
    }

    [Benchmark]
    public string Process() => _typographer.Process(_text);

    private RuleSet Rules()
    {
        RuleId[] scan = [.. RuleSet.Default.Where(rule => rule.Phase == RulePhase.Scan)];
        RuleId[] bind = [.. RuleSet.Default.Where(rule => rule.Phase == RulePhase.Bind)];

        return Set switch
        {
            // Фаза Bind выключена вовсе.
            "scan-only" => RuleSet.None.With(scan),

            // Фаза включена одним правилом, которому словарь не нужен: цена машинерии.
            "bind-one-cheap" => RuleSet.None.With(scan).With(RuleId.Common.Nbsp.Nowrap),

            // Одно словарное правило: цена одного словаря на каждом токене.
            "bind-nbsp" => RuleSet.None.With(scan).With(RuleId.Common.Nbsp.AfterShortWord),

            // Все правила фазы: полная словарная работа.
            _ => RuleSet.None.With(scan).With(bind),
        };
    }
}
