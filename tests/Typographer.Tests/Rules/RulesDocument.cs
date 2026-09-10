using System.Text;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>Текст справочника правил: реестр + описания + примеры.</summary>
internal static class RulesDocument
{
    private static readonly (string Name, RuleSet Set)[] Presets =
    [
        ("Default", RuleSet.Default),
        ("Lebedev", RuleSet.Lebedev),
        ("Typograf", RuleSet.Typograf),
        ("Gost", RuleSet.Gost),
        ("Minimal", RuleSet.Minimal),
    ];

    /// <summary>Собирает справочник целиком.</summary>
    public static string Build()
    {
        var text = new StringBuilder();

        text.Append("# Справочник правил\n\n");
        text.Append("Файл СГЕНЕРИРОВАН тестом `RulesDocumentTests` из реестра правил, ");
        text.Append("XML-комментариев ядра и тестовых данных. Руками не править: правка ");
        text.Append("потеряется при перегенерации, а тест в CI упадёт.\n\n");
        text.Append("Пример подобран автоматически — это первый из тестовых входов, на котором ");
        text.Append("правило, включённое в одиночку, что-то меняет. Неразрывный пробел показан ");
        text.Append("подчёркиванием `_`, перевод строки — знаком `⏎`.\n\n");

        text.Append($"Всего правил: {RuleSet.All.Count}.");
        foreach ((string name, RuleSet set) in Presets)
        {
            text.Append($" В пресете {name}: {set.Count}.");
        }

        text.Append('\n');

        foreach (RulePhase phase in Enum.GetValues<RulePhase>())
        {
            RuleId[] rules = [.. RuleSet.All
                .Where(rule => rule.Phase == phase)
                .OrderBy(rule => rule.Name, StringComparer.Ordinal)];

            if (rules.Length == 0)
            {
                continue;
            }

            text.Append($"\n## Фаза {phase} ({rules.Length})\n\n");
            text.Append("| Правило | Что делает | Пресеты | Пример |\n|---|---|---|---|\n");

            foreach (RuleId rule in rules)
            {
                string presets = string.Join(", ", Presets.Where(p => p.Set.Contains(rule)).Select(p => p.Name));
                text.Append($"| `{rule.Name}` | {RuleCatalog.DescriptionOf(rule)} ");
                text.Append($"| {(presets.Length == 0 ? "—" : presets)} ");
                text.Append($"| {RuleCatalog.ExampleOf(rule) ?? "—"} |\n");
            }
        }

        return text.ToString();
    }
}
