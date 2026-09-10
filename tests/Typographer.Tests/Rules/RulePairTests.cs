using System.Text;
using Typographer.Rules;
using Typographer.Tests.Corpus;

namespace Typographer.Tests.Rules;

/// <summary>
/// Гарантии проверяются на КАЖДОЙ паре правил, а не только на пресетах.
/// </summary>
/// <remarks>
/// Пресет — не единственный способ собрать набор: <c>With</c> и <c>Without</c> публичны, и
/// пользователь вправе включить любые два правила. Дефекты взаимодействия живут именно в
/// парах: в пресете их прячет третье правило, которое срабатывает раньше и приводит текст
/// к тому же виду. Так был найден дефект табуляции, который 1400 тестов не видели.
/// 5671 пара на входах-ловушках занимает около четырёх секунд — цена приемлемая.
/// </remarks>
public class RulePairTests
{
    private static readonly RuleId[] Rules = [.. RuleSet.All.OrderBy(r => r.Name, StringComparer.Ordinal)];

    private static readonly string[] MarkupChanging =
        [.. RuleId.Registry.MarkupChanging.Select(r => r.Name)];

    /// <summary>Правила, которым неидемпотентность разрешена спецификацией (гарантия 5).</summary>
    private static readonly string[] NotIdempotent =
    [
        "common/nbsp/replaceNbsp",
        "common/html/nbr",
        "common/html/escape",
        "common/html/p",
    ];

    /// <summary>
    /// Пары, у которых второй прогон меняет результат первого. Список закреплён, а не
    /// исправлен: у всех трёх один корень — правило меняет ГРАНИЦЫ токенов, а соседнее
    /// правило той же фазы их уже не пересматривает. Починка — пересборка фазы Bind,
    /// решение о ней не принято. В пресетах ни одна пара не проявляется: там раньше
    /// срабатывает третье правило и приводит текст к тому же виду.
    /// </summary>
    private static readonly HashSet<string> KnownNotIdempotent =
    [
        // «P.S.» становится «P. S.», и склейка с предыдущим словом видит новую границу
        // только на следующем прогоне.
        "common/nbsp/beforeShortLastWord + ru/nbsp/ps",
        "ru/nbsp/initials + ru/nbsp/ps",

        // Английское тире съедает пробелы вокруг себя, русское правило пробела после
        // запятой ставит пробел обратно. Правила разных языков спорят по существу.
        "common/space/afterComma + en-US/dash/main",
    ];

    /// <summary>
    /// Входы-ловушки. Корпус в перебор пар не берётся намеренно: его файлы длинные, а
    /// стыки правил ловятся короткими случаями — с корпусом перебор занимал двадцать
    /// секунд вместо пяти и ничего сверх не находил.
    /// </summary>
    public static string[] Samples { get; } = BuildSamples();

    [Fact]
    public void PairsAreIdempotentExceptKnownOnes()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var report = new StringBuilder();

        ForEachPair(NotIdempotent, (name, typograf) =>
        {
            foreach (string sample in Samples)
            {
                string once = typograf.Process(sample);
                if (!string.Equals(once, typograf.Process(once), StringComparison.Ordinal))
                {
                    found.Add(name);
                    if (!KnownNotIdempotent.Contains(name))
                    {
                        report.AppendLine($"НОВАЯ пара: {name}");
                        report.AppendLine($"  вход: {RuleCatalog.Show(sample)}");
                        report.AppendLine($"  раз:  {RuleCatalog.Show(once)}");
                        report.AppendLine($"  два:  {RuleCatalog.Show(typograf.Process(once))}");
                    }

                    return;
                }
            }
        });

        foreach (string known in KnownNotIdempotent.Where(k => !found.Contains(k)))
        {
            report.AppendLine($"Пара {known} больше не нарушает идемпотентность — убери её из списка.");
        }

        Assert.True(report.Length == 0, report.ToString());
    }

    // Гарантия 3: разметка байт в байт. Правила, которые её меняют по своей работе,
    // из перебора исключены — это автоссылки, переносы, абзацы и висячая пунктуация.
    [Fact]
    public void PairsKeepMarkupIntact()
    {
        var report = new StringBuilder();

        ForEachPair(MarkupChanging, (name, typograf) =>
        {
            foreach (string sample in Samples)
            {
                string result = typograf.Process(sample);
                if (!string.Equals(MarkupOf(sample), MarkupOf(result), StringComparison.Ordinal))
                {
                    report.AppendLine($"{name}");
                    report.AppendLine($"  вход:  {RuleCatalog.Show(sample)}");
                    report.AppendLine($"  выход: {RuleCatalog.Show(result)}");
                    return;
                }
            }
        });

        Assert.True(report.Length == 0, report.ToString());
    }

    private static void ForEachPair(string[] excluded, Action<string, HtmlTypograf> check)
    {
        for (int a = 0; a < Rules.Length; a++)
        {
            if (excluded.Contains(Rules[a].Name))
            {
                continue;
            }

            for (int b = a + 1; b < Rules.Length; b++)
            {
                if (excluded.Contains(Rules[b].Name))
                {
                    continue;
                }

                check(
                    $"{Rules[a].Name} + {Rules[b].Name}",
                    new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None.With(Rules[a], Rules[b]) }));
            }
        }
    }

    private static string[] BuildSamples()
    {
        var samples = new List<string>();
        foreach (TheoryDataRow<string> row in HardCases.All)
        {
            samples.Add(row.Data);
        }

        return [.. samples.Where(s => s.Length > 0)];
    }

    /// <summary>
    /// Отрезки разметки входа. Признак тега тот же, что у сканера: «меньше», за которым
    /// буква или косая. Иначе «5 &lt;= 6» считается тегом, и любое правило чисел выглядит
    /// нарушителем гарантии 3.
    /// </summary>
    private static string MarkupOf(string value)
    {
        var markup = new StringBuilder();
        bool inside = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!inside && c == '<' && i + 1 < value.Length
                && (char.IsLetter(value[i + 1]) || value[i + 1] is '/' or '!' or '?'))
            {
                inside = true;
            }

            if (inside)
            {
                markup.Append(c);
            }

            if (inside && c == '>')
            {
                inside = false;
            }
        }

        return markup.ToString();
    }
}
