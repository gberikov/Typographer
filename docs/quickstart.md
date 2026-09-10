# Быстрый старт

## Установка

```bash
dotnet add package Typographer
```

## Три способа вызвать

Статический фасад — когда настройки не нужны:

```csharp
using Typographer;

string html = Typographer.Html("<p>Он - человек</p>");
string text = Typographer.PlainText("Он - человек");
```

Настраиваемый типограф — иммутабельный и потокобезопасный, создаётся один раз и живёт
столько, сколько приложение:

```csharp
var typographer = new HtmlTypographer(new HtmlOptions
{
    Entities = EntityMode.Named,
    Rules = RuleSet.Default,
});

string html = typographer.Process(source);
```

Без промежуточной строки — ноль аллокаций в куче после прогрева пула:

```csharp
var writer = new ArrayBufferWriter<char>();
typographer.Process(source.AsSpan(), writer);
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
