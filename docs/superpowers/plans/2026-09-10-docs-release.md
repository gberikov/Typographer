# План 5. Документация и релиз

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** справочник правил, который не может разойтись с кодом, четыре страницы
документации по-русски, сайт DocFX на GitHub Pages и публикация всех пяти пакетов в
NuGet по тегу.

**Architecture:** ни строчки в `src/` — план про `docs/`, `tests/` и `.github/`.
Справочник правил не пишется руками, а собирается из трёх источников, которые уже есть:
реестр правил (имена, фазы, пресеты), XML-комментарии ядра (описания) и тестовые данные
(примеры). Тест сверяет файл с генератором и падает, если они разошлись, — тот же приём,
что уже держит `docs/oracle/divergences.md`. Сайт и публикация — два workflow, каждый со
своим триггером: сайт по тегу и вручную, публикация по тегу.

**Tech Stack:** C# 13, .NET 10 SDK, xunit.v3 3.2.0 на Microsoft.Testing.Platform,
DocFX 2.78.5, GitHub Actions, GitHub Pages, NuGet.

**Spec:** `docs/spec.md`, раздел 11 «Документация» и раздел 12 «Версионирование и релизы».

## Global Constraints

- Ни один файл в `src/` этим планом не меняется. Если справочнику не хватает данных —
  их берут из уже существующих XML-комментариев, а не дописывают в реестр.
- `dotnet test` запускается БЕЗ префикса `rtk` — под Microsoft.Testing.Platform фильтр rtk
  печатает «0 tests, exit code 5», хотя тесты проходят. Все остальные команды — с `rtk`.
- Ветка работы — `feature/docs-release`, ответвлённая от `develop`. Прямой коммит в
  `master` и `develop` запрещён правилами репозитория.
- Теги версий без префикса `v`: `0.1.0`, не `v0.1.0`. Триггер workflow — `'[0-9]*'`.
  Заголовок релиза — голый номер версии, без имени проекта.
- Ветка по умолчанию — `master`. `workflow_dispatch` работает только для файла,
  лежащего в ветке по умолчанию: пока workflow не дошёл до `master`, вручную он не
  запускается (проверено в плане 3 на фаззинге).
- Имена файлов документации — латиницей (совместимость с DocFX, CI и URL), содержимое —
  по-русски.
- Невидимые символы в исходниках literal-ом не пишутся: неразрывный пробел — только
  `"\u00A0"` или `Chars.Nbsp`.
- Каждый коммит заканчивается строками:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
  ```

---

## Файловая структура

| Файл | Ответственность |
|---|---|
| `tests/Typographer.Tests/Rules/RuleCatalog.cs` | сопоставление правила с описанием и примером |
| `tests/Typographer.Tests/Rules/RulesDocument.cs` | сборка текста справочника |
| `tests/Typographer.Tests/Rules/RulesDocumentTests.cs` | сверка файла с генератором и режим перегенерации |
| `docs/rules.md` | справочник правил, генерируется |
| `docs/index.md` | главная страница сайта |
| `docs/quickstart.md` | быстрый старт |
| `docs/architecture.md` | архитектура конвейера |
| `docs/recipes.md` | рецепты |
| `docs/sources.md` | источники истины и расхождения |
| `docs/toc.yml` | навигация сайта |
| `docs/docfx.json` | конфигурация DocFX |
| `.github/workflows/docs.yml` | сборка и публикация сайта |
| `.github/workflows/release.yml` | публикация пакетов в NuGet по тегу |
| `README.md` | ссылка на сайт, актуальный статус |

---

## Task 1: Справочник правил, который не может разойтись с кодом

Спецификация (раздел 11) требует, чтобы `docs/rules.md` генерировался из метаданных, а
примеры брались из реальных тестов. Отдельных метаданных у правил нет и заводить их не
нужно: описание каждого правила уже написано в XML-комментарии его свойства, а тестовые
данные — в `HardCases` и корпусе. Пример подбирается сам: правило включается в одиночку и
прогоняется по образцам, первый же образец, на котором вывод отличается от входа,
становится примером.

**Files:**
- Create: `tests/Typographer.Tests/Rules/RuleCatalog.cs`
- Create: `tests/Typographer.Tests/Rules/RulesDocument.cs`
- Create: `tests/Typographer.Tests/Rules/RulesDocumentTests.cs`
- Create: `docs/rules.md` (генерируется шагом 5)

**Interfaces:**
- Consumes: `RuleSet.All`, `RuleSet.Default`, `RuleSet.Lebedev`, `RuleSet.Typograf`,
  `RuleSet.Gost`, `RuleSet.Minimal`, `RuleId.Name`, `RuleId.Phase`,
  `HtmlTypograf.Process(string)`, `HardCases.All`, `CorpusFiles.EnumerateNames()`,
  `CorpusFiles.ReadInput(string)`, `OracleSnapshot.RepositoryRoot`.
- Produces: `RulesDocument.Build()` — текст справочника; используется задачей 3 как
  страница сайта.

- [x] **Step 1: Создать ветку**

```bash
rtk git switch develop
rtk git pull --ff-only origin develop
rtk git switch -c feature/docs-release
```

- [x] **Step 2: Написать сопоставление правила с описанием и примером**

Создать `tests/Typographer.Tests/Rules/RuleCatalog.cs`:

```csharp
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
internal static class RuleCatalog
{
    private static readonly Dictionary<string, string> Paths = BuildPaths();
    private static readonly Dictionary<string, string> Summaries = LoadSummaries();
    private static readonly string[] Samples = BuildSamples();

