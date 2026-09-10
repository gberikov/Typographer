# План 4. Интеграции

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** четыре пакета вокруг ядра — регистрация в контейнере, тег-хелпер ASP.NET Core,
расширение Markdig и консольная утилита `dotnet-typograf`, — каждый со своей внешней
зависимостью и ни одной новой зависимостью у ядра.

**Architecture:** ядро не меняется ни на строчку. Граница пакета — внешняя зависимость
(спецификация, раздел 10): всё, что тянет `Microsoft.Extensions.DependencyInjection.Abstractions`,
`Microsoft.AspNetCore.App` или `Markdig`, живёт в отдельном проекте. Каждая интеграция —
тонкий переходник к уже существующему `HtmlTypograf`/`TextTypograf`: типографы иммутабельны
и потокобезопасны, поэтому регистрируются одиночками и передаются по ссылке, а не
пересоздаются.

**Tech Stack:** C# 13, .NET 10 SDK, `Microsoft.Extensions.DependencyInjection.Abstractions`
10.0.12, `Microsoft.AspNetCore.App` (framework reference), `Markdig` 1.3.2, xunit.v3 3.2.0
на Microsoft.Testing.Platform.

**Spec:** `docs/spec.md`, раздел 10 «Пакеты» и раздел 4 «Публичный API».

## Global Constraints

- Целевые платформы: `netstandard2.0;net8.0;net10.0` для `Typographer.DependencyInjection`
  и `Typographer.Markdig` (обе зависимости эти платформы поддерживают), `net8.0;net10.0`
  для `Typographer.AspNetCore`, `net10.0` для CLI и тестов.
- Ядро остаётся без внешних зависимостей. Ни один пакет этого плана не добавляет
  `PackageReference` в `src/Typographer/Typographer.csproj`.
- `TreatWarningsAsErrors` и `EnforceCodeStyleInBuild` включены на весь репозиторий
  (`Directory.Build.props`). `GenerateDocumentationFile` включается в каждом библиотечном
  пакете, значит **каждый публичный член обязан иметь XML-комментарий по-русски**.
- `dotnet test` запускается БЕЗ префикса `rtk` — под Microsoft.Testing.Platform фильтр rtk
  печатает «0 tests, exit code 5», хотя тесты проходят. Все остальные команды — с `rtk`.
- Ветка работы — `feature/integrations`, ответвлённая от `develop`. Прямой коммит в `master`
  и `develop` запрещён правилами репозитория.
- Версии пакетов задаёт MinVer по тегам без префикса `v`. Каждый упаковываемый проект
  получает `PackageReference Include="MinVer" PrivateAssets="all"`.
- Комментарии, имена тестов и документация — по-русски, как во всём репозитории.
  Соглашение об именах тестов: первая часть до подчёркивания латиницей (имя проверяемого
  члена), вторая по-русски (ожидаемое поведение).
- Невидимые символы в исходниках literal-ом не пишутся: неразрывный пробел — только
  `"\u00A0"` или `Chars.Nbsp`, иначе его не видно в diff.
