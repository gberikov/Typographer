# Typographer

Типограф для русского языка на .NET: кавычки-ёлочки, тире, неразрывные пробелы.

> Статус: каркас репозитория. Правила типографики ещё не реализованы.

## Использование

```csharp
using Typographer;

var text = RussianTypographer.Format("\"Привет\" - мир");
```

## Целевые платформы

| TFM | Зачем |
|-----|-------|
| `netstandard2.0` | .NET Framework 4.6.1+, Unity, Xamarin, старые библиотеки |
| `net8.0` | текущая LTS |
| `net10.0` | актуальная LTS, `Span`/`SearchValues`, AOT |

## Разработка

```
dotnet build
dotnet test
```

## Производительность

Бенчмарки — `bench/Typographer.Bench` (BenchmarkDotNet). Запуск: `dotnet run -c Release --project bench/Typographer.Bench`.

Цель: обработка HTML не более чем вдвое медленнее двух последовательных `string.Replace` по тому же тексту; путь записи в `IBufferWriter<char>` не аллоцирует в куче.

Прогон: 2026-09-08, 13th Gen Intel Core i9-13980HX 2.20GHz (1 CPU, 32 логических / 24 физических ядра), Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2), .NET SDK 10.0.400, среда выполнения .NET 10.0.11 (X64 RyuJIT x86-64-v3), BenchmarkDotNet v0.15.8.

| Method             | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |-----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| StringReplace      |   9.429 us | 0.2783 us | 0.7985 us |  1.01 |    0.12 | 4.6997 | 0.3815 |   88848 B |        1.00 |
| Html               | 147.287 us | 2.0672 us | 2.9647 us | 15.73 |    1.34 | 2.1973 |      - |   44424 B |        0.50 |
| HtmlToBufferWriter | 143.312 us | 2.1091 us | 1.8697 us | 15.31 |    1.29 |      - |      - |         - |        0.00 |

**Аллокации:** цель достигнута — `HtmlToBufferWriter` показывает `Allocated` = 0 B/op (Alloc Ratio 0.00) после прогрева.

**Скорость:** цель не достигнута — `Html` в ~15.7 раза медленнее `StringReplace` по среднему времени вместо требуемых не более чем в 2 раза. Требует доработки конвейера ядра.

## Ветвление — git flow

- `master` — релизы, тег вида `1.2.3` на каждый релиз (версия пакета — MinVer);
- `develop` — интеграционная ветка;
- `feature/*`, `release/*`, `hotfix/*` — как в git flow.

`master`, `develop` и теги `*.*.*` защищены правилами репозитория: только PR, без force-push и удаления.

## Лицензия

MIT