    /// <summary>Описание правила из XML-комментария. Пустая строка, если комментария нет.</summary>
    public static string DescriptionOf(RuleId rule)
        => Paths.TryGetValue(rule.Name, out string? path) && Summaries.TryGetValue(path, out string? summary)
            ? summary
            : string.Empty;

    /// <summary>Пример работы правила или null, если ни на одном образце оно ничего не меняет.</summary>
    public static string? ExampleOf(RuleId rule)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None.With(rule) });

        foreach (string sample in Samples)
        {
            string result = typograf.Process(sample);
            if (!string.Equals(result, sample, StringComparison.Ordinal))
            {
                return $"`{Show(sample)}` → `{Show(result)}`";
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

        // Короткие образцы вперёд: пример в справочнике должен помещаться в ячейку
        // таблицы и показывать одно правило, а не абзац вокруг него.
        return [.. samples
            .Select(sample => sample.Trim())
            .Where(sample => sample.Length is > 0 and <= 80)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(sample => sample.Length)
            .ThenBy(sample => sample, StringComparer.Ordinal)];
    }
}
```

- [x] **Step 3: Написать сборку текста справочника**

Создать `tests/Typographer.Tests/Rules/RulesDocument.cs`:

```csharp
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

        text.Append("\n");

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
```

- [x] **Step 4: Написать падающий тест**

Создать `tests/Typographer.Tests/Rules/RulesDocumentTests.cs`:

```csharp
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
```

- [x] **Step 5: Убедиться, что тест падает, и сгенерировать файл**

Выполнить: `dotnet test tests/Typographer.Tests`
Ожидается: `RulesDocumentIsUpToDate` падает — файла `docs/rules.md` ещё нет
(`FileNotFoundException`).

Создать файл перегенерацией:

```bash
TYPOGRAPHER_UPDATE_RULES=1 dotnet test tests/Typographer.Tests
```

Ожидается: падение с текстом «docs/rules.md перезаписан» — это успех режима перегенерации.

- [x] **Step 6: Прочитать сгенерированный файл глазами**

Открыть `docs/rules.md`. Проверить по трём пунктам:

1. Правил ровно столько, сколько в реестре, и они разложены по фазам.
2. У правил, которые реализованы, есть пример, и пример показывает именно это правило.
3. Правила без примера — это те, для которых он и не может подобраться: не реализованные
   (`common/punctuation/quoteLink`, `common/html/stripTags`, `common/html/processingAttrs`),
   отложенное `ru/typo/switchingKeyboardLayout` и правила, которым нужны опции HTML
   (`common/html/nbr`, `common/html/p`) или которых нет ни в одном тестовом входе.

Если пример у какого-то правила бессмысленный (образец меняется не из-за него), добавить
в `HardCases.All` короткий вход, показывающий правило, и перегенерировать: тестовые данные
и есть источник примеров, и пополнять надо их, а не справочник.

- [x] **Step 7: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Tests`
Ожидается: PASS, все тесты, включая три новых.

- [x] **Step 8: Коммит**

