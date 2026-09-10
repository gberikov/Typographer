using System.Text;
using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Oracle;

/// <summary>Наш вывод против вывода оракула Лебедева на закоммиченном снимке.</summary>
/// <remarks>
/// Сеть не трогается: снимок лежит в docs/oracle/lebedev.md. Живая сверка — отдельный
/// проект tests/Typographer.Oracle, вне CI.
/// Строки, где мы расходимся с оракулом НАМЕРЕННО, перечислены в Divergences с причиной.
/// Тест падает в обе стороны: и когда совпадение сломалось, и когда расхождение из списка
/// внезапно исчезло — второе значит, что список устарел и вводит в заблуждение.
/// Набор правил — Lebedev: сверяться с оракулом имеет смысл в пресете, который его повторяет.
/// </remarks>
public class OracleSnapshotTests
{
    /// <summary>Входы, на которых мы расходимся с оракулом намеренно. Ключ — вход, значение — причина.</summary>
    private static readonly Dictionary<string, string> Divergences = new(StringComparer.Ordinal)
    {
        ["Он сказал: \"это важно, и т. д.\" - и ушёл в 1941-1945 гг."] =
            "порог короткого слова у нас три буквы (как в JS-typograf), у оракула две: «это» мы привязываем, он нет",
        ["Тире - вот так. И дефис-минус."] =
            "тот же порог короткого слова: «вот» мы привязываем, оракул нет",
        ["Диапазон 1941-1945 и 10-15 штук"] =
            "тот же порог короткого слова плюс привязка числа к следующему слову: «10-15 штук»",
        ["Минус -5 градусов и 5-й дом"] =
            "минус мы не ставим намеренно: дефис и минус в тексте неразличимы без знания замысла автора, "
            + "а гарантия 4 запрещает менять смысл записи по догадке",
        ["Температура 25 C и угол 90 град."] =
            "знак градуса мы дописываем сами (ГОСТ 8.417) и отбиваем от числа неразрывным пробелом (ГОСТ 9.6); оракул не делает ни того, ни другого",
        ["Дробь 1/2, 3/4 и 1/4"] =
            "common/number/fraction собирает вульгарные дроби, оракул оставляет косую черту",
        ["Неравенства 5 <= 6, 7 >= 3, 8 != 9"] =
            "common/number/mathSigns собирает знаки сравнения, оракул оставляет их набором из двух символов",
        ["Разряды 1000000 и 1 000 000 рублей"] =
            "разряды мы разбиваем сами (common/number/digitGrouping в пресете) и уже стоящие пробелы между разрядами делаем неразрывными (ГОСТ 9.5), оракул не делает ни того, ни другого",
        ["Копирайт (c) 2026, торговая марка (tm), знак (r)"] =
            "мы собираем в один символ и «(tm)», и «(r)»; оракул «(tm)» не трогает, а «(r)» оборачивает в тег — разметки из текста мы не делаем (гарантия 4)",
        ["Стрелки -> и <- в тексте"] =
            "common/symbols/arrow собирает стрелки, оракул оставляет их набором из двух символов",
        ["Многоточие... и ещё ...."] =
            "многоточие — один знак, а не три точки (Мильчин); оракул точки оставляет",
        ["Двойная пунктуация!! и ?? и ?!"] =
            "пробел перед знаком вопроса мы удаляем всегда, оракул — только перед одиночным",
        ["\"Вложенные \"кавычки\" внутри\" и 'одинарные'"] =
            "одиночные кавычки мы не трогаем: оракул подменяет обе правой, и открывающая с закрывающей становятся неразличимы",
        ["Апостроф д'Артаньян и 5\" дюймов"] =
            "штрих после числа мы не ставим: тот же символ в тексте чаще кавычка, чем дюйм",
        ["Инициалы А. С. Пушкин и Пушкин А. С."] =
            "инициалы привязываются к фамилии (Мильчин), оракул связывает только инициалы между собой",
        ["Улица ул. Ленина, д. 5, кв. 12"] =
            "сокращение привязывается и к слову слева — «Улица ул.»; у оракула привязка только вправо",
        ["Город г. Москва и с. Никольское"] =
            "«г. Москва» и «с. Никольское» мы не разрываем (Мильчин), оракул разрывает",
        ["Номер № 5 и параграф § 3"] =
            "после № и § мы ставим УЗКИЙ неразрывный пробел (ГОСТ Р 7.0.110), оракул — обычный неразрывный",
        ["Дата 2026-09-09 и 09.09.2026"] =
            "ISO-дату мы переписываем в русский формат, оракул оборачивает её в тег: разметки из текста мы не делаем (гарантия 4)",
        ["Понедельник, 9 сентября 2026 года"] =
            "«2026 года» мы не разрываем, оракул разрывает",
        ["Время 10:30 и 10 ч. 30 мин."] =
            "тот же порог короткого слова: «Время» к времени мы не привязываем, а «и» привязываем к левому соседу",
        ["Телефон +7 (999) 123-45-67"] =
            "телефон мы переписываем неразрывными пробелами, оракул оборачивает в тег: разметки из текста мы не делаем (гарантия 4)",
        ["Ударение а́ и ё вместо е"] =
            "«а́» с комбинирующим ударением коротким словом не считается: в нём есть небуквенный символ",
    };

