using System.Reflection;

namespace Typographer.Oracle;

/// <summary>Живая сверка снимка с веб-сервисом. Вне CI: ходит в сеть.</summary>
/// <remarks>
/// Запуск: TYPOGRAPHER_ORACLE=1 dotnet test tests/Typographer.Oracle.
/// Без переменной тест помечает себя пропущенным. Пропуск надёжнее фильтра по трейту в
/// командной строке: забытый фильтр означает сеть в CI, забытая переменная — всего лишь
/// пропущенный тест.
/// Тест отвечает на один вопрос: изменился ли сервис с тех пор, как снимок сняли. Наши
/// расхождения с оракулом проверяет OracleSnapshotTests в основном наборе, без сети.
/// </remarks>
[Trait("Category", "Oracle")]
public class LiveOracleTests
{
    /// <summary>Неразрывный пробел: в исходнике он неотличим от обычного, поэтому строится из кода.</summary>
    private const string Nbsp = "\u00A0";

    private static string Root => Path.GetFullPath(
        typeof(LiveOracleTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "RepositoryRoot")
            .Value!);

    [Fact]
    public async Task SnapshotStillMatchesLiveService()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("TYPOGRAPHER_ORACLE") == "1",
            "Живая сверка ходит в сеть. Задай TYPOGRAPHER_ORACLE=1, чтобы её запустить.");

        string[] inputs = await File.ReadAllLinesAsync(
            Path.Combine(Root, "tools", "oracle-inputs.txt"), TestContext.Current.CancellationToken);
        Dictionary<string, string> snapshot = ReadSnapshot();

        using var service = new LebedevService();
        var stale = new List<string>();

        foreach (string input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input) || !snapshot.TryGetValue(input, out string? recorded))
            {
                continue;
            }

            string live = await service.ProcessTextAsync(input, TestContext.Current.CancellationToken);
            if (live != recorded)
            {
                stale.Add($"вход:   {input}\n  снимок: {Show(recorded)}\n  сервис: {Show(live)}");
            }
        }

        Assert.True(
            stale.Count == 0,
            $"Снимок устарел на {stale.Count} строках из {snapshot.Count}. "
                + "Пересними: bash tools/oracle-snapshot.sh\n"
                + string.Join("\n", stale));
    }

    // Разбор снимка обязан работать и без сети. Если таблица разъедется, живая сверка
    // не найдёт ни одной строки, пропустит их все и молча «пройдёт» — этот тест держит
    // её честной. Заодно он единственный в проекте, который выполняется всегда: набор
    // из одних пропущенных тестов Microsoft.Testing.Platform считает несостоявшимся
    // прогоном и возвращает ненулевой код, а CI строит и этот проект тоже.
    [Fact]
    public void SnapshotParsesToRows()
    {
        Assert.NotEmpty(ReadSnapshot());
        Assert.True(File.Exists(Path.Combine(Root, "tools", "oracle-inputs.txt")));
    }

    /// <summary>Читает снимок так же, как OracleSnapshot в основном наборе, но без ссылки на него.</summary>
    /// <remarks>
    /// Дублирование намеренное: тянуть ссылку из этого проекта на Typographer.Tests ради
    /// двадцати строк разбора — связь дороже дубля, а Typographer.Tests ещё и видит
    /// внутренности ядра, которые здесь не нужны.
    /// </remarks>
    private static Dictionary<string, string> ReadSnapshot()
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(Path.Combine(Root, "docs", "oracle", "lebedev.md")))
        {
            string body = line.Trim();
            if (!body.StartsWith("| ", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = body[1..^1].Split(" | ");
            if (cells.Length != 2 || cells[0].Trim() == "Вход")
            {
                continue;
            }

            rows[Unescape(cells[0])] = Unescape(cells[1]).Replace("_", Nbsp);
        }

        return rows;
    }

    /// <summary>
    /// Возвращает ячейку таблицы к настоящим символам. Угловые скобки и амперсанд в снимке
    /// записаны сущностями — иначе Markdown-таблица разъезжается, — а вертикальная черта
    /// экранирована обратной косой.
    /// </summary>
    private static string Unescape(string cell) => cell
        .Trim()
        .Replace("\\|", "|")
        .Replace("&lt;", "<")
        .Replace("&gt;", ">")
        .Replace("&amp;", "&");

    /// <summary>Неразрывный пробел в отчёте виден как подчёркивание — иначе строки неотличимы.</summary>
    private static string Show(string value) => value.Replace(Nbsp, "_");
}