```bash
rtk git add tests/Typographer.Tests/Rules docs/rules.md
rtk git commit -m "docs: справочник правил генерируется из реестра"
```

---

## Task 2: Четыре страницы документации

Спецификация (раздел 11) обещает `docs/` по-русски: быстрый старт, архитектуру, рецепты и
источники. Всё это сейчас размазано по README и спецификации — README для того, кто
выбирает библиотеку, спецификация для того, кто её пишет, а страниц для того, кто ею
пользуется, нет.

**Files:**
- Create: `docs/index.md`
- Create: `docs/quickstart.md`
- Create: `docs/architecture.md`
- Create: `docs/recipes.md`
- Create: `docs/sources.md`

**Interfaces:**
- Consumes: ничего.
- Produces: страницы, на которые ссылается `docs/toc.yml` задачи 3.

- [x] **Step 1: Написать главную страницу**

Создать `docs/index.md`:

```markdown
# Typographer

Типограф для русского языка на .NET: кавычки-ёлочки с правильной вложенностью, тире вместо
дефисов там, где это тире, неразрывные пробелы там, где перенос недопустим, и корректные
символы вместо суррогатов. Работает и с обычным текстом, и с HTML-фрагментами.

```csharp
Typograf.Html("Он - человек и \"цитата\"");
// Он — человек и «цитата»
```

| Куда идти | Зачем |
|---|---|
| [Быстрый старт](quickstart.md) | установить и получить результат за пять минут |
| [Справочник правил](rules.md) | все правила, пресеты и примеры |
| [Рецепты](recipes.md) | ASP.NET Core, Markdown, командная строка, свой набор правил |
| [Архитектура](architecture.md) | как устроен конвейер и почему он однопроходный |
| [Источники](sources.md) | ГОСТ, Мильчин, практика студии Лебедева |
| [Спецификация](spec.md) | требования и решения целиком |

Ядро не имеет внешних зависимостей на `net8.0` и `net10.0`, работает на `netstandard2.0`
и не выбрасывает исключений ни на каком входе, кроме объявленного `OutputTooLargeException`.
```

- [x] **Step 2: Написать быстрый старт**

Создать `docs/quickstart.md`:

```markdown
# Быстрый старт

## Установка

```bash
dotnet add package Typographer
```

## Три способа вызвать

Статический фасад — когда настройки не нужны:

```csharp
using Typographer;

string html = Typograf.Html("<p>Он - человек</p>");
string text = Typograf.PlainText("Он - человек");
```

Настраиваемый типограф — иммутабельный и потокобезопасный, создаётся один раз и живёт
столько, сколько приложение:

```csharp
var typograf = new HtmlTypograf(new HtmlOptions
{
    Entities = EntityMode.Named,
    Rules = RuleSet.Default,
});

string html = typograf.Process(source);
```

Без промежуточной строки — ноль аллокаций в куче после прогрева пула:

```csharp
var writer = new ArrayBufferWriter<char>();
typograf.Process(source.AsSpan(), writer);
```

## Наборы правил

| Пресет | Когда брать |
|---|---|
| `RuleSet.Default` | по умолчанию: только оформление, смысл текста не меняется |
| `RuleSet.Lebedev` | поведение веб-сервиса `typograf.artlebedev.ru` |
| `RuleSet.Typograf` | паритет дефолтов с JS-typograf |
| `RuleSet.Gost` | строго по ГОСТ Р 7.0.110-2025 |
| `RuleSet.Minimal` | кавычки, тире, многоточие — и всё |
| `RuleSet.All` | всё, включая рискованное и требующее внешнего CSS |
| `RuleSet.None` | ничего: вход возвращается тем же экземпляром строки |

Набор правится точечно и остаётся иммутабельным:

```csharp
RuleSet rules = RuleSet.Default
    .Without(RuleId.Ru.Money.Ruble)
    .With(RuleId.Ru.OptAlign.Quote);
