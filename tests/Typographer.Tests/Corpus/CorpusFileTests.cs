using Typographer.Rules;

namespace Typographer.Tests.Corpus;

/// <summary>Golden-корпус: выход типографа на файле совпадает с эталоном рядом с ним.</summary>
/// <remarks>
/// Эталоны перегенерируются переменной окружения TYPOGRAPHER_UPDATE_CORPUS=1. Тест при
/// этом ПАДАЕТ намеренно: перегенерация — не проверка, и зелёный прогон с перезаписанными
/// эталонами обманул бы того, кто её запустил и забыл про переменную.
/// Набор правил — Default в режиме HTML: корпус показывает то, что получает тот, кто просто
/// вызвал Typographer.Html(text). Всё, что вне Default, проверяется юнит-тестами правил.
/// </remarks>
public class CorpusFileTests
{
    private static readonly bool Update =
        Environment.GetEnvironmentVariable("TYPOGRAPHER_UPDATE_CORPUS") == "1";

    [Theory]
    [MemberData(nameof(CorpusFiles.Names), MemberType = typeof(CorpusFiles))]
    public void OutputMatchesGolden(string name)
    {
        var typographer = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.Default });
        string actual = typographer.Process(CorpusFiles.ReadInput(name));

        if (Update)
        {
            CorpusFiles.WriteExpected(name, actual);
            Assert.Fail($"Эталон {name}.out.txt перезаписан. Сними TYPOGRAPHER_UPDATE_CORPUS и проверь diff.");
        }

        Assert.Equal(CorpusFiles.ReadExpected(name), actual);
    }

    // Гарантия 5 на связном материале, а не на строчках-ловушках: корпус — единственное
    // место, где вход похож на настоящий документ.
    [Theory]
    [MemberData(nameof(CorpusFiles.Names), MemberType = typeof(CorpusFiles))]
    public void GoldenOutputIsFixedPoint(string name)
    {
        if (Update)
        {
            return;
        }

        var typographer = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.Default });
        string expected = CorpusFiles.ReadExpected(name);

        Assert.Equal(expected, typographer.Process(expected));
    }

    [Fact]
    public void CorpusIsNotEmpty() => Assert.NotEmpty(CorpusFiles.EnumerateNames());
}
