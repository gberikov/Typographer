# Рецепты

## Свой набор правил

```csharp
RuleSet rules = RuleSet.Default
    .Without(RuleId.Common.Space.DelBeforePercent)   // ГОСТ 9.6 требует там пробел
    .With(RuleId.Common.Number.DigitGrouping);       // 1 000 000 вместо 1000000

var typographer = new HtmlTypographer(new HtmlOptions { Rules = rules });
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
services.AddTypographer();                                        // настройки по умолчанию
services.AddTypographer(new HtmlOptions { Entities = EntityMode.Named });
```

Регистрируются одиночками `HtmlTypographer` и `TextTypographer`. Своя регистрация, сделанная
раньше, побеждает: внутри `TryAddSingleton`.

## ASP.NET Core

```bash
dotnet add package Typographer.AspNetCore
```

```cshtml
@addTagHelper *, Typographer.AspNetCore

<typographer><p>Он - человек и "цитата"</p></typographer>
```

Тег-хелпер типографирует содержимое и исчезает сам. Требует `services.AddTypographer()`.
Там, где удобнее вызов, а не элемент:

```cshtml
@inject HtmlTypographer Typographer
@Typographer.ToHtmlContent(Model.Text)
```

## Markdown

```bash
dotnet add package Typographer.Markdig
```

```csharp
MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseTypographer().Build();
string html = Markdown.ToHtml(source, pipeline);
```

Правки вносятся в дерево документа после разбора, а не в готовый HTML: рендерер кодирует
прямую кавычку в `&quot;`, и в отрендеренном HTML ёлочки уже не появились бы. Поэтому код,
адреса ссылок и встроенный HTML остаются нетронутыми, а кавычки вокруг разметки —
`"**слово**"` — смотрят в разные стороны.

Защищено и содержимое HTML-элементов `code`, `pre`, `kbd`, `samp`, `script`, `style` и
`textarea`, записанных прямо в Markdown: для Markdig `<code>a - b</code>` — это три узла,
средний из которых обычный текст, и без учёта таких зон типограф правил бы код и клавиши.
Markdown-код в обратных кавычках (`` `a - b` ``) защищён и без этого — он отдельный тип узла.

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
typographer.Process(source.AsSpan(), writer);
```

Путь `Process(ReadOnlySpan<char>, IBufferWriter<char>)` после прогрева пула не выделяет в
куче ничего. Путь `Process(string)` выделяет ровно одну строку результата — и ту не
выделяет, если правок не было.

## Защита от разрастания вывода

```csharp
var typographer = new HtmlTypographer(new HtmlOptions { MaxOutputLength = 1_000_000 });
```

Превышение предела даёт `OutputTooLargeException` — единственное исключение, которое
типограф имеет право выбросить. Приёмник при этом не получает половину результата:
предел проверяется до записи.

## Не трогать кусок текста

Содержимое `code`, `pre`, `script`, `style`, `textarea`, `kbd`, `samp` и комментариев
защищено само. Если нужно защитить что-то ещё, оберните это в один из этих элементов или
выключите правило точечно через `Without`.