```

Полный список — в [справочнике правил](rules.md).

## Настройки HTML

| Настройка | Что делает |
|---|---|
| `Entities` | вид типографских символов: `Symbols`, `Named`, `Numeric`, `Mixed` |
| `UseBr` | перевод строки заменяется тегом переноса |
| `UseP` | абзацы оборачиваются в теги абзаца |
| `MaxNobr` | сколько слов объединять в неразрывный блок |
| `MaxOutputLength` | предел длины результата; при превышении — `OutputTooLargeException` |

`UseBr`, `UseP`, `MaxNobr` и `Entities` не имеют смысла вне HTML, и в `TextOptions` их
физически нет: невалидная комбинация не компилируется.

## Командная строка

```bash
dotnet tool install --global dotnet-typographer
echo 'Он - человек' | dotnet-typographer
dotnet-typographer --help
```
```

- [x] **Step 3: Написать страницу архитектуры**

Создать `docs/architecture.md`:

```markdown
# Архитектура

## Один проход, шесть фаз

Вход читается один раз. Порядок фаз жёсткий, внутри фазы правила независимы.

| # | Фаза | Что делает |
|---|---|---|
| 1 | `Prepare` | метка порядка байт, распознавание переводов строк, декодирование типографских сущностей |
| 2 | `Protect` | вход делится на текстовые узлы, разметку и защищённые зоны |
| 3 | `Scan` | посимвольный проход: кавычки, тире, апостроф, многоточие, символы, числа, пробелы |
| 4 | `Bind` | словарный проход по словам: неразрывные пробелы, сокращения, инициалы, единицы |
| 5 | `Layout` | неразрывные блоки, висячая пунктуация, переносы, абзацы |
| 6 | `Emit` | кодирование по `EntityMode` и запись в приёмник |

`Protect` не отдельный проход по документу: сегментация вызывается ВНУТРИ каждой фазы —
фаза сама идёт по сегментам, копирует разметку байт в байт и применяет свою логику только
к текстовым узлам.

## Буфер, который можно править задним числом

Всё пишется в растущий буфер поверх `ArrayPool<char>`. Буфер разрешает менять уже
записанные позиции по индексу — за счёт этого правила с длинным контекстом работают без
второго прохода: правило, которому нужен взгляд назад, не откладывает решение, а
исправляет то, что уже записано.

Буфер — изменяемая структура, передаётся только по ссылке и никогда не боксируется: иначе
обещание про ноль аллокаций не выполнялось бы.

## Правила как биты

`RuleSet` — иммутабельная маска на 128 бит (два `ulong`). Проверка «включено ли правило»
стоит одну инструкцию и не аллоцирует, поэтому её можно делать в горячем цикле для
каждого символа. `RuleId.Index` — позиция бита; индекс 0 зарезервирован под
`default(RuleId)`, чтобы значение по умолчанию не совпадало ни с одним правилом.

Реестр закрыт: 107 правил, имена совпадают с именами JS-typograf.

## Гарантии

1. Время работы линейно от длины входа.
2. Исключений нет ни на каком входе, кроме объявленного `OutputTooLargeException`.
3. Разметка копируется байт в байт. Исключение — правило `common/html/escape`, которое
   для того и существует.
4. `RuleSet.Default` никогда не превращает текст в разметку.
5. Повторный прогон по собственному выводу ничего не меняет. Исключения объявлены:
   `UseBr`, `MaxNobr`, `common/nbsp/replaceNbsp`, `common/html/nbr`, `common/html/escape`.
6. Если правок нет, `Process(string)` возвращает тот же экземпляр строки.
7. Ломаный UTF-16 проходит насквозь без исключения и без потери символов.

Гарантии проверяются тестами, а не декларируются: см. `tests/Typographer.Tests/Guarantees`.

## Как это проверяется

| Уровень | Что ловит |
|---|---|
| Unit и property | правила поштучно и каждое правило реестра в одиночку |
| Golden-корпус | связный текст целиком, пары «вход — эталон» в файлах |
| Снимок оракула | расхождения с `typograf.artlebedev.ru`, закреплённые таблицей |
| Живая сверка | не устарел ли сам снимок (с сетью, вне CI) |
| Фаззинг | падение конвейера на произвольном входе |
```

- [x] **Step 4: Написать рецепты**

Создать `docs/recipes.md`:

```markdown
# Рецепты

## Свой набор правил

```csharp
RuleSet rules = RuleSet.Default
    .Without(RuleId.Common.Space.DelBeforePercent)   // ГОСТ 9.6 требует там пробел
    .With(RuleId.Common.Number.DigitGrouping);       // 1 000 000 вместо 1000000