- Каждый коммит заканчивается строками:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
  ```

---

## Файловая структура

| Файл | Ответственность |
|---|---|
| `src/Typographer.DependencyInjection/Typographer.DependencyInjection.csproj` | пакет регистрации в контейнере |
| `src/Typographer.DependencyInjection/TypografServiceCollectionExtensions.cs` | `AddTypograf()` |
| `src/Typographer.AspNetCore/Typographer.AspNetCore.csproj` | пакет ASP.NET Core |
| `src/Typographer.AspNetCore/TypografTagHelper.cs` | тег-хелпер `<typograf>` |
| `src/Typographer.AspNetCore/HtmlTypografExtensions.cs` | типографирование в `IHtmlContent` |
| `src/Typographer.Markdig/Typographer.Markdig.csproj` | пакет Markdig |
| `src/Typographer.Markdig/TypografExtension.cs` | расширение конвейера Markdig |
| `src/Typographer.Markdig/MarkdownPipelineBuilderExtensions.cs` | `UseTypograf()` |
| `src/Typographer.Markdig/BlockTypograf.cs` | типографика текста блока с сохранением разметки |
| `src/Typographer.Cli/Typographer.Cli.csproj` | пакет утилиты `dotnet-typograf` |
| `src/Typographer.Cli/Program.cs` | точка входа, только консольная обвязка |
| `src/Typographer.Cli/CommandLine.cs` | разбор ключей и работа с потоками — всё, что тестируется |
| `tests/Typographer.Integrations.Tests/**` | тесты всех четырёх интеграций |
| `Typographer.slnx` | пять новых проектов в решении |
| `README.md`, `docs/spec.md` | раздел про пакеты |

Один тестовый проект на четыре интеграции, а не четыре: тесты запускаются одной командой,
а зависимости у них не конфликтуют. Разделять придётся тогда, когда какая-то интеграция
потребует своей целевой платформы.

---

## Task 1: Регистрация в контейнере

**Files:**
- Create: `src/Typographer.DependencyInjection/Typographer.DependencyInjection.csproj`
- Create: `src/Typographer.DependencyInjection/TypografServiceCollectionExtensions.cs`
- Create: `tests/Typographer.Integrations.Tests/Typographer.Integrations.Tests.csproj`
- Create: `tests/Typographer.Integrations.Tests/DependencyInjectionTests.cs`
- Modify: `Typographer.slnx`

**Interfaces:**
- Consumes: `HtmlTypograf(HtmlOptions?)`, `TextTypograf(TextOptions?)`, `HtmlOptions`,
  `TextOptions`, `EntityMode` из ядра.
- Produces: `IServiceCollection AddTypograf(this IServiceCollection services, HtmlOptions? html = null, TextOptions? text = null)`
  в пространстве имён `Microsoft.Extensions.DependencyInjection`. Задача 2 опирается на то,
  что `HtmlTypograf` после этого вызова разрешается из контейнера.

- [x] **Step 1: Создать ветку**

```bash
rtk git switch develop
rtk git pull --ff-only origin develop
rtk git switch -c feature/integrations
```

- [x] **Step 2: Завести проект пакета**

Создать `src/Typographer.DependencyInjection/Typographer.DependencyInjection.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net8.0;net10.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <IsTrimmable>true</IsTrimmable>
    <RootNamespace>Typographer.DependencyInjection</RootNamespace>
  </PropertyGroup>

  <PropertyGroup Label="Package">
    <PackageId>Typographer.DependencyInjection</PackageId>
    <Description>Регистрация типографа в контейнере Microsoft.Extensions.DependencyInjection.</Description>
    <PackageTags>typography;russian;typograf;dependencyinjection</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <RepositoryUrl>https://github.com/gberikov/Typographer</RepositoryUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="/" />
    <PackageReference Include="MinVer" Version="6.0.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.12" />
    <ProjectReference Include="../Typographer/Typographer.csproj" />
  </ItemGroup>

</Project>
```

Версия 10.0.12 выбрана потому, что это последний стабильный выпуск линейки 10, и он всё ещё
несёт `netstandard2.0` — целевые платформы пакета совпадают с платформами ядра.

- [x] **Step 3: Завести тестовый проект**

Создать `tests/Typographer.Integrations.Tests/Typographer.Integrations.Tests.csproj`.
Проект собирается сразу под все четыре интеграции: ссылки на остальные проекты добавляются
задачами 2–4, здесь — только на пакет этой задачи.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.2.0" />
    <!-- Абстракции есть у самого пакета; здесь нужна реализация контейнера, чтобы
         в тесте было что построить. -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.12" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Typographer.DependencyInjection/Typographer.DependencyInjection.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

- [x] **Step 4: Добавить оба проекта в решение**

В `Typographer.slnx` внутри `<Folder Name="/src/">` добавить строку:

```xml
    <Project Path="src/Typographer.DependencyInjection/Typographer.DependencyInjection.csproj" />
```

внутри `<Folder Name="/tests/">`:

```xml
    <Project Path="tests/Typographer.Integrations.Tests/Typographer.Integrations.Tests.csproj" />
```

- [x] **Step 5: Написать падающий тест**

Создать `tests/Typographer.Integrations.Tests/DependencyInjectionTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Typographer.Integrations.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddTypograf_РегистрируетОбаТипографа()
    {
        ServiceProvider provider = new ServiceCollection().AddTypograf().BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<HtmlTypograf>());
        Assert.NotNull(provider.GetRequiredService<TextTypograf>());
    }

    [Fact]
    public void AddTypograf_РегистрируетОдиночками()
    {
        ServiceProvider provider = new ServiceCollection().AddTypograf().BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<HtmlTypograf>(), provider.GetRequiredService<HtmlTypograf>());
    }

    [Fact]
    public void AddTypograf_ПередаётНастройкиТипографу()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddTypograf(new HtmlOptions { Entities = EntityMode.Named })
            .BuildServiceProvider();

        Assert.Contains("&laquo;", provider.GetRequiredService<HtmlTypograf>().Process("\"цитата\""));
    }

    // TryAdd, а не Add: своя регистрация типографа должна побеждать. Иначе AddTypograf,
    // вызванный библиотекой, молча затирал бы настройку приложения.
    [Fact]
    public void AddTypograf_НеЗатираетЧужуюРегистрацию()
    {
        var mine = new HtmlTypograf(new HtmlOptions { Entities = EntityMode.Numeric });
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(mine)
            .AddTypograf()
            .BuildServiceProvider();

        Assert.Same(mine, provider.GetRequiredService<HtmlTypograf>());
    }

    [Fact]
    public void AddTypograf_NullБросаетArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddTypograf());
    }
}
```

- [x] **Step 6: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Integrations.Tests`
Ожидается: ошибка компиляции CS1061 — метод `AddTypograf` не найден.

- [x] **Step 7: Реализовать регистрацию**

Создать `src/Typographer.DependencyInjection/TypografServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;
using Typographer;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Регистрация типографа в контейнере служб.</summary>
/// <remarks>
/// Пространство имён взято контейнерное, а не <c>Typographer.*</c>: так метод виден там,
/// где уже написано <c>using Microsoft.Extensions.DependencyInjection</c>, без второго using.
/// Обёртки над <c>IOptions</c> здесь нет намеренно: типографы и их настройки иммутабельны,
/// перенастраивать их в рантайме нечего, а <c>IOptionsMonitor</c> добавил бы зависимость
/// ради значения, которое никогда не меняется.
/// </remarks>
public static class TypografServiceCollectionExtensions
{
    /// <summary>Регистрирует <see cref="HtmlTypograf"/> и <see cref="TextTypograf"/> одиночками.</summary>
    /// <param name="services">Коллекция служб.</param>
    /// <param name="html">Настройки типографа HTML; <c>null</c> — настройки по умолчанию.</param>
    /// <param name="text">Настройки типографа текста; <c>null</c> — настройки по умолчанию.</param>
    /// <returns>Та же коллекция служб, чтобы вызовы выстраивались в цепочку.</returns>
    /// <exception cref="ArgumentNullException">Коллекция служб равна <c>null</c>.</exception>
    public static IServiceCollection AddTypograf(
        this IServiceCollection services,
        HtmlOptions? html = null,
        TextOptions? text = null)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton(new HtmlTypograf(html));
        services.TryAddSingleton(new TextTypograf(text));
        return services;
    }
}
```

- [x] **Step 8: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Integrations.Tests`
Ожидается: PASS, 5 тестов.

- [x] **Step 9: Убедиться, что собираются все платформы**

Выполнить: `rtk dotnet build -c Release`
Ожидается: успех. Проверяется, что пакет компилируется и под `netstandard2.0`, где нет
`ArgumentNullException.ThrowIfNull` — потому в коде явная проверка, а не хелпер.

- [x] **Step 10: Коммит**

```bash
rtk git add src/Typographer.DependencyInjection tests/Typographer.Integrations.Tests Typographer.slnx
rtk git commit -m "feat: пакет регистрации типографа в контейнере"
```

---

## Task 2: ASP.NET Core — тег-хелпер и IHtmlContent

**Files:**
- Create: `src/Typographer.AspNetCore/Typographer.AspNetCore.csproj`
- Create: `src/Typographer.AspNetCore/TypografTagHelper.cs`
- Create: `src/Typographer.AspNetCore/HtmlTypografExtensions.cs`
- Modify: `tests/Typographer.Integrations.Tests/Typographer.Integrations.Tests.csproj`
- Create: `tests/Typographer.Integrations.Tests/AspNetCoreTests.cs`
- Modify: `Typographer.slnx`

**Interfaces:**
- Consumes: `HtmlTypograf.Process(string)`, `HtmlTypograf.Default`, `AddTypograf()` из задачи 1.
- Produces: `Typographer.AspNetCore.TypografTagHelper` с конструктором `TypografTagHelper(HtmlTypograf)`;
  `Typographer.AspNetCore.HtmlTypografExtensions.ToHtmlContent(this HtmlTypograf typograf, string? html)`,
  возвращающий `IHtmlContent`.

- [x] **Step 1: Завести проект пакета**

Создать `src/Typographer.AspNetCore/Typographer.AspNetCore.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <RootNamespace>Typographer.AspNetCore</RootNamespace>
  </PropertyGroup>

  <PropertyGroup Label="Package">
    <PackageId>Typographer.AspNetCore</PackageId>
    <Description>Тег-хелпер и IHtmlContent для типографа в ASP.NET Core.</Description>
    <PackageTags>typography;russian;typograf;aspnetcore;taghelper</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <RepositoryUrl>https://github.com/gberikov/Typographer</RepositoryUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="/" />
    <PackageReference Include="MinVer" Version="6.0.0" PrivateAssets="all" />
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <!-- Ссылка на пакет регистрации, а не на ядро: тег-хелпер создаётся контейнером,
         и без AddTypograf() его нечем удовлетворить. Ядро приходит транзитивно. -->
    <ProjectReference Include="../Typographer.DependencyInjection/Typographer.DependencyInjection.csproj" />
  </ItemGroup>

</Project>
```

`netstandard2.0` тут нет и быть не может: `Microsoft.AspNetCore.App` — общая среда
выполнения, доступная начиная с `net8.0`.

- [x] **Step 2: Подключить проект к решению и тестам**

В `Typographer.slnx` внутри `<Folder Name="/src/">` добавить:

```xml
    <Project Path="src/Typographer.AspNetCore/Typographer.AspNetCore.csproj" />
```

В `tests/Typographer.Integrations.Tests/Typographer.Integrations.Tests.csproj` в
`ItemGroup` со ссылками на проекты добавить:

```xml
    <ProjectReference Include="../../src/Typographer.AspNetCore/Typographer.AspNetCore.csproj" />
```

и в тот же файл, в `ItemGroup` с пакетами, — ссылку на общую среду выполнения: тест
собирает `TagHelperOutput` сам, значит типы ASP.NET Core нужны ему напрямую.

```xml
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
```

- [x] **Step 3: Написать падающий тест**

Создать `tests/Typographer.Integrations.Tests/AspNetCoreTests.cs`:

```csharp
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Typographer.AspNetCore;

namespace Typographer.Integrations.Tests;

public class AspNetCoreTests
{
    private const string Nbsp = "\u00A0";

    private static TagHelperOutput MakeOutput(string childHtml)
        => new(
            "typograf",
            [],
            (useCachedResult, encoder) =>
            {
                var content = new DefaultTagHelperContent();
                content.SetHtmlContent(childHtml);
                return Task.FromResult<TagHelperContent>(content);
            });

    private static TagHelperContext MakeContext()
        => new([], new Dictionary<object, object>(), "тест");

    [Fact]
    public async Task ProcessAsync_ТипографируетСодержимое()
    {
        TagHelperOutput output = MakeOutput("<p>Он - человек</p>");

        await new TypografTagHelper(HtmlTypograf.Default).ProcessAsync(MakeContext(), output);

        string result = output.Content.GetContent();
        Assert.Contains("—", result);
        Assert.DoesNotContain(" - ", result);
    }

    // Сам элемент <typograf> — инструкция шаблонизатору, а не разметка страницы,
    // и в выводе его быть не должно.
    [Fact]
    public async Task ProcessAsync_УбираетСобственныйТег()
    {
        TagHelperOutput output = MakeOutput("текст");

        await new TypografTagHelper(HtmlTypograf.Default).ProcessAsync(MakeContext(), output);

        Assert.Null(output.TagName);
    }

    [Fact]
    public async Task ProcessAsync_РазметкуВнутриНеТрогает()
    {
        TagHelperOutput output = MakeOutput("<a href=\"http://a.example/x--y\">ссылка</a>");

        await new TypografTagHelper(HtmlTypograf.Default).ProcessAsync(MakeContext(), output);

        Assert.Contains("http://a.example/x--y", output.Content.GetContent());
    }

    [Fact]
    public void ToHtmlContent_ВозвращаетГотовуюРазметку()
    {
        IHtmlContent content = HtmlTypograf.Default.ToHtmlContent("Он - человек");

        Assert.Contains(Nbsp + "—", content.ToString());
    }

    [Fact]
    public void ToHtmlContent_NullДаётПустуюРазметку()
    {
        Assert.Equal(string.Empty, HtmlTypograf.Default.ToHtmlContent(null).ToString());
    }
}
```

`HtmlString.ToString()` возвращает саму разметку, поэтому отдельная запись в
`TextWriter` в тесте не нужна.

- [x] **Step 4: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Integrations.Tests`
Ожидается: ошибка компиляции — типы `TypografTagHelper` и метод `ToHtmlContent` не найдены.

- [x] **Step 5: Реализовать тег-хелпер**

Создать `src/Typographer.AspNetCore/TypografTagHelper.cs`:

```csharp
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Typographer.AspNetCore;

/// <summary>Тег-хелпер <c>&lt;typograf&gt;</c>: типографирует содержимое элемента.</summary>
/// <remarks>
/// Содержимое к этому моменту уже отрендерено и закодировано шаблонизатором, то есть
/// является готовым HTML, — потому обрабатывается <see cref="HtmlTypograf"/>, а не
/// текстовым типографом, и возвращается как разметка, а не как текст.
/// Требует регистрации типографа в контейнере: <c>services.AddTypograf()</c>.
/// </remarks>
[HtmlTargetElement("typograf")]
public sealed class TypografTagHelper : TagHelper
{
    private readonly HtmlTypograf _typograf;

    /// <summary>Создаёт тег-хелпер с типографом из контейнера.</summary>
    /// <param name="typograf">Типограф HTML.</param>
    /// <exception cref="ArgumentNullException">Типограф равен <c>null</c>.</exception>
    public TypografTagHelper(HtmlTypograf typograf)
    {
        ArgumentNullException.ThrowIfNull(typograf);
        _typograf = typograf;
    }

    /// <summary>Заменяет содержимое элемента типографированным и убирает сам элемент.</summary>
    /// <param name="context">Контекст тег-хелпера.</param>
    /// <param name="output">Вывод тег-хелпера.</param>
    /// <exception cref="ArgumentNullException">Вывод равен <c>null</c>.</exception>
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        TagHelperContent content = await output.GetChildContentAsync().ConfigureAwait(false);

        output.TagName = null;
        output.Content.SetHtmlContent(_typograf.Process(content.GetContent()));
    }
}
```

- [x] **Step 6: Реализовать переходник к IHtmlContent**

Создать `src/Typographer.AspNetCore/HtmlTypografExtensions.cs`:

```csharp
using Microsoft.AspNetCore.Html;

namespace Typographer.AspNetCore;

/// <summary>Типографирование для представлений Razor.</summary>
public static class HtmlTypografExtensions
{
    /// <summary>Типографирует фрагмент и возвращает его как готовую разметку.</summary>
    /// <param name="typograf">Типограф HTML.</param>
    /// <param name="html">Исходный фрагмент; <c>null</c> даёт пустую разметку.</param>
    /// <returns>Разметка, готовая к выводу в представлении.</returns>
    /// <exception cref="ArgumentNullException">Типограф равен <c>null</c>.</exception>
    /// <remarks>
    /// Возвращается <see cref="IHtmlContent"/>, а не строка: результат уже содержит
    /// разметку, и повторное кодирование шаблонизатором превратило бы её в текст.
    /// В представлении:
    /// <code>
    /// @inject HtmlTypograf Typograf
    /// @Typograf.ToHtmlContent(Model.Text)
    /// </code>
    /// </remarks>
    public static IHtmlContent ToHtmlContent(this HtmlTypograf typograf, string? html)
    {
        ArgumentNullException.ThrowIfNull(typograf);

        return html is null ? HtmlString.Empty : new HtmlString(typograf.Process(html));
    }
}
```

- [x] **Step 7: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Integrations.Tests`
Ожидается: PASS, 10 тестов (5 из задачи 1 и 5 новых).

- [x] **Step 8: Коммит**

```bash
rtk git add src/Typographer.AspNetCore tests/Typographer.Integrations.Tests Typographer.slnx
rtk git commit -m "feat: тег-хелпер и IHtmlContent для ASP.NET Core"
```

---

## Task 3: Расширение Markdig

Markdown нельзя типографировать после рендеринга: `HtmlRenderer` кодирует прямую кавычку
в `&quot;`, а типограф сущности разметки намеренно не декодирует (спецификация, 5.1) —
кавычки-ёлочки не появились бы вовсе. Поэтому правки вносятся в дерево документа, до
рендеринга, и работают с любым рендерером, включая нормализующий.

Внутри абзаца текст разорван разметкой: `"**слово**"` — это три отдельных `LiteralInline`.
Обработать их поодиночке нельзя: направление кавычки определяется предыдущим символом
(`QuoteRules.IsOpeningContext`), а в начале каждого куска предыдущего символа нет, и
закрывающая кавычка стала бы второй открывающей. Куски собираются в одну строку через
разделитель U+FFFC (OBJECT REPLACEMENT CHARACTER — «здесь был объект»), типографируются
разом и раскладываются обратно. Символ выбран за то, что он не пробельный, не буква и не
скобка: для правила кавычек он — обычное содержимое, ровно то, чем и была вырезанная
разметка.

**Files:**
- Create: `src/Typographer.Markdig/Typographer.Markdig.csproj`
- Create: `src/Typographer.Markdig/BlockTypograf.cs`
- Create: `src/Typographer.Markdig/TypografExtension.cs`
- Create: `src/Typographer.Markdig/MarkdownPipelineBuilderExtensions.cs`
- Modify: `tests/Typographer.Integrations.Tests/Typographer.Integrations.Tests.csproj`
- Create: `tests/Typographer.Integrations.Tests/MarkdigTests.cs`
- Modify: `Typographer.slnx`

**Interfaces:**
- Consumes: `TextTypograf.Process(string)`, `TextTypograf.Default` из ядра.
- Produces: `Typographer.Markdig.TypografExtension : IMarkdownExtension` с конструктором
  `TypografExtension(TextTypograf? typograf = null)`;
  `Typographer.Markdig.MarkdownPipelineBuilderExtensions.UseTypograf(this MarkdownPipelineBuilder pipeline, TextTypograf? typograf = null)`,
  возвращающий тот же `MarkdownPipelineBuilder`.

- [x] **Step 1: Завести проект пакета**

Создать `src/Typographer.Markdig/Typographer.Markdig.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net8.0;net10.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <RootNamespace>Typographer.Markdig</RootNamespace>
  </PropertyGroup>

  <PropertyGroup Label="Package">
    <PackageId>Typographer.Markdig</PackageId>
    <Description>Типографика Markdown: расширение конвейера Markdig.</Description>
    <PackageTags>typography;russian;typograf;markdown;markdig</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <RepositoryUrl>https://github.com/gberikov/Typographer</RepositoryUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="/" />
    <PackageReference Include="MinVer" Version="6.0.0" PrivateAssets="all" />
    <PackageReference Include="Markdig" Version="1.3.2" />
    <ProjectReference Include="../Typographer/Typographer.csproj" />
  </ItemGroup>

</Project>
```

`Markdig` 1.3.2 несёт `netstandard2.0`, `net8.0` и `net10.0` — платформы совпадают с ядром.

- [x] **Step 2: Подключить проект к решению и тестам**

В `Typographer.slnx` внутри `<Folder Name="/src/">`:

```xml
    <Project Path="src/Typographer.Markdig/Typographer.Markdig.csproj" />
```

В `tests/Typographer.Integrations.Tests/Typographer.Integrations.Tests.csproj` к ссылкам
на проекты:

```xml
    <ProjectReference Include="../../src/Typographer.Markdig/Typographer.Markdig.csproj" />
```

- [x] **Step 3: Написать падающий тест**

Создать `tests/Typographer.Integrations.Tests/MarkdigTests.cs`:

```csharp
using Markdig;
using Typographer.Markdig;

namespace Typographer.Integrations.Tests;

public class MarkdigTests
{
    private const string Nbsp = "\u00A0";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseTypograf()
        .Build();

    [Fact]
    public void UseTypograf_СтавитТиреИНеразрывныйПробел()
    {
        Assert.Equal(
            $"<p>Он{Nbsp}— человек</p>\n",
            Markdown.ToHtml("Он - человек", Pipeline));
    }

    // Ради этого случая куски абзаца и собираются в одну строку: поодиночке вторая
    // кавычка не знает, что слева от неё что-то было, и открылась бы второй раз.
    [Fact]
    public void UseTypograf_КавычкиВокругРазметкиСмотрятВРазныеСтороны()
    {
        Assert.Equal(
            "<p>«<strong>слово</strong>»</p>\n",
            Markdown.ToHtml("\"**слово**\"", Pipeline));
    }

    [Fact]
    public void UseTypograf_КодНеТрогает()
    {
        Assert.Equal(
            "<p><code>a - b</code></p>\n",
            Markdown.ToHtml("`a - b`", Pipeline));
    }

    [Fact]
    public void UseTypograf_БлокКодаНеТрогает()
    {
        Assert.Contains("a - b", Markdown.ToHtml("```\na - b\n```", Pipeline));
    }

    [Fact]
    public void UseTypograf_АдресСсылкиНеТрогает()
    {
        string html = Markdown.ToHtml("[текст - тут](http://a.example/x--y)", Pipeline);

        Assert.Contains("http://a.example/x--y", html);
        Assert.Contains("текст" + Nbsp + "—", html);
    }

    [Fact]
    public void БезРасширенияНичегоНеМеняется()
    {
        Assert.Equal("<p>Он - человек</p>\n", Markdown.ToHtml("Он - человек"));
    }
}
```

- [x] **Step 4: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Integrations.Tests`
Ожидается: ошибка компиляции — метод `UseTypograf` не найден.

- [x] **Step 5: Реализовать типографику блока**

Создать `src/Typographer.Markdig/BlockTypograf.cs`.

Ловушка пространств имён: внутри `namespace Typographer.Markdig` имя `Markdig` разрешается
в это же пространство, поэтому обращаться к типам библиотеки через `Markdig.Syntax.X`
нельзя. Директивы `using` стоят ДО объявления пространства имён — они разрешаются
глобально, и короткие имена типов работают.

```csharp
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Typographer.Markdig;

/// <summary>Типографика текста одного блока с сохранением разметки внутри него.</summary>
internal static class BlockTypograf
{
    /// <summary>Разделитель, которым в собранной строке представлена вырезанная разметка.</summary>
    private const char Object = '\uFFFC';

    private static readonly string Separator = Object.ToString();

    /// <summary>Типографирует текстовые куски блока целиком, как один связный текст.</summary>
    /// <param name="root">Корень строчных элементов блока.</param>
    /// <param name="typograf">Типограф обычного текста.</param>
    public static void Apply(ContainerInline root, TextTypograf typograf)
    {
        List<LiteralInline> literals = [];
        foreach (LiteralInline literal in root.Descendants<LiteralInline>())
        {
            literals.Add(literal);
        }

        if (literals.Count == 0)
        {
            return;
        }

        var parts = new string[literals.Count];
        for (int i = 0; i < literals.Count; i++)
        {
            parts[i] = literals[i].Content.ToString();

            // Чужой U+FFFC во входе сделал бы раскладку обратно неоднозначной.
            // Такой блок остаётся нетронутым: испортить текст хуже, чем не улучшить.
            if (parts[i].IndexOf(Object) >= 0)
            {
                return;
            }
        }

        string[] result = typograf.Process(string.Join(Separator, parts)).Split(Object);

        // Правило могло съесть разделитель вместе с соседним пробелом или размножить его.
        // Раскладывать нечего — блок остаётся как был.
        if (result.Length != literals.Count)
        {
            return;
        }

        for (int i = 0; i < literals.Count; i++)
        {
            if (!string.Equals(result[i], parts[i], StringComparison.Ordinal))
            {
                literals[i].Content = new StringSlice(result[i]);
            }
        }
    }
}
```

- [x] **Step 6: Реализовать расширение**

Создать `src/Typographer.Markdig/TypografExtension.cs`:

```csharp
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;

namespace Typographer.Markdig;

/// <summary>Расширение Markdig: типографика применяется к дереву документа после разбора.</summary>
/// <remarks>
/// После разбора, а не после рендеринга: рендерер кодирует прямую кавычку в
/// <c>&amp;quot;</c>, а типограф сущности разметки не декодирует, и кавычки-ёлочки
/// в готовом HTML уже не появились бы. Правка дерева работает с любым рендерером.
/// Разметка внутри абзаца не трогается: правкам подлежат только текстовые узлы, а
/// содержимое кода, адреса ссылок и встроенный HTML отдельными узлами и остаются.
/// </remarks>
public sealed class TypografExtension : IMarkdownExtension
{
    private readonly TextTypograf _typograf;

    /// <summary>Создаёт расширение с указанным типографом.</summary>
    /// <param name="typograf">Типограф обычного текста; <c>null</c> — с настройками по умолчанию.</param>
    public TypografExtension(TextTypograf? typograf = null) => _typograf = typograf ?? TextTypograf.Default;

    /// <summary>Подписывается на завершение разбора документа.</summary>
    /// <param name="pipeline">Строитель конвейера.</param>
    /// <exception cref="ArgumentNullException">Строитель равен <c>null</c>.</exception>
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (pipeline is null)
        {
            throw new ArgumentNullException(nameof(pipeline));
        }

        pipeline.DocumentProcessed += Apply;
    }

    /// <summary>Ничего не делает: правки уже в дереве, и рендерер увидит их сам.</summary>
    /// <param name="pipeline">Готовый конвейер.</param>
    /// <param name="renderer">Рендерер.</param>
    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }

    private void Apply(MarkdownDocument document)
    {
        foreach (LeafBlock block in document.Descendants<LeafBlock>())
        {
            // Inline есть у абзацев и заголовков; у блока кода и блока HTML его нет,
            // и именно поэтому они не типографируются — отдельной проверки не нужно.
            if (block.Inline is not null)
            {
                BlockTypograf.Apply(block.Inline, _typograf);
            }
        }
    }
}
```

- [x] **Step 7: Реализовать точку подключения**

Создать `src/Typographer.Markdig/MarkdownPipelineBuilderExtensions.cs`:

```csharp
using Markdig;

namespace Typographer.Markdig;

/// <summary>Подключение типографа к конвейеру Markdig.</summary>
public static class MarkdownPipelineBuilderExtensions
{
    /// <summary>Включает типографику текста в конвейере.</summary>
    /// <param name="pipeline">Строитель конвейера.</param>
    /// <param name="typograf">Типограф обычного текста; <c>null</c> — с настройками по умолчанию.</param>
    /// <returns>Тот же строитель, чтобы вызовы выстраивались в цепочку.</returns>
    /// <exception cref="ArgumentNullException">Строитель равен <c>null</c>.</exception>
    /// <remarks>
    /// Повторный вызов ничего не добавляет: расширение регистрируется однократно.
    /// </remarks>
    public static MarkdownPipelineBuilder UseTypograf(
        this MarkdownPipelineBuilder pipeline,
        TextTypograf? typograf = null)
    {
        if (pipeline is null)
        {
            throw new ArgumentNullException(nameof(pipeline));
        }

        pipeline.Extensions.AddIfNotAlready(new TypografExtension(typograf));
        return pipeline;
    }
}
```

- [x] **Step 8: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Integrations.Tests`
Ожидается: PASS, 16 тестов.

Если тест `UseTypograf_СтавитТиреИНеразрывныйПробел` падает на разнице переводов строк
(`\n` против `\r\n`), это не наша беда: Markdig всегда пишет `\n`. Смотреть на фактический
вывод в сообщении xunit и править ожидание только если различие в самой типографике.

- [x] **Step 9: Коммит**

```bash
rtk git add src/Typographer.Markdig tests/Typographer.Integrations.Tests Typographer.slnx
rtk git commit -m "feat: расширение Markdig для типографики Markdown"
```

---

## Task 4: Утилита dotnet-typograf

**Files:**
- Create: `src/Typographer.Cli/Typographer.Cli.csproj`
- Create: `src/Typographer.Cli/CommandLine.cs`
- Create: `src/Typographer.Cli/Program.cs`
- Modify: `tests/Typographer.Integrations.Tests/Typographer.Integrations.Tests.csproj`
- Create: `tests/Typographer.Integrations.Tests/CliTests.cs`
- Modify: `Typographer.slnx`

**Interfaces:**
- Consumes: `HtmlTypograf`, `TextTypograf`, `HtmlOptions`, `TextOptions`, `EntityMode`,
  `RuleSet`, `RuleId.TryParse(string, out RuleId)`, `RuleId.Registry` недоступен снаружи —
  список правил берётся перечислением `RuleSet.All`.
- Produces: `internal static int CommandLine.Run(string[] args, TextReader input, TextWriter output, TextWriter error)`.
  Ничего наружу пакет не отдаёт: это утилита, а не библиотека.

- [x] **Step 1: Завести проект утилиты**

Создать `src/Typographer.Cli/Typographer.Cli.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <AssemblyName>dotnet-typograf</AssemblyName>
    <RootNamespace>Typographer.Cli</RootNamespace>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>dotnet-typograf</ToolCommandName>
  </PropertyGroup>

  <PropertyGroup Label="Package">
    <PackageId>dotnet-typograf</PackageId>
    <Description>Типограф для русского языка в командной строке.</Description>
    <PackageTags>typography;russian;typograf;cli;dotnet-tool</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <RepositoryUrl>https://github.com/gberikov/Typographer</RepositoryUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
  </PropertyGroup>

  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="/" />
    <PackageReference Include="MinVer" Version="6.0.0" PrivateAssets="all" />
    <ProjectReference Include="../Typographer/Typographer.csproj" />
    <InternalsVisibleTo Include="Typographer.Integrations.Tests" />
  </ItemGroup>

</Project>
```

`IncludeSymbols` у утилиты нет намеренно: `PackAsTool` и `snupkg` вместе не работают —
пакет инструмента и так несёт приложение целиком.

Разбор ключей написан вручную: спецификация (раздел 10) обещает утилите единственную
зависимость — ядро, а `System.CommandLine` был бы второй ради двух десятков строк.

- [x] **Step 2: Подключить проект к решению и тестам**

В `Typographer.slnx` внутри `<Folder Name="/src/">`:

```xml
    <Project Path="src/Typographer.Cli/Typographer.Cli.csproj" />
```

В `tests/Typographer.Integrations.Tests/Typographer.Integrations.Tests.csproj` к ссылкам
на проекты:

```xml
    <ProjectReference Include="../../src/Typographer.Cli/Typographer.Cli.csproj" />
```

- [x] **Step 3: Написать падающий тест**

Создать `tests/Typographer.Integrations.Tests/CliTests.cs`:

```csharp
using Typographer.Cli;

namespace Typographer.Integrations.Tests;

public class CliTests
{
    private const string Nbsp = "\u00A0";

    private static (int Code, string Output, string Error) Run(string[] args, string input = "")
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int code = CommandLine.Run(args, new StringReader(input), output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void Run_БезФайловЧитаетСтандартныйВвод()
    {
        (int code, string output, _) = Run([], "Он - человек");

        Assert.Equal(0, code);
        Assert.Equal($"Он{Nbsp}— человек", output);
    }

    [Fact]
    public void Run_ПоУмолчаниюРежимHtml()
    {
        (_, string output, _) = Run([], "<code>a - b</code>");

        Assert.Contains("a - b", output);
    }

    [Fact]
    public void Run_КлючТекстОтключаетЗащитуРазметки()
    {
        (_, string output, _) = Run(["--text"], "<code>a - b</code>");

        Assert.Contains("—", output);
    }

    [Fact]
    public void Run_КлючEntitiesМеняетВидСущностей()
    {
        (int code, string output, _) = Run(["--entities", "named"], "\"цитата\"");

        Assert.Equal(0, code);
        Assert.Contains("&laquo;", output);
    }

    [Fact]
    public void Run_ПресетNoneНичегоНеМеняет()
    {
        (_, string output, _) = Run(["--rules", "none"], "Он - человек");

        Assert.Equal("Он - человек", output);
    }

    [Fact]
    public void Run_КлючDisableВыключаетПравило()
    {
        (int code, string output, _) = Run(["--disable", "ru/dash/main"], "Он - человек");

        Assert.Equal(0, code);
        Assert.DoesNotContain("—", output);
    }

    [Fact]
    public void Run_НеизвестноеПравилоЭтоОшибка()
    {
        (int code, _, string error) = Run(["--disable", "ru/нет/такого"], "текст");

        Assert.Equal(1, code);
        Assert.Contains("ru/нет/такого", error);
    }

    [Fact]
    public void Run_НеизвестныйКлючЭтоОшибка()
    {
        (int code, _, string error) = Run(["--чего-нет"], "текст");

        Assert.Equal(1, code);
        Assert.NotEqual(string.Empty, error);
    }

    [Fact]
    public void Run_КлючБезЗначенияЭтоОшибка()
    {
        (int code, _, string error) = Run(["--entities"], "текст");

        Assert.Equal(1, code);
        Assert.NotEqual(string.Empty, error);
    }

    [Fact]
    public void Run_СправкаПечатаетсяВВыводИДаётНоль()
    {
        (int code, string output, _) = Run(["--help"]);

        Assert.Equal(0, code);
        Assert.Contains("dotnet-typograf", output);
    }

    [Fact]
    public void Run_ВерсияПечатаетсяВВывод()
    {
        (int code, string output, _) = Run(["--version"]);

        Assert.Equal(0, code);
        Assert.NotEqual(string.Empty, output.Trim());
    }

    [Fact]
    public void Run_СписокПравилСодержитИменаJsTypograf()
    {
        (int code, string output, _) = Run(["--list-rules"]);

        Assert.Equal(0, code);
        Assert.Contains("ru/dash/main", output);
    }

    [Fact]
    public void Run_ФайлЧитаетсяИПишетсяВВывод()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "Он - человек");
        try
        {
            (int code, string output, _) = Run([path]);

            Assert.Equal(0, code);
            Assert.Contains("—", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_КлючInPlaceПерезаписываетФайл()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "Он - человек");
        try
        {
            (int code, string output, _) = Run(["--in-place", path]);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, output);
            Assert.Contains("—", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_ОтсутствующийФайлЭтоОшибка()
    {
        (int code, _, string error) = Run([Path.Combine(Path.GetTempPath(), "нет-такого-файла.txt")]);

        Assert.Equal(1, code);
        Assert.NotEqual(string.Empty, error);
    }
}
```

- [x] **Step 4: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Integrations.Tests`
Ожидается: ошибка компиляции — тип `CommandLine` не найден.

- [x] **Step 5: Реализовать разбор ключей и работу**

Создать `src/Typographer.Cli/CommandLine.cs`:

```csharp
using System.Globalization;
using System.Reflection;
using System.Text;
using Typographer.Rules;

namespace Typographer.Cli;

/// <summary>Разбор ключей и работа с потоками. Всё, что можно проверить тестом.</summary>
internal static class CommandLine
{
    private const string Help = """
        Типограф для русского языка.

        Использование: dotnet-typograf [ключи] [файл...]

        Без файлов читает стандартный ввод и пишет в стандартный вывод.

          -t, --text              обычный текст вместо HTML
          -e, --entities <режим>  symbols | named | numeric | mixed (по умолчанию symbols)
          -r, --rules <пресет>    default | lebedev | typograf | gost | all | minimal | none
              --enable <правило>  включить правило по имени, можно повторять
              --disable <правило> выключить правило по имени, можно повторять
              --br                перевод строки заменять на <br />
              --p                 абзацы оборачивать в <p>
              --nobr <число>      объединять до N слов в <nobr>
          -i, --in-place          писать результат обратно в файлы
          -l, --list-rules        напечатать имена всех правил и выйти
          -h, --help              эта справка
          -V, --version           версия

        Имена правил совпадают с именами JS-typograf: ru/dash/main, common/nbsp/afterShortWord.
        """;

    /// <summary>Выполняет команду.</summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <param name="input">Стандартный ввод.</param>
    /// <param name="output">Стандартный вывод.</param>
    /// <param name="error">Поток ошибок.</param>
    /// <returns>Ноль при успехе, единица при любой ошибке.</returns>
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        bool text = false;
        bool inPlace = false;
        EntityMode entities = EntityMode.Symbols;
        RuleSet rules = RuleSet.Default;
        bool useBr = false;
        bool useP = false;
        int maxNobr = 0;
        var files = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h" or "--help":
                    output.WriteLine(Help);
                    return 0;

                case "-V" or "--version":
                    output.WriteLine(Version());
                    return 0;

                case "-l" or "--list-rules":
                    foreach (RuleId rule in RuleSet.All)
                    {
                        output.WriteLine(rule.Name);
                    }

                    return 0;

                case "-t" or "--text":
                    text = true;
                    break;

                case "-i" or "--in-place":
                    inPlace = true;
                    break;

                case "--br":
                    useBr = true;
                    break;

                case "--p":
                    useP = true;
                    break;

                case "-e" or "--entities":
                    if (!TryTakeValue(args, ref i, out string? mode))
                    {
                        return Fail(error, $"Ключ {arg} требует значения.");
                    }

                    if (!TryParseEntities(mode, out entities))
                    {
                        return Fail(error, $"Неизвестный режим сущностей: {mode}.");
                    }

                    break;

                case "-r" or "--rules":
                    if (!TryTakeValue(args, ref i, out string? preset))
                    {
                        return Fail(error, $"Ключ {arg} требует значения.");
                    }

                    if (!TryParsePreset(preset, out rules))
                    {
                        return Fail(error, $"Неизвестный пресет: {preset}.");
                    }

                    break;

                case "--enable" or "--disable":
                    if (!TryTakeValue(args, ref i, out string? name))
                    {
                        return Fail(error, $"Ключ {arg} требует значения.");
                    }

                    if (!RuleId.TryParse(name, out RuleId rule))
                    {
                        return Fail(error, $"Неизвестное правило: {name}.");
                    }

                    rules = arg == "--enable" ? rules.With(rule) : rules.Without(rule);
                    break;

                case "--nobr":
                    if (!TryTakeValue(args, ref i, out string? count))
                    {
                        return Fail(error, $"Ключ {arg} требует значения.");
                    }

                    if (!int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxNobr)
                        || maxNobr < 0)
                    {
                        return Fail(error, $"Ключ --nobr требует неотрицательного числа, получено: {count}.");
                    }

                    break;

                default:
                    if (arg.StartsWith('-'))
                    {
                        return Fail(error, $"Неизвестный ключ: {arg}. Справка: dotnet-typograf --help");
                    }

                    files.Add(arg);
                    break;
            }
        }

        if (inPlace && files.Count == 0)
        {
            return Fail(error, "Ключ --in-place требует хотя бы одного файла.");
        }

        Func<string, string> process = text
            ? new TextTypograf(new TextOptions { Rules = rules }).Process
            : new HtmlTypograf(new HtmlOptions
            {
                Rules = rules,
                Entities = entities,
                UseBr = useBr,
                UseP = useP,
                MaxNobr = maxNobr,
            }).Process;

        if (files.Count == 0)
        {
            output.Write(process(input.ReadToEnd()));
            return 0;
        }

        foreach (string file in files)
        {
            string source;
            try
            {
                source = File.ReadAllText(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Fail(error, $"Не удалось прочитать {file}: {e.Message}");
            }

            string result = process(source);

            if (!inPlace)
            {
                output.Write(result);
                continue;
            }

            try
            {
                // Без метки порядка байт: она делает файл нечитаемым для половины утилит,
                // а UTF-8 в ней не нуждается.
                File.WriteAllText(file, result, new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Fail(error, $"Не удалось записать {file}: {e.Message}");
            }
        }

        return 0;
    }

    private static bool TryTakeValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryParseEntities(string value, out EntityMode mode)
    {
        switch (value)
        {
            case "symbols": mode = EntityMode.Symbols; return true;
            case "named": mode = EntityMode.Named; return true;
            case "numeric": mode = EntityMode.Numeric; return true;
            case "mixed": mode = EntityMode.Mixed; return true;
            default: mode = EntityMode.Symbols; return false;
        }
    }

    private static bool TryParsePreset(string value, out RuleSet rules)
    {
        switch (value)
        {
            case "default": rules = RuleSet.Default; return true;
            case "lebedev": rules = RuleSet.Lebedev; return true;
            case "typograf": rules = RuleSet.Typograf; return true;
            case "gost": rules = RuleSet.Gost; return true;
            case "all": rules = RuleSet.All; return true;
            case "minimal": rules = RuleSet.Minimal; return true;
            case "none": rules = RuleSet.None; return true;
            default: rules = RuleSet.Default; return false;
        }
    }

    private static string Version()
        => typeof(CommandLine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
               ?.InformationalVersion
           ?? "0.0.0";

    private static int Fail(TextWriter error, string message)
    {
        error.WriteLine(message);
        return 1;
    }
}
```

- [x] **Step 6: Реализовать точку входа**

Создать `src/Typographer.Cli/Program.cs`. В нём только то, чего нельзя проверить тестом:

```csharp
using System.Text;
using Typographer.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        // UTF-8 задаётся потокам явно, а не через Console.InputEncoding: перенаправленный
        // ввод-вывод в Windows иначе кодируется кодовой страницей консоли, и кавычки-ёлочки
        // в файле превращаются в угловые скобки. Кодовая страница самой консоли меняется
        // отдельно — иначе те же байты стали бы кашей уже на экране.
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        if (!Console.IsOutputRedirected)
        {
            try
            {
                Console.OutputEncoding = utf8;
            }
            catch (IOException)
            {
                // Консоли нет вовсе — работаем как есть.
            }
        }

        using var input = new StreamReader(Console.OpenStandardInput(), utf8);
        using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
        using var error = new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true };

        return CommandLine.Run(args, input, output, error);
    }
}
```

- [x] **Step 7: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Integrations.Tests`
Ожидается: PASS, 31 тест.

- [x] **Step 8: Проверить утилиту вживую**

Выполнить:

```bash
echo 'Он - человек и "цитата"' | rtk dotnet run --project src/Typographer.Cli --
```

Ожидается: `Он — человек и «цитата»` (перед тире неразрывный пробел).

- [x] **Step 9: Коммит**

```bash
rtk git add src/Typographer.Cli tests/Typographer.Integrations.Tests Typographer.slnx
rtk git commit -m "feat: утилита командной строки dotnet-typograf"
```

---

## Task 5: Упаковка и документация

**Files:**
- Modify: `README.md`
- Modify: `docs/spec.md:426-455`
- Modify: `docs/superpowers/plans/2026-09-08-typographer-core.md:3022-3029`

**Interfaces:**
- Consumes: всё, что сделано задачами 1–4.
- Produces: ничего для кода; раздел «Пакеты» в README и отметку о выполнении в очереди планов.

- [x] **Step 1: Проверить, что пакеты собираются**

Выполнить: `rtk dotnet pack -c Release -o artifacts/packages`

Ожидается: пять файлов `.nupkg` — `Typographer`, `Typographer.DependencyInjection`,
`Typographer.AspNetCore`, `Typographer.Markdig`, `dotnet-typograf`. Проверить список:

```bash
rtk ls artifacts/packages
```

Каталог `artifacts/` в репозиторий не попадает — убедиться, что он в `.gitignore`, и
добавить строку `artifacts/`, если её там нет.

- [x] **Step 2: Проверить, что утилита ставится**

Выполнить:

```bash
rtk dotnet tool install --global --add-source artifacts/packages dotnet-typograf --prerelease
rtk dotnet-typograf --version
rtk dotnet tool uninstall --global dotnet-typograf
```

Ожидается: установка проходит, версия печатается, удаление проходит. Это единственная
проверка того, что `PackAsTool` и `ToolCommandName` заданы верно: тестом её не сделать.

- [x] **Step 3: Описать пакеты в README**

В `README.md` после раздела «Целевые платформы» добавить раздел:

````markdown
## Пакеты

| Пакет | Зачем | Зависимости |
|---|---|---|
| `Typographer` | ядро: HTML и обычный текст, правила, пресеты | нет на `net8.0` и `net10.0`; `System.Memory` на `netstandard2.0` |
| `Typographer.DependencyInjection` | `AddTypograf()` | `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `Typographer.AspNetCore` | тег-хелпер `<typograf>` и `IHtmlContent` | ASP.NET Core |
| `Typographer.Markdig` | типографика Markdown | `Markdig` |
| `dotnet-typograf` | утилита командной строки | ядро |

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

Правки вносятся в дерево документа после разбора, поэтому код, адреса ссылок и встроенный
HTML остаются нетронутыми, а кавычки вокруг разметки — `"**слово**"` — смотрят в разные
стороны.

### Командная строка

```bash
dotnet tool install --global dotnet-typograf
echo 'Он - человек' | dotnet-typograf
dotnet-typograf --entities named --in-place статья.html
dotnet-typograf --help
```
````

- [x] **Step 4: Отметить сделанное в спецификации**

В `docs/spec.md`, раздел 10, привести структуру репозитория в соответствие с фактом:
`src/Typographer.Cli/` появился, `tests/Typographer.Integrations.Tests/` — тоже. Добавить
в дерево строку после `tests/Typographer.Oracle/`:

```
tests/Typographer.Integrations.Tests/ тесты пакетов интеграций
```

и после `src/Typographer.Markdig/` строку `src/Typographer.Cli/` оставить как есть — она
там уже описана.

В таблицу пакетов того же раздела добавить колонку с целевыми платформами:

| Пакет | Целевые платформы |
|---|---|
| `Typographer` | `netstandard2.0;net8.0;net10.0` |
| `Typographer.DependencyInjection` | `netstandard2.0;net8.0;net10.0` |
| `Typographer.AspNetCore` | `net8.0;net10.0` |
| `Typographer.Markdig` | `netstandard2.0;net8.0;net10.0` |
| `dotnet-typograf` | `net10.0` |

- [x] **Step 5: Отметить план в очереди**

В `docs/superpowers/plans/2026-09-08-typographer-core.md`, таблица «Очередь планов»,
строку про план 4 привести к виду:

```markdown
| 4. Интеграции ✅ | `Typographer.DependencyInjection`, `Typographer.AspNetCore`, `Typographer.Markdig`, `dotnet-typograf`. План — `2026-09-10-integrations.md` | 2 |
```

- [x] **Step 6: Прогнать всё**

Выполнить: `rtk dotnet build -c Release` и `dotnet test -c Release`
Ожидается: сборка без предупреждений, все тесты зелёные — и старые, и новые.

- [x] **Step 7: Коммит**

```bash
rtk git add README.md docs .gitignore
rtk git commit -m "docs: пакеты интеграций в README и спецификации"
```

- [x] **Step 8: Завершение работы**

Открыть PR в `develop`, дождаться зелёного `build` и слить через PR — прямой push в
`develop` защита ветки не пропустит.

---

## Self-Review

**Покрытие спецификации:**

| Требование спецификации | Задача |
|---|---|
| 10, `Typographer.DependencyInjection` — `AddTypograf()` | 1 |
| 10, `Typographer.AspNetCore` — TagHelper и `IHtmlContent` | 2 |
| 10, `Typographer.Markdig` — типографика Markdown | 3 |
| 10, `dotnet-typograf` — CLI как dotnet tool | 4 |
| 10, граница пакета — внешняя зависимость | 1–4, ядро не тронуто |
| 10, структура репозитория | 5 |
| 11, XML-комментарии по-русски на каждом публичном члене | 1–4, `GenerateDocumentationFile` |
| 12, MinVer, теги без префикса | 1–4, `PackageReference MinVer` |

**Чего в плане нет и почему:**

| Не делаем | Почему |
|---|---|
| `IOptions<HtmlOptions>` в пакете контейнера | настройки и типографы иммутабельны; менять их в рантайме нечего, а `IOptionsMonitor` тянул бы ещё один пакет ради константы |
| Расширение `IHtmlHelper.Typograf()` | `@Html.Typograf(...)` красивее, но проверяется только через заглушку всего `IHtmlHelper`; тег-хелпер и `ToHtmlContent` закрывают оба случая |
| `System.CommandLine` в CLI | спецификация обещает утилите единственную зависимость — ядро; ручной разбор двух десятков ключей короче, чем описание команд для библиотеки |
| Публикация пакетов в NuGet | план 5: публикация по тегу вместе с сайтом документации |
| Кэш готовых `TextTypograf` в расширении Markdig | типограф иммутабелен и создаётся один раз на конвейер |
| Типографика Markdown после рендеринга | рендерер кодирует `"` в `&quot;`, а типограф сущности разметки не декодирует — кавычки не появились бы вовсе |
| Отдельный тестовый проект на каждый пакет | зависимости не конфликтуют, платформа у всех одна; делить придётся, когда это перестанет быть правдой |
