using Typographer.Rules;
using Typographer.Tests.Oracle;

namespace Typographer.Tests.Rules;

/// <summary>Справочник правил не разошёлся с реестром.</summary>
/// <remarks>
/// Перегенерация — переменной окружения TYPOGRAPHER_UPDATE_RULES=1. Тест при этом
/// ПАДАЕТ намеренно: перезапись файла не проверка, и зелёный прогон обманул бы того,
/// кто запустил перегенерацию и забыл про переменную.
/// </remarks>
public class RulesDocumentTests
{
    private static readonly bool Update =
        Environment.GetEnvironmentVariable("TYPOGRAPHER_UPDATE_RULES") == "1";

    // Путь к корню репозитория уже вычислен для снимка оракула — второй способ
    // добраться до того же каталога разошёлся бы с первым.
    private static string Path => System.IO.Path.Combine(OracleSnapshot.RepositoryRoot, "docs", "rules.md");

    [Fact]
    public void RulesDocumentIsUpToDate()
    {
        string expected = RulesDocument.Build();

        if (Update)
        {
            File.WriteAllText(Path, expected);
            Assert.Fail("docs/rules.md перезаписан. Сними TYPOGRAPHER_UPDATE_RULES и проверь diff.");
        }

        Assert.Equal(expected, File.ReadAllText(Path).Replace("\r\n", "\n"));
    }

    // Новое правило без XML-комментария не должно попасть в справочник пустой строкой:
    // комментарий — единственный источник описания.
    [Fact]
    public void EveryRuleHasDescription()
    {
        string[] silent = [.. RuleSet.All
            .Where(rule => RuleCatalog.DescriptionOf(rule).Length == 0)
            .Select(rule => rule.Name)];

        Assert.Empty(silent);
    }

    [Fact]
    public void ShowMakesInvisibleVisible()
    {
        Assert.Equal("а_б", RuleCatalog.Show("а\u00A0б"));
    }
}