var typograf = new HtmlTypograf(new HtmlOptions { Rules = rules });
```

Правило можно найти и по имени, как в JS-typograf:

```csharp
if (RuleId.TryParse("ru/nbsp/abbr", out RuleId rule))
{
    rules = rules.Without(rule);
}
```

## Контейнер

```bash
dotnet add package Typographer.DependencyInjection
```

```csharp
services.AddTypograf();                                        // настройки по умолчанию
services.AddTypograf(new HtmlOptions { Entities = EntityMode.Named });
```

Регистрируются одиночками `HtmlTypograf` и `TextTypograf`. Своя регистрация, сделанная
раньше, побеждает: внутри `TryAddSingleton`.

## ASP.NET Core

```bash
dotnet add package Typographer.AspNetCore
```

```cshtml
@addTagHelper *, Typographer.AspNetCore

<typograf><p>Он - человек и "цитата"</p></typograf>
```

Тег-хелпер типографирует содержимое и исчезает сам. Требует `services.AddTypograf()`.
Там, где удобнее вызов, а не элемент:

```cshtml
@inject HtmlTypograf Typograf
@Typograf.ToHtmlContent(Model.Text)
```

## Markdown

```bash
dotnet add package Typographer.Markdig
```

```csharp
MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseTypograf().Build();
string html = Markdown.ToHtml(source, pipeline);
```

Правки вносятся в дерево документа после разбора, а не в готовый HTML: рендерер кодирует
прямую кавычку в `&quot;`, и в отрендеренном HTML ёлочки уже не появились бы. Поэтому код,
адреса ссылок и встроенный HTML остаются нетронутыми, а кавычки вокруг разметки —
`"**слово**"` — смотрят в разные стороны.

## Командная строка

```bash
dotnet tool install --global dotnet-typographer

echo 'Он - человек' | dotnet-typographer
dotnet-typographer --entities named --in-place статья.html
dotnet-typographer --rules gost --text заметка.txt
dotnet-typographer --list-rules
```

## Без аллокаций

```csharp
var writer = new ArrayBufferWriter<char>();
typograf.Process(source.AsSpan(), writer);
```

Путь `Process(ReadOnlySpan<char>, IBufferWriter<char>)` после прогрева пула не выделяет в
куче ничего. Путь `Process(string)` выделяет ровно одну строку результата — и ту не
выделяет, если правок не было.

## Защита от разрастания вывода

```csharp
var typograf = new HtmlTypograf(new HtmlOptions { MaxOutputLength = 1_000_000 });
```

Превышение предела даёт `OutputTooLargeException` — единственное исключение, которое
типограф имеет право выбросить. Приёмник при этом не получает половину результата:
предел проверяется до записи.

## Не трогать кусок текста

Содержимое `code`, `pre`, `script`, `style`, `textarea`, `kbd`, `samp` и комментариев
защищено само. Если нужно защитить что-то ещё, оберните это в один из этих элементов или
выключите правило точечно через `Without`.
```

- [x] **Step 5: Написать страницу источников**

Создать `docs/sources.md`:

```markdown
# Источники

Приоритет при расхождении: **ГОСТ Р 7.0.110-2025 ⇒ Мильчин и Чельцова ⇒ практика студии
Лебедева ⇒ JS-typograf**. Порядок не декоративный: он решает споры, и каждое решение,
принятое не по первому источнику, записано как расхождение.

| Источник | Что берём |
|---|---|
| ГОСТ Р 7.0.110-2025 | неразрывные пробелы (9.4–9.5), диапазоны (14.3), дефис и тире (16.1–16.2), кавычки и вложенность (16.3), знак номера (16.4), скобки (16.5) |
| Мильчин, Чельцова. «Справочник издателя и автора» | сокращения, числа и знаки, даты и время, цитаты |
| type.today, «Справочник: кавычки» | три уровня вложенности `«» → „“ → ‘’`, отличие кавычек от штрихов |
| typograf/typograf (MIT) | имена и состав 107 правил, набор регрессионных случаев |
| typograf.artlebedev.ru | семантика `entityType`, `useBr`, `useP`, `maxNobr`; поведение как оракул |

## Известные расхождения

ГОСТ 14.3 требует диапазон без отбивки (`1941—1945`), практика веба ставит неразрывный
пробел. `RuleSet.Default` следует ГОСТу, `RuleSet.Lebedev` — практике.

Полный список расхождений с сервисом Лебедева — [`oracle/divergences.md`](oracle/divergences.md).
Он не написан руками: список живёт в коде теста и падает в обе стороны — и когда
совпадение сломалось, и когда записанное расхождение исчезло.

Снимок ответов сервиса — [`oracle/lebedev.md`](oracle/lebedev.md). Живая сверка со
службой вынесена в отдельный проект и отвечает на другой вопрос: не устарел ли сам снимок.

## Чего типограф не делает

Ёфикация, проверка орфографии, расстановка переносов, языки кроме русского (английские
правила тире — только как опция) и разбор HTML целиком: библиотека работает с фрагментами
и не нормализует чужую разметку.
```