    public static TheoryData<string> Inputs
    {
        get
        {
            var data = new TheoryData<string>();
            foreach ((string input, _) in OracleSnapshot.Rows)
            {
                data.Add(input);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void MatchesOracleUnlessDivergenceIsRecorded(string input)
    {
        string expected = OracleSnapshot.Rows.First(row => row.Input == input).Expected;
        string actual = Process(input);

        if (Divergences.TryGetValue(input, out string? reason))
        {
            Assert.False(
                expected == actual,
                $"Расхождение исчезло, а запись о нём осталась: «{input}». Причина в списке: {reason}");
            return;
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SnapshotIsNotEmpty() => Assert.NotEmpty(OracleSnapshot.Rows);

    // Разбор снимка подменяет подчёркивание неразрывным пробелом. Приём держится на том, что
    // настоящего подчёркивания во входах нет — если появится, разбор начнёт врать молча.
    [Fact]
    public void NoInputContainsUnderscore()
        => Assert.DoesNotContain(OracleSnapshot.Rows, row => row.Input.Contains('_'));

    // Документ docs/oracle/divergences.md — производная от списка выше, а не второй его
    // экземпляр: причины живут в коде, где их видит тест. Перегенерация — переменной
    // окружения TYPOGRAPHER_UPDATE_ORACLE=1; тест при этом падает намеренно, как и у корпуса.
    [Fact]
    public void DivergenceDocumentIsUpToDate()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("TYPOGRAPHER_UPDATE_ORACLE") == "1",
            "Перегенерация docs/oracle/divergences.md. Задай TYPOGRAPHER_UPDATE_ORACLE=1.");

        File.WriteAllText(
            Path.Combine(OracleSnapshot.RepositoryRoot, "docs", "oracle", "divergences.md"),
            BuildDocument());

        Assert.Fail("docs/oracle/divergences.md перезаписан. Сними переменную и проверь diff.");
    }

    private static string Process(string input) => new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.Lebedev,
        Entities = EntityMode.Symbols,
    }).Process(input);

    private static string BuildDocument()
    {
        var text = new StringBuilder();
        text.Append("""
            # Расхождения с оракулом Лебедева

            Документ сгенерирован тестом `OracleSnapshotTests` из снимка `lebedev.md` и списка
            причин в самом тесте. Править руками нечего: причины живут в коде, где их видит
            тест, а здесь — их читаемая проекция. Перегенерация:
            `TYPOGRAPHER_UPDATE_ORACLE=1 dotnet test tests/Typographer.Tests`.

            Наш набор правил — пресет `Lebedev`. Неразрывный пробел показан как `_`,
            узкий неразрывный — как `~`.


            """.Replace("\r\n", "\n"));

        var matching = new List<string>();
        text.Append("| Вход | Наш вывод | Вывод оракула | Почему расходимся |\n");
        text.Append("|---|---|---|---|\n");

        foreach ((string input, string expected) in OracleSnapshot.Rows)
        {
            string actual = Process(input);
            if (actual == expected)
            {
                matching.Add(input);
                continue;
            }

            string reason = Divergences.TryGetValue(input, out string? recorded)
                ? recorded
                : "**не записано — тест падает**";

            text.Append($"| {Cell(input)} | {Cell(actual)} | {Cell(expected)} | {reason} |\n");
        }

        text.Append($"\nСовпадают полностью {matching.Count} строк из {OracleSnapshot.Rows.Count}.\n");
        return text.ToString();
    }

    private static string Cell(string value) => value
        .Replace(Chars.Nbsp, '_')
        .Replace(Chars.NarrowNbsp, '~')
        .Replace("|", "\\|")
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
