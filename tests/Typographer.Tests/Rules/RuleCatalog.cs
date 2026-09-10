using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Typographer.Internal;
using Typographer.Rules;
using Typographer.Tests.Corpus;

namespace Typographer.Tests.Rules;

/// <summary>Описания и примеры правил, собранные из того, что уже есть в репозитории.</summary>
/// <remarks>
/// Описание берётся из XML-комментария свойства правила, а не из отдельного поля реестра:
/// комментарий обязателен (GenerateDocumentationFile и предупреждения-ошибки), значит
/// второе место для того же текста разошлось бы с первым в первый же день.
/// Путь к свойству ищется отражением, а не выводится из имени правила: совпадение
/// «common/space/afterColon» и «Common.Space.AfterColon» — соглашение, а не закон, и
/// проверять его на 107 правилах дороже, чем обойти дерево типов.
/// </remarks>
public static class RuleCatalog
{
    // Правило-носитель: само по себе на образцах ничего не меняет (метка порядка байт
    // есть ровно в одном входе), но заставляет конвейер работать. Для него самого носителем
    // служит второе правило — иначе оба набора совпали бы и примера не нашлось бы вовсе.
    private static readonly RuleId Carrier = RuleId.Common.Other.DelBom;
    private static readonly RuleId SecondCarrier = RuleId.Common.Punctuation.Quote;

    private static readonly Dictionary<string, string> Paths = BuildPaths();
    private static readonly Dictionary<string, string> Summaries = LoadSummaries();
    private static readonly string[] Samples = BuildSamples();

    /// <summary>Описание правила из XML-комментария. Пустая строка, если комментария нет.</summary>
    public static string DescriptionOf(RuleId rule)
        => Paths.TryGetValue(rule.Name, out string? path) && Summaries.TryGetValue(path, out string? summary)
            ? summary
            : string.Empty;

    /// <summary>Пример работы правила или null, если ни на одном образце оно ничего не меняет.</summary>
    /// <remarks>
    /// Сравнивается не вход с выходом, а выход БЕЗ правила с выходом С правилом. Иначе
    /// примером почти каждого правила становилось бы декодирование «&amp;nbsp;»: фаза Prepare
    /// декодирует типографские сущности, как только включено ХОТЬ ОДНО правило, и эта разница
    /// не имеет к проверяемому правилу отношения.
    /// По той же причине в обоих наборах есть правило-носитель: с пустым набором конвейер
    /// возвращает вход байт в байт, и сравнивать было бы не с чем.
    /// </remarks>
    public static string? ExampleOf(RuleId rule)
    {
        RuleId carrier = rule.Equals(Carrier) ? SecondCarrier : Carrier;
        var without = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.None.With(carrier) });
        var with = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.None.With(carrier, rule) });

        foreach (string sample in Samples)
        {
            string before = without.Process(sample);
            string after = with.Process(sample);
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                return $"`{Show(before)}` → `{Show(after)}`";
            }
        }

        return null;
    }

    /// <summary>Делает невидимое видимым и безопасным для ячейки таблицы.</summary>
    public static string Show(string value)
        => value
            .Replace(Chars.Nbsp.ToString(), "_")
            .Replace(Chars.NarrowNbsp.ToString(), "_")
            .Replace("\r\n", "⏎")
            .Replace("\n", "⏎")
            .Replace("|", "\\|");

    private static Dictionary<string, string> BuildPaths()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        Walk(typeof(RuleId), "Typographer.Rules.RuleId.", map);
        return map;
    }

    private static void Walk(Type type, string prefix, Dictionary<string, string> map)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (property.PropertyType == typeof(RuleId))
            {
                var rule = (RuleId)property.GetValue(null)!;
                map[rule.Name] = prefix + property.Name;
            }
        }

        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public))
        {
            Walk(nested, prefix + nested.Name + ".", map);
        }
    }

    private static Dictionary<string, string> LoadSummaries()
    {
        // Файл документации кладётся рядом со сборкой ядра при сборке тестов —
        // GenerateDocumentationFile включён, и XML копируется вместе с dll.
        string path = Path.Combine(AppContext.BaseDirectory, "Typographer.xml");
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (XElement member in XDocument.Load(path).Descendants("member"))
        {
            string name = member.Attribute("name")?.Value ?? string.Empty;
            XElement? summary = member.Element("summary");
            if (name.StartsWith("P:", StringComparison.Ordinal) && summary is not null)
            {
                map[name[2..]] = Flatten(summary);
            }
        }

        return map;
    }

    private static string Flatten(XElement summary)
    {
        var text = new StringBuilder();
        foreach (XNode node in summary.Nodes())
        {
            switch (node)
            {
                case XText plain:
                    text.Append(plain.Value);
                    break;

                // <see cref="T:Typographer.Rules.RuleSet"/> — в тексте нужно последнее звено.
                case XElement link when link.Name == "see":
                    string cref = link.Attribute("cref")?.Value ?? string.Empty;
                    text.Append(cref[(cref.LastIndexOf('.') + 1)..]);
                    break;

                case XElement other:
                    text.Append(other.Value);
                    break;
            }
        }

        return string.Join(" ", text.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Replace("|", "\\|");
    }

    private static string[] BuildSamples()
    {
        var samples = new List<string>();

        foreach (TheoryDataRow<string> row in HardCases.All)
        {
            samples.Add(row.Data);
        }

        foreach (string name in CorpusFiles.EnumerateNames())
        {
            samples.AddRange(CorpusFiles.ReadInput(name).Split('\n'));
        }

        // Края образцов НЕ обрезаются: правила отступов, табуляции и обрезки краёв
        // работают ровно там, и обрезка лишила бы их единственного примера.
        // Короткие образцы вперёд: пример в справочнике должен помещаться в ячейку
        // таблицы и показывать одно правило, а не абзац вокруг него.
        return [.. samples
            .Where(sample => sample.Length is > 0 and <= 80)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(sample => sample.Length)
            .ThenBy(sample => sample, StringComparer.Ordinal)];
    }
}