- [x] **Step 6: Проверить ссылки**

Выполнить: `rtk ls docs`
Ожидается: `index.md`, `quickstart.md`, `architecture.md`, `recipes.md`, `sources.md`,
`rules.md`, `spec.md`, `perf.md`, каталог `oracle`. Все относительные ссылки со страниц
ведут на существующие файлы.

- [x] **Step 7: Коммит**

```bash
rtk git add docs
rtk git commit -m "docs: быстрый старт, архитектура, рецепты, источники"
```

---

## Task 3: Сайт DocFX на GitHub Pages

**Files:**
- Create: `docs/docfx.json`
- Create: `docs/toc.yml`
- Create: `.github/workflows/docs.yml`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: страницы задачи 2 и `docs/rules.md` задачи 1.
- Produces: сайт по адресу `https://gberikov.github.io/Typographer/`.

- [x] **Step 1: Написать конфигурацию DocFX**

Создать `docs/docfx.json`:

```json
{
  "metadata": [
    {
      "src": [
        {
          "src": "../src",
          "files": ["**/*.csproj"]
        }
      ],
      "dest": "api",
      "properties": {
        "TargetFramework": "net10.0"
      }
    }
  ],
  "build": {
    "content": [
      {
        "files": ["**/*.{md,yml}"],
        "exclude": ["_site/**", "superpowers/**"]
      }
    ],
    "output": "_site",
    "template": ["default", "modern"],
    "globalMetadata": {
      "_appName": "Typographer",
      "_appTitle": "Typographer",
      "_enableSearch": true,
      "_gitContribute": {
        "repo": "https://github.com/gberikov/Typographer",
        "branch": "master"
      }
    }
  }
}
```

Планы из `superpowers/` исключены намеренно: это рабочие документы разработки, а не
документация библиотеки, и на сайте они только мешали бы поиску.

- [x] **Step 2: Написать навигацию**

Создать `docs/toc.yml`:

```yaml
- name: Быстрый старт
  href: quickstart.md
- name: Справочник правил
  href: rules.md
- name: Рецепты
  href: recipes.md
- name: Архитектура
  href: architecture.md
- name: Источники
  href: sources.md
- name: Производительность
  href: perf.md
- name: Спецификация
  href: spec.md
- name: API
  href: api/
```

- [x] **Step 3: Проверить сборку сайта локально**

```bash
rtk dotnet tool install --global docfx --version 2.78.5
rtk docfx docs/docfx.json
```

Ожидается: `Build succeeded`, каталог `docs/_site` с `index.html` и разделом `api`.
Предупреждения про отсутствующие XML-комментарии допустимы только для проектов, где
`GenerateDocumentationFile` выключен (утилита командной строки); для остальных их быть
не должно.

- [x] **Step 4: Закрыть результат сборки от git**

В `.gitignore` добавить:

```
docs/_site/
docs/api/
```

`docs/api` — сгенерированные DocFX YAML-файлы по коду; в репозитории им делать нечего,
они пересобираются на каждой сборке сайта.

- [x] **Step 5: Написать workflow**

Создать `.github/workflows/docs.yml`:

```yaml
name: Docs

on:
  push:
    tags: ['[0-9]*']
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

# Публикация страниц не терпит параллельных прогонов, но и отменять уже начатую
# выкладку нельзя: получится наполовину выложенный сайт.
concurrency:
  group: pages
  cancel-in-progress: false

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: |
            8.0.x
            10.0.x

      - run: dotnet tool install --global docfx --version 2.78.5
      - run: docfx docs/docfx.json
      - uses: actions/upload-pages-artifact@v4
        with:
          path: docs/_site

  deploy:
    needs: build
    # На пул-реквесте выкладки не происходит: там проверяется только сборка сайта.
    if: github.event_name != 'pull_request'
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - id: deployment
        uses: actions/deploy-pages@v4
```

