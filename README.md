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

## Ветвление — git flow

- `master` — релизы, тег вида `1.2.3` на каждый релиз (версия пакета — MinVer);
- `develop` — интеграционная ветка;
- `feature/*`, `release/*`, `hotfix/*` — как в git flow.

`master`, `develop` и теги `*.*.*` защищены правилами репозитория: только PR, без force-push и удаления.

## Лицензия

MIT
