using System.Reflection;

namespace Typographer.Tests.Oracle;

/// <summary>Снимок веб-сервиса Лебедева, разобранный из таблицы docs/oracle/lebedev.md.</summary>
/// <remarks>
/// Снимок снят скриптом tools/oracle-snapshot.sh с настройками entityType=3 (символы),
/// useBr=false, useP=false, maxNobr=0. В таблице неразрывный пробел записан подчёркиванием,
/// вертикальная черта экранирована, а угловые скобки и амперсанд — сущностями: иначе
/// Markdown-таблица разъезжается. Разбор возвращает всё это к настоящим символам.
/// Сеть не трогается: снимок закоммичен, и тест по нему идёт без единого запроса.
/// </remarks>
public static class OracleSnapshot
{
    /// <summary>Каталог репозитория — путь, подставленный MSBuild во время сборки.</summary>
    public static string RepositoryRoot { get; } = Path.GetFullPath(
        typeof(OracleSnapshot).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "RepositoryRoot")
            .Value!);

    /// <summary>Пары «вход — выход оракула» в порядке таблицы.</summary>
    public static IReadOnlyList<(string Input, string Expected)> Rows { get; } = Parse();

    private static IReadOnlyList<(string, string)> Parse()
    {
        string path = Path.Combine(RepositoryRoot, "docs", "oracle", "lebedev.md");
        var rows = new List<(string, string)>();

        foreach (string line in File.ReadLines(path))
        {
            string body = line.Trim();
            if (!body.StartsWith("| ", StringComparison.Ordinal))
            {
                continue;
            }

            body = body[1..^1];
            int split = FindSeparator(body);
            if (split < 0)
            {
                continue;
            }

            string input = Unescape(body[..split]);
            if (input == "Вход")
            {
                continue;
            }

            rows.Add((input, Unescape(body[(split + 1)..])));
        }

        return rows;
    }

    /// <summary>Разделитель колонок — вертикальная черта, не экранированная обратной косой.</summary>
    private static int FindSeparator(string body)
    {
        for (int i = 0; i < body.Length; i++)
        {
            if (body[i] == '|' && (i == 0 || body[i - 1] != '\\'))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Unescape(string cell) => cell
        .Trim()
        .Replace("\\|", "|")
        .Replace("_", " ")
        .Replace("&lt;", "<")
        .Replace("&gt;", ">")
        .Replace("&amp;", "&");
}