- [x] **Step 6: Включить GitHub Pages**

Страницы репозитория ещё не включены (`gh api repos/gberikov/Typographer/pages` отвечает
404), а `actions/deploy-pages` работает только при источнике «GitHub Actions»:

```bash
rtk gh api -X POST repos/gberikov/Typographer/pages -f build_type=workflow
```

Ожидается: JSON с `"build_type": "workflow"`. Повторный вызов даёт 409 — это тоже успех,
значит страницы уже включены.

- [x] **Step 7: Проверить workflow на пул-реквесте**

`workflow_dispatch` не сработает, пока файл не дойдёт до ветки по умолчанию (`master`), —
это уже проверено в плане 3 на фаззинге. Чтобы проверить сборку сайта до слияния, временно
добавить в `docs.yml` триггер:

```yaml
on:
  pull_request:
  push:
    tags: ['[0-9]*']
  workflow_dispatch:
```

Открыть пул-реквест, дождаться прогона, убедиться, что задача `build` зелёная, а `deploy`
пропущена. После этого триггер `pull_request` из файла УБРАТЬ и закоммитить — постоянно
собирать сайт на каждом пул-реквесте незачем.

- [x] **Step 8: Коммит**

```bash
rtk git add docs/docfx.json docs/toc.yml .github/workflows/docs.yml .gitignore
rtk git commit -m "docs: сайт DocFX на GitHub Pages"
```

---

## Task 4: Публикация в NuGet по тегу

**Files:**
- Create: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: пять упаковываемых проектов и MinVer.
- Produces: workflow, публикующий пакеты и создающий релиз на GitHub.

- [x] **Step 1: Написать workflow**

Создать `.github/workflows/release.yml`:

```yaml
name: Release

on:
  push:
    tags: ['[0-9]*']
  workflow_dispatch:

permissions:
  contents: write

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      # Полная история и теги обязательны: версию пакетов считает MinVer,
      # а по мелкой копии он выдаст 0.0.0-alpha.
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: |
            8.0.x
            10.0.x

      - run: dotnet build -c Release
      - run: dotnet test -c Release
      - run: dotnet pack -c Release -o artifacts/packages
      - run: ls -l artifacts/packages

      - name: Публикация в NuGet
        if: startsWith(github.ref, 'refs/tags/')
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: |
          if [ -z "$NUGET_API_KEY" ]; then
            echo "::error::Секрет NUGET_API_KEY не задан — публиковать нечем."
            exit 1
          fi
          dotnet nuget push "artifacts/packages/*.nupkg" \
            --api-key "$NUGET_API_KEY" \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate

      - name: Релиз на GitHub
        if: startsWith(github.ref, 'refs/tags/')
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          gh release create "$GITHUB_REF_NAME" \
            --title "$GITHUB_REF_NAME" \
            --generate-notes \
            artifacts/packages/*.nupkg
```

Заголовок релиза — голое имя тега, то есть голый номер версии: имя репозитория и так
написано вокруг. Ручной запуск (`workflow_dispatch`) доходит до упаковки и останавливается
перед публикацией — так проверяется, что сборка и упаковка живы, без выкладки в NuGet.

Символьные пакеты (`.snupkg`) отдельной строкой не публикуются: `dotnet nuget push`
отправляет их сам вместе с одноимённым `.nupkg`.

- [x] **Step 2: Проверить упаковку локально**

Выполнить: `rtk dotnet pack -c Release -o artifacts/packages`

Ожидается: пять файлов `.nupkg` — `Typographer`, `Typographer.DependencyInjection`,
`Typographer.AspNetCore`, `Typographer.Markdig`, `dotnet-typographer`.

```bash
rtk ls artifacts/packages
```

- [x] **Step 3: Записать, чего не хватает для публикации**

Ключ NuGet в секретах репозитория не задан (`gh secret list` пуст), и завести его может
только владелец: ключ выпускается на nuget.org и в репозиторий кладётся руками.

В `README.md`, в раздел «Ветвление — git flow», добавить абзац:

