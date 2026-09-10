# Typographer

[![CI](https://github.com/gberikov/Typographer/actions/workflows/ci.yml/badge.svg)](https://github.com/gberikov/Typographer/actions/workflows/ci.yml)

Типограф для русского языка на .NET: кавычки-ёлочки, тире, неразрывные пробелы, работает
и с обычным текстом, и с HTML-фрагментами.

> Статус: ядро шестифазного конвейера и двенадцать правил типографики работают и покрыты
> тестами (кавычки, тире, дефис, многоточие, апостроф, пробелы вокруг пунктуации,
> неразрывные пробелы после коротких слов, в сокращениях и при инициалах). Ещё два
> зарегистрированных правила (`ru/punctuation/ano`, `ru/typo/switchingKeyboardLayout`) и
> оставшаяся часть из 107 правил спецификации придут в следующих версиях.

## Использование

Быстрый старт — статический фасад с настройками по умолчанию:

```csharp
using Typographer;

string html = Typograf.Html("Он сказал: \"Привет!\" - и махнул рукой.");
string text = Typograf.PlainText("Он сказал: \"Привет!\" - и махнул рукой.");
```

Настраиваемый вариант — свой набор правил, кодирование сущностей, перенос строк и абзацы:

```csharp
using Typographer;
using Typographer.Rules;

var typograf = new HtmlTypograf(new HtmlOptions
{
    Rules = RuleSet.Default.Without(RuleId.Ru.Nbsp.Initials),
    Entities = EntityMode.Named,
    UseBr = true,
    MaxNobr = 3,
});

string html = typograf.Process("Он сказал: \"Привет!\" - и махнул рукой.\nВторая строка.");
```

Для обычного текста — `TextTypograf` и `TextOptions` (без `Entities`, `UseBr`, `UseP`,
`MaxNobr`: они имеют смысл только в HTML, поэтому в `TextOptions` их физически нет):

```csharp
var typograf = new TextTypograf(new TextOptions { Rules = RuleSet.Minimal });
string text = typograf.Process("Он сказал: \"Привет!\" - и махнул рукой.");
```

Обе точки входа умеют писать результат прямо в приёмник без промежуточной строки:

```csharp
using System.Buffers;

var writer = new ArrayBufferWriter<char>();
HtmlTypograf.Default.Process("Он сказал: \"Привет!\"".AsSpan(), writer);
```

### Наборы правил

`RuleSet` — иммутабельное множество включённых правил с готовыми пресетами:

| Пресет | Смысл |
|---|---|
| `RuleSet.Default` | безопасная типографика — пресет по умолчанию |
| `RuleSet.Minimal` | только кавычки, тире и многоточие |
| `RuleSet.All` | все зарегистрированные правила, включая ещё не реализованные |
| `RuleSet.None` | ничего не менять |
| `RuleSet.Lebedev`, `RuleSet.Gost`, `RuleSet.Typograf` | пока совпадают с `Default` — правила, которые должны их различать, ещё не реализованы (см. XML-комментарии на этих пресетах) |

```csharp
RuleSet rules = RuleSet.Default.With(RuleId.Ru.Dash.Years).Without(RuleId.Ru.Nbsp.Abbr);
bool enabled = rules.Contains(RuleId.Ru.Dash.Years);
```

### Идемпотентность и разметка

Правила `RuleSet` идемпотентны: повторный прогон ничего не меняет. Исключение — опции
`UseBr` и `MaxNobr`: они рассчитаны на однократное применение к исходному тексту. Прогон
по СОБСТВЕННОМУ ВЫВОДУ типографа с той же опцией вложит разметку в саму себя
(`<nobr><nobr>текст</nobr></nobr>`) — типограф не распознаёт свой прошлый вывод, это
намеренное решение (см. `docs/spec.md`, гарантия 5).

`UseP` в это исключение не входит: абзацы размечаются по документу целиком, а не по
каждому текстовому узлу, и во входе с готовой блочной разметкой (`<p>`, `<ul>`, `<div>`…)
опция не применяется — границы абзацев там задаёт сама разметка.
Незакрытые теги и защищённые области также отключают `UseP`, чтобы добавленный `</p>`
не оказался внутри атрибута, комментария или скрипта. `UseBr` обрабатывает только текст
вне тегов и защищённых областей.

BOM (`U+FEFF`) удаляется только в начале документа. Внутри текста этот символ сохраняется:
его удаление могло бы превратить текст в HTML-тег или сущность.

## Целевые платформы

| TFM | Зачем |
|-----|-------|
| `netstandard2.0` | .NET Framework 4.6.1+, Unity, Xamarin, старые библиотеки |
| `net8.0` | текущая LTS |
| `net10.0` | актуальная LTS, `Span`/`SearchValues`, AOT |

## Пакеты

| Пакет | Зачем | Зависимости | Платформы |
|---|---|---|---|
| `Typographer` | ядро: HTML и обычный текст, правила, пресеты | нет на `net8.0` и `net10.0`; `System.Memory` на `netstandard2.0` | `netstandard2.0`, `net8.0`, `net10.0` |
| `Typographer.DependencyInjection` | `AddTypograf()` | `Microsoft.Extensions.DependencyInjection.Abstractions` | `netstandard2.0`, `net8.0`, `net10.0` |
| `Typographer.AspNetCore` | тег-хелпер `<typograf>` и `IHtmlContent` | ASP.NET Core | `net8.0`, `net10.0` |
| `Typographer.Markdig` | типографика Markdown | `Markdig` | `netstandard2.0`, `net8.0`, `net10.0` |
| `dotnet-typograf` | утилита командной строки | ядро | `net10.0` |

### Контейнер

```csharp
services.AddTypograf();                                        // настройки по умолчанию
services.AddTypograf(new HtmlOptions { Entities = EntityMode.Named });
```

Регистрируются одиночками `HtmlTypograf` и `TextTypograf`. Своя регистрация, сделанная
раньше, побеждает: внутри `TryAddSingleton`.

### ASP.NET Core

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

### Markdown

```csharp
MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseTypograf().Build();
string html = Markdown.ToHtml(source, pipeline);
```

Правки вносятся в дерево документа после разбора, а не в готовый HTML: рендерер кодирует
прямую кавычку в `&quot;`, и в отрендеренном HTML ёлочки уже не появились бы. Поэтому код,
адреса ссылок и встроенный HTML остаются нетронутыми, а кавычки вокруг разметки —
`"**слово**"` — смотрят в разные стороны.

### Командная строка

```bash
dotnet tool install --global dotnet-typograf
echo 'Он - человек' | dotnet-typograf
dotnet-typograf --entities named --in-place статья.html
dotnet-typograf --help
```

## Разработка

```
dotnet build
dotnet test
```

Четыре уровня проверки, три из них в CI:

| Уровень | Что проверяет | Команда |
|---|---|---|
| Unit и property | правила поштучно, гарантии, каждое правило реестра в одиночку | `dotnet test` |
| Golden-корпус | `tests/Typographer.Corpus` — пары «вход — эталон» | `dotnet test` |
| Снимок оракула | наши расхождения с `typograf.artlebedev.ru` (без сети) | `dotnet test` |
| Живая сверка | не устарел ли сам снимок (с сетью, вне CI) | `TYPOGRAPHER_ORACLE=1 dotnet test tests/Typographer.Oracle` |
| Фаззинг | конвейер не падает ни на каком входе | workflow `Fuzz`, по расписанию |

Эталоны корпуса и список расхождений перегенерируются переменными
`TYPOGRAPHER_UPDATE_CORPUS=1` и `TYPOGRAPHER_UPDATE_ORACLE=1`. Тест при этом падает
намеренно: перезапись эталона — не проверка.

## Производительность

Бенчмарки — `bench/Typographer.Bench` (BenchmarkDotNet).
Запуск: `dotnet run -c Release --project bench/Typographer.Bench`.

Последний замер — `docs/perf.md`. Коротко, на входе в 22 200 символов:

| Сценарий | Пропускная способность | Аллокации |
|---|---:|---:|
| `Html(string)` | 28,0 млн симв./с | сама возвращаемая строка |
| `Html(span, IBufferWriter)` | 27,3 млн симв./с | **0 байт** |

Обещание про ноль аллокаций выполнено. Обещание про 50 млн символов в секунду — нет:
получается 28. Разрыв записан и объяснён в `docs/perf.md`; там же сравнение с замером
плана 1, когда правил было четырнадцать вместо ста семи.

## Ветвление — git flow

- `master` — релизы, тег вида `1.2.3` на каждый релиз (версия пакета — MinVer);
- `develop` — интеграционная ветка;
- `feature/*`, `release/*`, `hotfix/*` — как в git flow.

`master`, `develop` и теги `*.*.*` защищены правилами репозитория: только PR, без force-push и удаления.

## Лицензия

MIT