```markdown
Релиз: тег вида `0.1.0` (без префикса `v`) на `master` запускает workflow `Release` —
он собирает, прогоняет тесты, упаковывает пять пакетов, публикует их в NuGet и создаёт
релиз на GitHub. Для публикации нужен секрет репозитория `NUGET_API_KEY`; без него
workflow останавливается с понятной ошибкой, а не публикует половину.
```

- [x] **Step 4: Коммит**

```bash
rtk git add .github/workflows/release.yml README.md
rtk git commit -m "ci: публикация пакетов в NuGet по тегу"
```

---

## Task 5: README и очередь планов

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/plans/2026-09-08-typographer-core.md`

**Interfaces:**
- Consumes: всё сделанное задачами 1–4.
- Produces: ничего для кода.

- [x] **Step 1: Убрать устаревший статус из README**

В `README.md` блок «Статус» сейчас утверждает, что работают двенадцать правил, а остальные
из 107 придут позже. Это перестало быть правдой ещё в плане 2. Заменить блок на:

```markdown
> Статус: реестр закрыт — 107 правил, шесть фаз конвейера, пять пакетов. Не реализованы
> три правила, которые не могут быть реализованы без нарушения гарантий
> (`common/punctuation/quoteLink`, `common/html/stripTags`, `common/html/processingAttrs`),
> и отложено `ru/typo/switchingKeyboardLayout`. Публикации в NuGet ещё не было.
```

- [x] **Step 2: Добавить ссылку на документацию**

В `README.md` сразу после значка CI добавить строку:

```markdown
Документация: <https://gberikov.github.io/Typographer/> · [справочник правил](docs/rules.md)
```

- [x] **Step 3: Отметить план в очереди**

В `docs/superpowers/plans/2026-09-08-typographer-core.md`, таблица «Очередь планов»,
строку про план 5 привести к виду:

```markdown
| 5. Документация и релиз ✅ | Генератор `docs/rules.md` из метаданных, сайт DocFX на GitHub Pages, публикация в NuGet по тегу. План — `2026-09-10-docs-release.md` | 2, 4 |
```

- [x] **Step 4: Прогнать всё**

Выполнить: `rtk dotnet build -c Release` и `dotnet test -c Release`
Ожидается: сборка без предупреждений, все тесты зелёные.

- [x] **Step 5: Коммит**

```bash
rtk git add README.md docs
rtk git commit -m "docs: актуальный статус и ссылка на сайт документации"
```

- [x] **Step 6: Завершение работы**

Открыть PR в `develop`, дождаться зелёного `build` и слить через PR — прямой push в
`develop` защита ветки не пропустит.

---

## Self-Review

**Покрытие спецификации:**

| Требование спецификации | Задача |
|---|---|
| 11, `docs/rules.md` генерируется из метаданных | 1 |
| 11, примеры берутся из реальных тестов | 1, примеры подбираются по `HardCases` и корпусу |
| 11, тест в CI падает, если файл разошёлся с кодом | 1 |
| 11, `docs/` по-русски: быстрый старт, архитектура, рецепты, источники | 2 |
| 11, сайт DocFX публикуется на GitHub Pages при релизе | 3 |
| 12, теги вида `1.2.3` без префикса | 4, триггер `'[0-9]*'` |
| 12, публикация в NuGet | 4 |

**Чего в плане нет и почему:**

| Не делаем | Почему |
|---|---|
| Отдельное поле описания в реестре правил | описание уже обязано быть в XML-комментарии; второе место для того же текста разошлось бы с первым |
| Примеры, написанные руками для каждого из 107 правил | спецификация требует примеров ИЗ ТЕСТОВ; подбор по тестовым данным даёт то же, но не может протухнуть |
| Публикация сайта на каждый push в `master` | спецификация говорит «при релизе»; лишние выкладки сайта путают тем, что документация опережает опубликованный пакет |
| Проверка публикации в NuGet | ключа в секретах нет и завести его может только владелец; проверяется всё до `push`, а сам `push` защищён `--skip-duplicate` |
| Trusted Publishing вместо ключа | требует настройки политики на nuget.org — то же действие владельца, но с большим числом шагов |
| Перевод README на страницы сайта | README читает тот, кто выбирает библиотеку, страницы — тот, кто уже выбрал; это разные тексты, а не один в двух местах |
