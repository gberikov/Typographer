# План 2a: инфраструктура под полный набор правил

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Подготовить конвейер к 93 новым правилам: перевести фазы с посегментной работы на документную (состояние фазы `Bind` начинает жить сквозь разметку), разложить диспетчер фазы `Scan` по тематическим файлам, закрыть вопрос по U+202F и снять эталонный снимок веб-сервиса Лебедева.

**Architecture:** Сейчас HTML-режим гоняет каждый текстовый сегмент через все фазы и заводит на сегмент три буфера. После плана каждая фаза — один проход по ДОКУМЕНТУ: фаза сама ходит по сегментам через `MarkupScanner`, обрабатывает текстовые узлы и копирует разметку байт в байт. Промежуточных буферов становится три на документ вместо трёх на сегмент, а слово, разорванное тегом (`<b>сло</b>во`), впервые становится одним словом для словарных правил. Ни одно правило в этом плане не добавляется и не меняет поведения: все 370 существующих тестов обязаны остаться зелёными на каждом шаге.

**Tech Stack:** C# 13, .NET 10 SDK; целевые платформы `netstandard2.0`, `net8.0`, `net10.0`; xunit.v3 поверх Microsoft.Testing.Platform; bash + curl для разового снимка оракула.

**Spec:** `docs/spec.md`; передача из плана 1 — `docs/superpowers/plans/2026-09-08-input-for-plan-2.md`

## Global Constraints

- Целевые платформы ядра: `netstandard2.0;net8.0;net10.0`. Внешних зависимостей нет; на `netstandard2.0` допускаются только `System.Memory` и `PolySharp`, уже подключённые.
- `SearchValues<char>` и `FrozenSet<string>` — только под `#if NET8_0_OR_GREATER`, с эквивалентом для `netstandard2.0`. Поведение веток обязано совпадать.
- Никаких регулярных выражений. `System.Text.RegularExpressions` в `src/Typographer` не используется.
- `TreatWarningsAsErrors` включён, `GenerateDocumentationFile` включён. Каждый публичный член — с XML-комментарием **на русском языке**.
- Ноль аллокаций на пути `Process(ReadOnlySpan<char>, IBufferWriter<char>)` после прогрева пула. `CharBuffer` — изменяемая структура, передаётся только по `ref`, не боксируется и не захватывается замыканием.
- Левый контекст правила читается ИЗ БУФЕРА, правый — из исходной строки. Инвариант введён планом 1, нарушение возвращает разрывы идемпотентности.
- Ветка `feature/rules-infra` от `develop`. Прямой коммит в `master` и `develop` запрещён.
- Каждый коммит заканчивается строками:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
  ```
- Тесты гоняются командой `dotnet test` БЕЗ префикса `rtk` (под Microsoft.Testing.Platform фильтр rtk врёт «0 tests, exit code 5»). Остальные команды — с `rtk`.

---

## Структура файлов

| Файл | Что с ним происходит |
|---|---|
| `src/Typographer/Internal/Preparer.cs` | + `RunDocument`: фаза Prepare по документу |
| `src/Typographer/Internal/TextScanner.cs` | + `RunDocument`; посимвольный цикл переезжает в диспетчер (Task 7) |
| `src/Typographer/Internal/WordBinder.cs` | + `RunDocument` с состоянием слова сквозь разметку |
| `src/Typographer/Internal/LayoutWriter.cs` | `Run` становится документной: nobr внутри текстовых сегментов + переносы и абзацы |
| `src/Typographer/Internal/Emitter.cs` | + `EncodeDocument`: кодирование только внутри текстовых сегментов |
| `src/Typographer/HtmlTypograf.cs` | `Run` — пять вызовов фаз, три буфера на документ |
| `src/Typographer/Internal/Scan/SpaceRules.cs` | новый: пробелы и запятая |
| `src/Typographer/Internal/Scan/QuoteRules.cs` | новый: кавычки и апостроф |
| `src/Typographer/Internal/Scan/DashRules.cs` | новый: тире, дефис, минус |
| `src/Typographer/Internal/Scan/PunctuationRules.cs` | новый: многоточие |
| `src/Typographer/Internal/EntityTable.cs` | U+202F становится кодируемым числовым кодом |
| `tests/Typographer.Tests/Rules/BindAcrossMarkupTests.cs` | новый: слово, разорванное тегом |
| `tests/Typographer.Tests/Rules/RuleSetTests.cs` | + тест на уникальность и ёмкость индексов реестра |
| `tools/oracle-snapshot.sh`, `tools/oracle-inputs.txt` | новые: разовый снимок веб-сервиса Лебедева |
| `docs/oracle/lebedev.md` | новый: сам снимок, входит в репозиторий |

Порядок задач: сначала документные проходы фаза за фазой (1–6, каждая задача оставляет тесты зелёными), затем чистый рефакторинг диспетчера (7), затем мелочи (8–10).

---

### Task 1: Фаза Prepare по документу

**Files:**
- Modify: `src/Typographer/Internal/Preparer.cs`
- Modify: `src/Typographer/HtmlTypograf.cs:62-155` (метод `RunSegments`)
- Test: `tests/Typographer.Tests/Rules/PrepareTests.cs`

**Interfaces:**
- Consumes: `MarkupScanner(ReadOnlySpan<char>)`, `bool TryRead(out Segment)`, `Segment(SegmentKind Kind, int Start, int Length, bool IsBlock, bool PreventsParagraphWrapping)` — всё из `src/Typographer/Internal/MarkupScanner.cs`.
- Produces: `internal static void Preparer.RunDocument(ReadOnlySpan<char> html, RuleSet rules, ref CharBuffer buffer)` — декодирует типографские сущности в текстовых узлах, разметку и защищённые зоны копирует байт в байт, метку порядка байт снимает только в начале документа.

Задача — рефакторинг, а не починка: фаза Prepare уже ведёт себя правильно, меняется только место, где живёт обход сегментов. Тесты поэтому пишутся не «красными», а как страховка переноса: они обязаны быть зелёными и до, и после.

- [ ] **Step 1: Закрепить поведение тестами**

В `tests/Typographer.Tests/Rules/PrepareTests.cs` добавить:

```csharp
[Fact]
public void EntityDecodedInEverySegmentNotOnlyTheFirst()
{
    // Сущность во ВТОРОМ текстовом узле доходит до правила тире так же, как в первом:
    // документный проход не должен зависеть от номера сегмента.
    string result = new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.None.With(RuleId.Ru.Dash.Main),
    }).Process("<b>раз</b> два&nbsp;- три");

    // Правило тире отбивает левый пробел неразрывным — им и оказывается
    // раскодированный &nbsp;.
    Assert.Equal($"<b>раз</b> два{Chars.Nbsp}{Chars.MDash} три", result);
}

[Fact]
public void BomInsideSecondSegmentIsKept()
{
    // Метка порядка байт снимается только в начале ДОКУМЕНТА: внутри текста её
    // удаление могло бы склеить соседние символы в тег или сущность.
    string result = new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.Default,
    }).Process($"<b>раз</b>{Chars.Bom}два");

    Assert.Contains(Chars.Bom, result);
}
```

Невидимые символы в тестах пишутся через `Chars.*`, а не литералами — так принято в существующих тестах (`EmitterTests`), и литеральный U+FEFF в файле иначе невозможно вычитать глазами.

- [ ] **Step 2: Прогнать тесты**

Run: `dotnet test`
Expected: PASS оба. Если падают — переносить нечего: сначала разобраться, почему поведение отличается от ожидаемого.

- [ ] **Step 3: Добавить документный проход в `Preparer`**

В `src/Typographer/Internal/Preparer.cs`:

```csharp
/// <summary>
/// Фаза Prepare по документу: текстовые узлы приводятся к одному представлению,
/// разметка и защищённые зоны копируются байт в байт.
/// </summary>
/// <param name="html">Исходный документ.</param>
/// <param name="rules">Набор включённых правил.</param>
/// <param name="buffer">Приёмник.</param>
public static void RunDocument(ReadOnlySpan<char> html, RuleSet rules, ref CharBuffer buffer)
{
    var scanner = new MarkupScanner(html);
    while (scanner.TryRead(out Segment segment))
    {
        ReadOnlySpan<char> slice = html.Slice(segment.Start, segment.Length);
        if (segment.Kind != SegmentKind.Text)
        {
            buffer.Write(slice);
            continue;
        }

        // Начало документа — свойство ДОКУМЕНТА, а не сегмента: метка порядка байт
        // допустима только в самом начале входа, внутри текста она остаётся символом.
        Run(slice, decodeEntities: true, rules, ref buffer, isDocumentStart: segment.Start == 0);
    }
}
```

- [ ] **Step 4: Переключить `HtmlTypograf` на документный Prepare**

В `HtmlTypograf.RunSegments` удалить буфер `prepared` и вызов `Preparer.Run(...)`, а в `HtmlTypograf.Run` вызвать `Preparer.RunDocument` ДО сегментного цикла, передав его результат дальше вместо `source`:

```csharp
private void Run(ReadOnlySpan<char> source, ref CharBuffer buffer)
{
    var prepared = new CharBuffer(source.Length + (source.Length >> 2));
    try
    {
        Preparer.RunDocument(source, _options.Rules, ref prepared);
        RunPipeline(prepared.AsSpan(), ref buffer);
    }
    finally
    {
        prepared.Dispose();
    }
}
```

где `RunPipeline` — прежнее тело `Run` (проверка `UseBr`/`UseP`, буфер `body`, `RunSegments`, `LayoutWriter.WriteBreaks`), а в `RunSegments` текстовый сегмент теперь идёт сразу в `TextScanner.Run`.

- [ ] **Step 5: Прогнать тесты**

Run: `dotnet test`
Expected: PASS — падать не должно ничего, включая тесты, добавленные этой задачей.

- [ ] **Step 6: Коммит**

```bash
rtk git add -A && rtk git commit -F - <<'EOF'
refactor: фаза Prepare проходит по документу, а не по сегменту

Начало документа — свойство документа: метка порядка байт снимается
только в самом начале входа, а сущности декодируются одинаково во всех
текстовых узлах, а не только в первом.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
EOF
```

---

### Task 2: Фаза Scan по документу

**Files:**
- Modify: `src/Typographer/Internal/TextScanner.cs`
- Modify: `src/Typographer/HtmlTypograf.cs`
- Test: `tests/Typographer.Tests/Rules/ScannerContextTests.cs`

**Interfaces:**
- Consumes: `Preparer.RunDocument` (Task 1); `ScanState` с полями `Quotes`, `Last`, `TrailingDigits`.
- Produces: `internal static bool TextScanner.RunDocument(ReadOnlySpan<char> html, RuleSet rules, ref CharBuffer buffer)` — возвращает `canWrapParagraphs`: `true`, если в документе нет ни блочной, ни незакрытой разметки. Значение потребляет `LayoutWriter.Run` (Task 4).

- [ ] **Step 1: Закрепить инвариант тестом**

В `tests/Typographer.Tests/Rules/ScannerContextTests.cs` добавить:

```csharp
[Fact]
public void ScanStateSurvivesManySegments()
{
    // Стек кавычек и последний символ живут сквозь ВСЕ сегменты: закрывающая
    // кавычка в четвёртом узле обязана закрыть уровень, открытый в первом.
    string result = new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.None.With(RuleId.Common.Punctuation.Quote),
    }).Process("<i>\"</i>а<b>б</b>\"");

    Assert.Equal("<i>«</i>а<b>б</b>»", result);
}
```

- [ ] **Step 2: Прогнать тест**

Run: `dotnet test`
Expected: PASS — состояние сканера уже протянуто планом 1. Тест закрепляет инвариант перед переносом кода; если он падает, переносить нечего — сначала чинить.

- [ ] **Step 3: Перенести сегментный цикл в `TextScanner.RunDocument`**

В `src/Typographer/Internal/TextScanner.cs` добавить (тело переезжает из `HtmlTypograf.RunSegments` без изменений в логике):

```csharp
/// <summary>
/// Фаза Scan по документу: правила применяются к текстовым узлам, разметка копируется.
/// </summary>
/// <param name="html">Документ после фазы Prepare.</param>
/// <param name="rules">Набор включённых правил.</param>
/// <param name="buffer">Приёмник.</param>
/// <returns>Можно ли оборачивать документ в абзацы: нет блочной и незакрытой разметки.</returns>
public static bool RunDocument(ReadOnlySpan<char> html, RuleSet rules, ref CharBuffer buffer)
{
    var scanner = new MarkupScanner(html);
    var state = new ScanState();
    bool hasBlockMarkup = false;

    while (scanner.TryRead(out Segment segment))
    {
        ReadOnlySpan<char> slice = html.Slice(segment.Start, segment.Length);
        if (segment.Kind != SegmentKind.Text)
        {
            // Блочный тег разрывает предложение: за </p> начинается новая строка, и
            // правила обязаны видеть её начало, а не последний символ прошлого абзаца.
            if (segment.IsBlock)
            {
                state.Last = '\n';
                state.TrailingDigits = 0;
            }
            else if (segment.Kind == SegmentKind.Protected && segment.Length > 0)
            {
                // Содержимое защищённой зоны не анализируется и потому не может
                // прозрачно соединять числовой контекст по обе стороны от неё.
                state.TrailingDigits = 0;
            }

            hasBlockMarkup |= segment.PreventsParagraphWrapping;
            buffer.Write(slice);
            continue;
        }

        Run(slice, rules, ref state, ref buffer);
    }

    return !hasBlockMarkup && !scanner.HasUnclosedMarkup;
}
```

- [ ] **Step 4: Убрать сегментный цикл из `HtmlTypograf`**

`HtmlTypograf.RunSegments` удаляется целиком. Место вызова:

```csharp
bool canWrapParagraphs = TextScanner.RunDocument(prepared.AsSpan(), _options.Rules, ref scanned);
```

Буферы `scanned`, `bound`, `laidOut` на сегмент удаляются; фазы `Bind`, `Layout`, `Emit` пока вызываются по документу в том же порядке, что раньше по сегменту — их документные версии приходят в задачах 3–5, а до тех пор используются существующие сигнатуры на всём буфере.

- [ ] **Step 5: Прогнать тесты**

Run: `dotnet test`
Expected: FAIL ожидаем в тестах кодирования сущностей (`NamedModeEncodesOnlyTextSegments`) — `Emitter` пока кодирует весь документ, включая разметку. Это чинит Task 5. Если падает что-то ещё — разбираться до перехода дальше.

Чтобы не оставлять ветку красной, порядок внутри задачи такой: Step 4 выполняется вместе с Task 5 Step 3 (документный `Emitter`) в одном коммите. Оба шага маленькие; разрывать их нельзя, потому что промежуточное состояние ломает гарантию 3.

- [ ] **Step 6: Коммит** (после того как Task 5 Step 3 сделан)

```bash
rtk git add -A && rtk git commit -F - <<'EOF'
refactor: фазы Scan и Emit проходят по документу

Сегментный цикл переехал из HtmlTypograf в сами фазы: каждая ходит по
сегментам сама и копирует разметку байт в байт. Буферов стало три на
документ вместо трёх на каждый текстовый узел.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
EOF
```

---

### Task 3: Фаза Bind со состоянием слова сквозь разметку

**Files:**
- Modify: `src/Typographer/Internal/WordBinder.cs`
- Create: `tests/Typographer.Tests/Rules/BindAcrossMarkupTests.cs`
- Modify: `src/Typographer/HtmlTypograf.cs`

**Interfaces:**
- Consumes: `MarkupScanner`, `Dictionaries.IsShortWord(ReadOnlySpan<char>)`, `Dictionaries.IsAbbreviationPart(ReadOnlySpan<char>)`, `CharBuffer.PatchAt(int, char)`.
- Produces: `internal static void WordBinder.RunDocument(ref CharBuffer buffer, RuleSet rules)` — патчит буфер на месте; слово, разорванное строчным тегом, считается одним словом; блочный тег и защищённая зона слово завершают.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/Rules/BindAcrossMarkupTests.cs`:

```csharp
using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class BindAcrossMarkupTests
{
    private static string Run(string html) => new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Nbsp.AfterShortWord)
            .With(RuleId.Ru.Nbsp.Abbr)
            .With(RuleId.Ru.Nbsp.Initials),
    }).Process(html);

    [Fact]
    public void WordSplitByInlineTagIsOneWord()
    {
        // «сло|во» — слово из пяти букв: короткому слову неразрывный пробел положен,
        // длинному нет. Пока состояние не жило сквозь сегменты, «во» выглядело
        // коротким словом и получало неразрывный пробел, которого в тексте нет.
        Assert.Equal("<b>сло</b>во дом", Run("<b>сло</b>во дом"));
    }

    [Fact]
    public void ShortWordSplitByInlineTagStillBinds()
    {
        // «н|а» — короткое слово, разорванное тегом: связь остаётся.
        Assert.Equal($"<b>н</b>а{Chars.Nbsp}дом", Run("<b>н</b>а дом"));
    }

    [Fact]
    public void BlockTagEndsTheWord()
    {
        // <p> — граница строки: слово за неё не продолжается. «во» закрывается
        // блочным тегом, а не пробелом, поэтому связывать его не с чем.
        Assert.Equal("<p>во</p> дом", Run("<p>во</p> дом"));
    }

    [Fact]
    public void ProtectedZoneEndsTheWord()
    {
        // Защищённая зона слово завершает: «во» после </code> — самостоятельное
        // короткое слово, и неразрывный пробел после него ставится.
        Assert.Equal(
            $"<code>сло</code>во{Chars.Nbsp}дом",
            Run("<code>сло</code>во дом"));
    }

    [Fact]
    public void InitialSplitByTagBindsToSurname()
    {
        // Инициал в конце документа связывается с фамилией назад — через тег.
        Assert.Equal($"Пушкин{Chars.Nbsp}<b>А.</b>", Run("Пушкин <b>А.</b>"));
    }
}
```

- [ ] **Step 2: Прогнать тесты и увидеть падения**

Run: `dotnet test`
Expected: FAIL в `WordSplitByInlineTagIsOneWord`, `ShortWordSplitByInlineTagStillBinds`, `ProtectedZoneEndsTheWord`, `InitialSplitByTagBindsToSurname` — фаза Bind сейчас видит один сегмент и о соседних не знает. `BlockTagEndsTheWord` проходит уже сейчас: закрепляем то, что ломать нельзя.

- [ ] **Step 3: Написать документный проход**

В `src/Typographer/Internal/WordBinder.cs` добавить:

```csharp
/// <summary>
/// Максимальная длина слова, которую фаза собирает через разметку. Всё длиннее
/// заведомо не короткое слово, не сокращение и не инициал — накопление прекращается.
/// </summary>
private const int MaxWord = 32;

/// <summary>
/// Фаза Bind по документу: слово, разорванное СТРОЧНЫМ тегом, остаётся одним словом.
/// </summary>
/// <remarks>
/// «&lt;b&gt;сло&lt;/b&gt;во» — одно слово, и словарные правила обязаны видеть его целиком;
/// иначе «во» выглядит коротким словом и получает неразрывный пробел, которого в тексте нет.
/// Блочный тег и защищённая зона слово, наоборот, завершают: за ними начинается новая строка
/// или содержимое, которого фаза не касается.
/// Буквы слова копятся в стековый буфер, а патчатся позиции ДОКУМЕНТА — фаза не меняет длину,
/// поэтому индексы, взятые до тега, остаются валидными после него.
/// </remarks>
public static void RunDocument(ref CharBuffer buffer, RuleSet rules)
{
    bool afterShortWord = rules.Contains(RuleId.Common.Nbsp.AfterShortWord);
    bool abbr = rules.Contains(RuleId.Ru.Nbsp.Abbr);
    bool initials = rules.Contains(RuleId.Ru.Nbsp.Initials);
    if (!afterShortWord && !abbr && !initials)
    {
        return;
    }

    Span<char> word = stackalloc char[MaxWord];
    var state = new BindState();
    ReadOnlySpan<char> document = buffer.AsSpan();
    var scanner = new MarkupScanner(document);

    while (scanner.TryRead(out Segment segment))
    {
        if (segment.Kind == SegmentKind.Markup && !segment.IsBlock)
        {
            // Строчный тег для слова прозрачен: ни буквы, ни границы он не даёт.
            continue;
        }

        if (segment.Kind != SegmentKind.Text)
        {
            state.Reset();
            continue;
        }

        BindSegment(ref buffer, document, segment, word, ref state, afterShortWord, abbr, initials);
    }

    // Конец документа тоже завершает токен: «Пушкин А.» кончается инициалом.
    if (initials && state.WordLength == 2 && state.PrevSpaceIndex >= 0
        && IsInitial(word.Slice(0, state.WordLength)))
    {
        buffer.PatchAt(state.PrevSpaceIndex, Chars.Nbsp);
    }
}
```

Состояние выносится в структуру рядом с `WordBinder`:

```csharp
/// <summary>Состояние фазы Bind, живущее сквозь текстовые сегменты документа.</summary>
internal struct BindState
{
    /// <summary>Сколько букв текущего слова уже собрано.</summary>
    public int WordLength;

    /// <summary>Слово длиннее <c>MaxWord</c>: словарная проверка ему заведомо не нужна.</summary>
    public bool WordOverflow;

    /// <summary>Индекс пробела перед текущим словом в документе, -1 — пробела нет.</summary>
    public int PrevSpaceIndex;

    public BindState()
    {
        WordLength = 0;
        WordOverflow = false;
        PrevSpaceIndex = -1;
    }

    /// <summary>Граница, за которой слово продолжаться не может.</summary>
    public void Reset()
    {
        WordLength = 0;
        WordOverflow = false;
        PrevSpaceIndex = -1;
    }
}
```

- [ ] **Step 4: Разобрать текстовый сегмент**

Тело `BindSegment` повторяет решение существующего `Run`, но координаты — документные, а буква копится в `word`:

```csharp
private static void BindSegment(
    ref CharBuffer buffer,
    ReadOnlySpan<char> document,
    Segment segment,
    Span<char> word,
    ref BindState state,
    bool afterShortWord,
    bool abbr,
    bool initials)
{
    int end = segment.Start + segment.Length;
    for (int i = segment.Start; i < end; i++)
    {
        char c = document[i];

        if (char.IsLetter(c) || c == '.')
        {
            // Точка слово не завершает: она может быть частью сокращения или инициала,
            // решение принимает следующий за ней пробел.
            if (state.WordLength < word.Length)
            {
                word[state.WordLength++] = c;
            }
            else
            {
                state.WordOverflow = true;
            }

            continue;
        }

        if (c != ' ')
        {
            // Токен закончился знаком препинания: «Пушкин А., автор». Вперёд связывать
            // нечего, но связь НАЗАД, с фамилией, инициалу по-прежнему нужна.
            if (initials && !state.WordOverflow && state.PrevSpaceIndex >= 0
                && IsInitial(word.Slice(0, state.WordLength)))
            {
                buffer.PatchAt(state.PrevSpaceIndex, Chars.Nbsp);
            }

            state.Reset();
            continue;
        }

        if (state.WordLength == 0)
        {
            state.PrevSpaceIndex = -1;
            continue;
        }

        ReadOnlySpan<char> token = word.Slice(0, state.WordLength);
        bool hasDot = token[token.Length - 1] == '.';
        ReadOnlySpan<char> letters = hasDot ? token.Slice(0, token.Length - 1) : token;
        bool isInitial = initials && !state.WordOverflow && IsInitial(token);

        bool bind = !state.WordOverflow
            && ((afterShortWord && !hasDot && Dictionaries.IsShortWord(letters))
                || (abbr && hasDot && Dictionaries.IsAbbreviationPart(letters))
                || isInitial);

        if (isInitial && state.PrevSpaceIndex >= 0)
        {
            buffer.PatchAt(state.PrevSpaceIndex, Chars.Nbsp);
        }

        if (bind)
        {
            buffer.PatchAt(i, Chars.Nbsp);
        }

        state.WordLength = 0;
        state.WordOverflow = false;
        state.PrevSpaceIndex = i;
    }
}
```

- [ ] **Step 5: Переключить оба типографа**

В `HtmlTypograf` вызвать `WordBinder.RunDocument(ref scanned, _options.Rules)`. В `TextTypograf` оставить `WordBinder.Run(ref buffer, _options.Rules)`: в обычном тексте сегментов нет, документный проход там ничего не даст.

- [ ] **Step 6: Прогнать тесты**

Run: `dotnet test`
Expected: PASS — падать не должно ничего, включая тесты, добавленные этой задачей.

- [ ] **Step 7: Коммит**

```bash
rtk git add -A && rtk git commit -F - <<'EOF'
feat: слово, разорванное строчным тегом, снова одно слово

Состояние фазы Bind живёт сквозь текстовые сегменты: «<b>сло</b>во» для
словарных правил — одно слово, а не два, и «во» больше не выглядит
коротким словом. Блочный тег и защищённая зона слово завершают.

Правила неразрывных пробелов из полного набора упирались в это первыми.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
EOF
```

---

### Task 4: Фаза Layout по документу

**Files:**
- Modify: `src/Typographer/Internal/LayoutWriter.cs`
- Modify: `src/Typographer/HtmlTypograf.cs`
- Test: `tests/Typographer.Tests/Rules/LayoutTests.cs`

**Interfaces:**
- Consumes: `TextScanner.RunDocument` (возвращает `canWrapParagraphs`).
- Produces: `internal static void LayoutWriter.Run(ReadOnlySpan<char> source, HtmlOptions options, bool canWrapParagraphs, ref CharBuffer buffer)` — единственная точка входа фазы; неразрывные цепочки ставятся внутри текстовых сегментов, переносы и абзацы — по документу. Прежние `Run(source, options, ref buffer)` и `WriteBreaks(...)` уходят.

- [ ] **Step 1: Написать тест на границу цепочки**

В `tests/Typographer.Tests/Rules/LayoutTests.cs` добавить:

```csharp
[Fact]
public void NobrChainDoesNotCrossTag()
{
    // Неразрывная цепочка живёт ВНУТРИ текстового узла: через тег она не тянется,
    // иначе <nobr> пересечётся с <b> и разметка перестанет быть валидной.
    string result = new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.None,
        MaxNobr = 3,
    }).Process("раз два <b>три</b> четыре пять");

    Assert.DoesNotContain("<nobr>раз два <b>", result);
    Assert.Contains("<b>три</b>", result);
}
```

- [ ] **Step 2: Прогнать тест**

Run: `dotnet test`
Expected: PASS — так работает и сейчас (nobr применяется к сегменту). Тест закрепляет поведение перед переносом.

- [ ] **Step 3: Сделать фазу документной**

В `src/Typographer/Internal/LayoutWriter.cs` заменить обе точки входа одной:

```csharp
/// <summary>
/// Фаза Layout по документу.
/// </summary>
/// <param name="source">Документ после фаз Scan и Bind.</param>
/// <param name="options">Настройки HTML-режима.</param>
/// <param name="canWrapParagraphs">Разрешено ли оборачивать абзацы: нет блочной и незакрытой разметки.</param>
/// <param name="buffer">Приёмник.</param>
public static void Run(
    ReadOnlySpan<char> source, HtmlOptions options, bool canWrapParagraphs, ref CharBuffer buffer)
{
    bool useP = options.UseP && canWrapParagraphs;
    if (options.MaxNobr <= 0)
    {
        WriteBreaks(source, options.UseBr, useP, ref buffer);
        return;
    }

    // Неразрывные цепочки ставятся ПЕРВЫМИ и только внутри текстовых узлов; переносы и
    // абзацы считаются уже по документу с этими тегами — тот же порядок, что был при
    // посегментной обработке, поэтому вывод не меняется.
    var chained = new CharBuffer(source.Length + (source.Length >> 2));
    try
    {
        var scanner = new MarkupScanner(source);
        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = source.Slice(segment.Start, segment.Length);
            if (segment.Kind == SegmentKind.Text)
            {
                WriteWithNobr(slice, options.MaxNobr, ref chained);
            }
            else
            {
                chained.Write(slice);
            }
        }

        WriteBreaks(chained.AsSpan(), options.UseBr, useP, ref buffer);
    }
    finally
    {
        chained.Dispose();
    }
}
```

`WriteBreaks` становится приватным, публичная перегрузка `Run(source, options, ref buffer)` удаляется.

- [ ] **Step 4: Переключить `HtmlTypograf`**

```csharp
LayoutWriter.Run(scanned.AsSpan(), _options, canWrapParagraphs, ref laidOut);
```

Ветка «ни `UseBr`, ни `UseP` — второй буфер не заводим» из `HtmlTypograf` уходит: решение о буфере принимает сама фаза.

- [ ] **Step 5: Прогнать тесты**

Run: `dotnet test`
Expected: PASS — падать не должно ничего, включая тесты, добавленные этой задачей. Особенно `UseP_WithMaxNobr_KeepsChainInsideParagraph` и `LongChain_TerminatesAndLosesNoChars` — они ловят смену порядка подфаз.

- [ ] **Step 6: Коммит**

```bash
rtk git add -A && rtk git commit -F - <<'EOF'
refactor: фаза Layout — одна документная точка входа

Неразрывные цепочки ставятся внутри текстовых узлов, переносы и абзацы —
по документу; порядок подфаз тот же, что был при посегментной обработке.
Решение о втором буфере принимает сама фаза, а не типограф.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
EOF
```

---

### Task 5: Фаза Emit по документу

**Files:**
- Modify: `src/Typographer/Internal/Emitter.cs`
- Modify: `src/Typographer/HtmlTypograf.cs`
- Test: `tests/Typographer.Tests/Internal/EmitterTests.cs`

**Interfaces:**
- Consumes: `MarkupScanner`, `EntityTable.NameOf(char)`, `EntityTable.IsInvisible(char)`.
- Produces: `internal static void Emitter.EncodeDocument(ReadOnlySpan<char> html, EntityMode mode, ref CharBuffer destination)` — кодирует только внутри текстовых узлов; разметка и защищённые зоны выходят байт в байт.

- [ ] **Step 1: Написать тест на гарантию 3**

В `tests/Typographer.Tests/Internal/EmitterTests.cs` добавить:

```csharp
[Fact]
public void DocumentEncodingSkipsMarkupAndProtectedZones()
{
    // Кавычка-ёлочка в значении атрибута и внутри <code> сущностью не становится:
    // гарантия 3 обещает разметку и защищённые зоны байт в байт.
    string result = new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.None,
        Entities = EntityMode.Named,
    }).Process("<a title=\"«х»\">«текст»</a><code>«код»</code>");

    Assert.Equal(
        "<a title=\"«х»\">&laquo;текст&raquo;</a><code>«код»</code>",
        result);
}
```

- [ ] **Step 2: Прогнать тест**

Run: `dotnet test`
Expected: PASS до Task 2 Step 4 и FAIL после него, пока `EncodeDocument` не написан. Именно поэтому Task 2 Step 4 и этот шаг едут одним коммитом.

- [ ] **Step 3: Написать документное кодирование**

В `src/Typographer/Internal/Emitter.cs`:

```csharp
/// <summary>
/// Фаза Emit по документу: кодируются только текстовые узлы.
/// </summary>
/// <remarks>
/// Разметка не кодируется ни в одном режиме. Кавычка в значении атрибута и неразрывный
/// пробел внутри &lt;code&gt; — часть разметки и защищённой зоны, а гарантия 3 обещает их
/// байт в байт.
/// </remarks>
/// <param name="html">Документ после фазы Layout.</param>
/// <param name="mode">Режим вывода сущностей.</param>
/// <param name="destination">Приёмник.</param>
public static void EncodeDocument(ReadOnlySpan<char> html, EntityMode mode, ref CharBuffer destination)
{
    if (mode == EntityMode.Symbols)
    {
        destination.Write(html);
        return;
    }

    var scanner = new MarkupScanner(html);
    while (scanner.TryRead(out Segment segment))
    {
        ReadOnlySpan<char> slice = html.Slice(segment.Start, segment.Length);
        if (segment.Kind == SegmentKind.Text)
        {
            Encode(slice, mode, ref destination);
        }
        else
        {
            destination.Write(slice);
        }
    }
}
```

- [ ] **Step 4: Переключить `HtmlTypograf` и убрать посегментные буферы**

Итоговое тело:

```csharp
private void Run(ReadOnlySpan<char> source, ref CharBuffer output)
{
    var prepared = new CharBuffer(source.Length + (source.Length >> 2));
    var scanned = new CharBuffer(source.Length + (source.Length >> 2));
    var laidOut = new CharBuffer(source.Length + (source.Length >> 2));
    try
    {
        Preparer.RunDocument(source, _options.Rules, ref prepared);
        bool canWrapParagraphs = TextScanner.RunDocument(prepared.AsSpan(), _options.Rules, ref scanned);
        WordBinder.RunDocument(ref scanned, _options.Rules);
        LayoutWriter.Run(scanned.AsSpan(), _options, canWrapParagraphs, ref laidOut);
        Emitter.EncodeDocument(laidOut.AsSpan(), _options.Entities, ref output);
    }
    finally
    {
        laidOut.Dispose();
        scanned.Dispose();
        prepared.Dispose();
    }
}
```

- [ ] **Step 5: Прогнать тесты**

Run: `dotnet test`
Expected: PASS — падать не должно ничего, включая тесты, добавленные этой задачей.

- [ ] **Step 6: Коммит** — общий с Task 2 Step 6.

---

### Task 6: Проверка гарантий после перестройки

**Files:**
- Test: `tests/Typographer.Tests/Guarantees/RobustnessTests.cs`
- Test: `tests/Typographer.Tests/PipelineTests.cs`

**Interfaces:**
- Consumes: всё, что собрано задачами 1–5.
- Produces: ничего для кода; задача закрепляет, что перестройка буферов не нарушила гарантии 1, 2 и 6.

- [ ] **Step 1: Написать тесты**

В `tests/Typographer.Tests/Guarantees/RobustnessTests.cs` добавить:

```csharp
[Fact]
public void LimitCountsWholeDocumentNotSegment()
{
    // Предел — на РЕЗУЛЬТАТ документа. Каждый сегмент по отдельности в предел
    // укладывается, документ целиком — нет.
    var typograf = new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.None,
        MaxOutputLength = 20,
    });

    Assert.Throws<OutputTooLargeException>(
        () => typograf.Process("<b>раз</b><b>два</b><b>три</b><b>четыре</b>"));
}

[Fact]
[Fact]
public void MultiSegmentDocumentStaysIdempotent()
{
    // Гарантия 5 на документе из многих сегментов: документные проходы держат
    // состояние сквозь разметку, и второй прогон обязан ничего не изменить.
    var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });

    string once = typograf.Process("Он <i>сказал</i>: \"это <b>важно</b>, и т. д.\" - и ушёл");
    Assert.Equal(once, typograf.Process(once));
}
```

- [ ] **Step 2: Прогнать тесты**

Run: `dotnet test`
Expected: PASS. Если `LimitCountsWholeDocumentNotSegment` падает — предел где-то снова проверяется по частям; чинить до коммита.

- [ ] **Step 3: Прогнать бенчмарк и записать цифры**

Run: `rtk dotnet run -c Release --project bench/Typographer.Bench -- --filter '*'`
Ожидание: время на документе из одного-двух сегментов не выросло больше чем на 10 %, число аллокаций на пути `IBufferWriter<char>` осталось нулевым. Цифры до и после вписать в тело коммита.

- [ ] **Step 4: Коммит**

```bash
rtk git add -A && rtk git commit -F - <<'EOF'
test: гарантии после перехода на документные проходы

Предел длины считается по документу, а не по сегментам; текст, нарезанный
тегами, даёт тот же результат, что и цельный. Бенчмарк: вписать измеренные медианы до и после.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
EOF
```

---

### Task 7: Диспетчер фазы Scan

**Files:**
- Modify: `src/Typographer/Internal/TextScanner.cs`
- Create: `src/Typographer/Internal/Scan/PunctuationRules.cs`
- Create: `src/Typographer/Internal/Scan/QuoteRules.cs`
- Create: `src/Typographer/Internal/Scan/SpaceRules.cs`
- Create: `src/Typographer/Internal/Scan/DashRules.cs`

**Interfaces:**
- Consumes: `ScanState`, `CharBuffer`, `RuleSet.Contains(RuleId)` — проверка включённости стоит один бит-тест, поэтому флаги перед циклом не поднимаются.
- Produces: соглашение о сигнатуре правила фазы Scan, на которое опираются планы 2b и 2c:
  ```csharp
  internal static bool TryApply(
      ReadOnlySpan<char> source, int index, char previous, RuleSet rules,
      ref ScanState state, ref CharBuffer buffer);
  ```
  `true` означает «символ обработан и записан в буфер»; `false` — «правило не применилось, символ пишет диспетчер».

- [ ] **Step 1: Убедиться, что тесты зелёные до рефакторинга**

Run: `dotnet test`
Expected: PASS. Задача — чистый рефакторинг: ни один тест не добавляется и не меняется.

- [ ] **Step 2: Вынести правила многоточия и кавычек**

Создать `src/Typographer/Internal/Scan/PunctuationRules.cs` и `QuoteRules.cs`, перенеся в них тела соответствующих веток из `TextScanner.Run` вместе с приватными помощниками (`ClosesEllipsis`, `IsOpeningContext`, `IsReadyOpeningQuote`, `IsReadyClosingQuote`) и их комментариями — комментарии объясняют неочевидные решения и обязаны переехать целиком. Пример:

```csharp
using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>Правила кавычек и апострофа фазы Scan.</summary>
internal static class QuoteRules
{
    public static bool TryApply(
        ReadOnlySpan<char> source, int index, char previous, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        char c = source[index];
        if (rules.Contains(RuleId.Common.Punctuation.Quote) && c == '"')
        {
            // Штрих после цифры прямой кавычкой и остаётся: 5" — пять дюймов.
            bool inch = char.IsDigit(previous) && state.Quotes.IsEmpty;
            if (inch)
            {
                buffer.Write(c);
                return true;
            }

            bool opening = IsOpeningContext(previous) || (state.Quotes.IsEmpty && previous == ':');
            buffer.Write(opening ? state.Quotes.Open() : state.Quotes.Close());
            return true;
        }

        // Остальные ветки переезжают сюда без изменений логики и вместе со своими
        // комментариями: готовая открывающая кавычка (двигает стек уровней),
        // готовая закрывающая (с оговоркой про правую одинарную как апостроф между
        // буквами) и правило апострофа для символа '. Помощники IsOpeningContext,
        // IsReadyOpeningQuote и IsReadyClosingQuote переезжают из TextScanner сюда.
        return false;
    }
}
```

- [ ] **Step 3: Прогнать тесты**

Run: `dotnet test`
Expected: PASS. Каждый вынос — отдельный прогон; если тесты покраснели, вынос сделан не байт в байт.

- [ ] **Step 4: Вынести правила пробелов и тире**

Создать `SpaceRules.cs` (ветка `' '`, дописывание пробела после запятой, `IsPunctuation`, `IsClosing`, `NeedsSpaceAfterComma`) и `DashRules.cs` (ветка `'-'`, `IsNumberAhead`, `IsYearBefore`, `IsYearAfter`, константа `YearDigits`). `CountTrailingDigits` остаётся в `TextScanner`: это учёт состояния, а не правило.

- [ ] **Step 5: Свести цикл к диспетчеру**

`TextScanner.Run` становится таким:

```csharp
public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref ScanState state, ref CharBuffer buffer)
{
    for (int i = 0; i < source.Length; i++)
    {
        char c = source[i];
        char previous = buffer.Length > 0 ? buffer.CharAt(buffer.Length - 1) : state.Last;

        // switch по символу-триггеру: компилятор строит по нему таблицу переходов, и
        // цена диспетчеризации не растёт с числом правил. Порядок правил на один и тот
        // же символ задан порядком вызовов внутри ветки — в одном месте и явно.
        bool handled = c switch
        {
            '.' => PunctuationRules.TryApply(source, i, previous, rules, ref state, ref buffer),
            '"' or '\'' or Chars.Laquo or Chars.Raquo or Chars.Bdquo or Chars.Ldquo
                or Chars.Lsquo or Chars.Rsquo
                => QuoteRules.TryApply(source, i, previous, rules, ref state, ref buffer),
            ' ' => SpaceRules.TryApply(source, i, previous, rules, ref state, ref buffer),
            '-' => DashRules.TryApply(source, i, previous, rules, ref state, ref buffer),
            _ => false,
        };

        if (handled)
        {
            continue;
        }

        buffer.Write(c);
        SpaceRules.WriteSpaceAfterComma(source, i, previous, rules, ref state, ref buffer);
    }

    if (buffer.Length > 0)
    {
        state.Last = buffer.CharAt(buffer.Length - 1);
        state.TrailingDigits = CountTrailingDigits(ref buffer, state.TrailingDigits);
    }
}
```

- [ ] **Step 6: Прогнать тесты и бенчмарк**

Run: `dotnet test`
Expected: PASS, число тестов не изменилось.

Run: `rtk dotnet run -c Release --project bench/Typographer.Bench -- --filter '*'`
Ожидание: время не выросло больше чем на 5 % — диспетчеризация по `switch` не должна стоить дороже цепочки `if`.

- [ ] **Step 7: Коммит**

```bash
rtk git add -A && rtk git commit -F - <<'EOF'
refactor: правила фазы Scan разложены по тематическим файлам

Цикл сканера стал диспетчером: switch по символу-триггеру выбирает группу
правил, сами правила живут в Internal/Scan/*. Порядок правил на один
символ задан явно и в одном месте — без этого 55 правил плана 2b
превратили бы сканер в две тысячи строк, где порядок виден только чтением
подряд.

Поведение не менялось: тесты те же, все зелёные.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
EOF
```

---

### Task 8: Узкий неразрывный пробел U+202F

**Files:**
- Modify: `src/Typographer/Internal/EntityTable.cs`
- Modify: `src/Typographer/Internal/Emitter.cs`
- Test: `tests/Typographer.Tests/Internal/EntityTableTests.cs`
- Test: `tests/Typographer.Tests/Internal/EmitterTests.cs`

**Interfaces:**
- Consumes: `EntityTable.NameOf(char)`, `EntityTable.IsInvisible(char)`.
- Produces: `EntityTable.IsEncodable(char)` — символ кодируется, даже если буквенного имени у него нет. `Emitter` при `NameOf == null` пишет числовой код.

- [ ] **Step 1: Написать падающий тест**

В `tests/Typographer.Tests/Internal/EntityTableTests.cs`:

```csharp
[Fact]
public void NarrowNbspHasNoNameButIsEncodable()
{
    // Стандартного буквенного имени у U+202F нет, а невидимым символом в выводе
    // он остаться не должен: режим Named обязан дать числовой код.
    Assert.Null(EntityTable.NameOf(Chars.NarrowNbsp));
    Assert.True(EntityTable.IsEncodable(Chars.NarrowNbsp));
    Assert.True(EntityTable.IsInvisible(Chars.NarrowNbsp));
}
```

В `tests/Typographer.Tests/Internal/EmitterTests.cs`:

```csharp
[Theory]
[InlineData(EntityMode.Named, "а&#8239;б")]
[InlineData(EntityMode.Numeric, "а&#8239;б")]
[InlineData(EntityMode.Mixed, "а&#8239;б")]
public void NarrowNbspIsWrittenAsNumericCode(EntityMode mode, string expected)
{
    // Буквенного имени нет ни в одном режиме, поэтому во всех трёх — числовой код.
    Assert.Equal(expected, Encode($"а{Chars.NarrowNbsp}б", mode));
}

[Fact]
public void SymbolsModeKeepsNarrowNbspAsChar()
{
    string input = $"а{Chars.NarrowNbsp}б";
    Assert.Equal(input, Encode(input, EntityMode.Symbols));
}
```

`Encode` — уже существующий в `EmitterTests` приватный помощник над `CharBuffer`.

- [ ] **Step 2: Прогнать тесты**

Run: `dotnet test`
Expected: FAIL — `IsEncodable` не существует, U+202F выходит символом во всех режимах.

- [ ] **Step 3: Реализовать**

В `EntityTable` добавить:

```csharp
/// <summary>
/// Символ кодируется сущностью. Буквенное имя есть не у всех: у узкого неразрывного
/// пробела U+202F стандартного имени нет, но оставлять его в выводе невидимым символом
/// нельзя — он пишется числовым кодом.
/// </summary>
public static bool IsEncodable(char c) => NameOf(c) is not null || c == Chars.NarrowNbsp;
```

`IsInvisible` дополнить символом `Chars.NarrowNbsp`. В `Emitter.Encode` условие переписать:

```csharp
string? name = EntityTable.NameOf(c);
bool encode = EntityTable.IsEncodable(c)
    && (mode != EntityMode.Mixed || EntityTable.IsInvisible(c));

if (!encode)
{
    destination.Write(c);
    continue;
}

destination.Write('&');
if (mode == EntityMode.Numeric || name is null)
{
    destination.Write('#');
    WriteDecimal(ref destination, c);
}
else
{
    destination.Write(name.AsSpan());
}

destination.Write(';');
```

- [ ] **Step 4: Прогнать тесты**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Записать решение в передачу планам**

В `docs/superpowers/plans/2026-09-08-input-for-plan-2.md` в абзаце про U+202F заменить «решить, выводить числовым кодом или оставлять символом» на «решено: числовым кодом `&#8239;`, см. план 2a, задача 8».

- [ ] **Step 6: Коммит**

```bash
rtk git add -A && rtk git commit -F - <<'EOF'
feat: узкий неразрывный пробел выводится числовым кодом

Буквенного имени у U+202F нет, но невидимым символом в выводе он
оставаться не должен: во всех режимах, кроме Symbols, пишется &#8239;.
Символ появится с правилами знака номера и параграфа (ГОСТ 16.4).

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
EOF
```

---

### Task 9: Защита реестра правил от роста до 107

**Files:**
- Test: `tests/Typographer.Tests/Rules/RuleSetTests.cs`

**Interfaces:**
- Consumes: `RuleId.Registry.All`, `RuleId.Index`, `RuleSet` (маска на два `ulong`).
- Produces: ничего для кода; тест ловит ошибку, которую иначе заметят только через 93 правила.

- [ ] **Step 1: Написать тест**

```csharp
[Fact]
public void RegistryIndicesAreUniqueAndFitTheMask()
{
    // Индекс правила — позиция бита в маске RuleSet: два ulong, 128 бит, индекс 0
    // зарезервирован за default(RuleId). Дубликат индекса молча склеил бы два правила
    // в одно, а индекс за пределом маски — включил бы чужое правило.
    var seen = new HashSet<int>();
    foreach (RuleId rule in RuleId.Registry.All)
    {
        Assert.InRange(rule.Index, 1, 127);
        Assert.True(seen.Add(rule.Index), $"индекс {rule.Index} занят дважды: {rule.Name}");
    }

    Assert.Equal(RuleId.Registry.All.Length, seen.Count);
}

[Fact]
public void RegistryNamesAreUnique()
{
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (RuleId rule in RuleId.Registry.All)
    {
        Assert.True(seen.Add(rule.Name), $"имя {rule.Name} занято дважды");
    }
}
```

- [ ] **Step 2: Прогнать тесты**

Run: `dotnet test`
Expected: PASS — сейчас реестр корректен. Тест страхует планы 2b и 2c, где реестр вырастет с 14 записей до 107.

- [ ] **Step 3: Коммит**

```bash
rtk git add -A && rtk git commit -F - <<'EOF'
test: реестр правил защищён от дубля индекса и выхода за маску

Индекс правила — позиция бита в маске RuleSet. Дубликат склеил бы два
правила в одно, индекс за 127 включал бы чужое. Реестр растёт с 14 до 107
записей в следующих планах, руками этого никто не заметит.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
EOF
```

---

### Task 10: Снимок веб-сервиса Лебедева

**Files:**
- Create: `tools/oracle-inputs.txt`
- Create: `tools/oracle-snapshot.sh`
- Create: `docs/oracle/lebedev.md`

**Interfaces:**
- Consumes: SOAP-метод `ProcessText(text, entityType, useBr, useP, maxNobr)` по адресу `https://typograf.artlebedev.ru/webservices/typograf.asmx`, `SOAPAction: "http://typograf.artlebedev.ru/webservices/ProcessText"`. Проверено: WSDL отдаёт 200, `entityType=3` возвращает символы, а не сущности.
- Produces: `docs/oracle/lebedev.md` — таблица «вход | выход оракула», источник для планов 2b и 2c. Тесты его не читают: они остаются оффлайновыми.

- [ ] **Step 1: Составить список входов**

Создать `tools/oracle-inputs.txt` — по одному случаю в строке, покрывая группы правил планов 2b и 2c:

```
Он сказал: "это важно, и т. д." - и ушёл в 1941-1945 гг.
Тире - вот так. И дефис-минус.
Диапазон 1941-1945 и 10-15 штук
Минус -5 градусов и 5-й дом
Температура 25 C и угол 90 град.
Формула 3 x 4 = 12 и 2 x 2
Дробь 1/2, 3/4 и 1/4
Неравенства 5 <= 6, 7 >= 3, 8 != 9
Разряды 1000000 и 1 000 000 рублей
Копирайт (c) 2026, торговая марка (tm), знак (r)
Стрелки -> и <- в тексте
Многоточие... и ещё ....
Двойная пунктуация!! и ?? и ?!
Вопрос?.. и восклицание!..
"Вложенные "кавычки" внутри" и 'одинарные'
Апостроф д'Артаньян и 5" дюймов
Инициалы А. С. Пушкин и Пушкин А. С.
Сокращения т. д., т. п., т. е., н. э.
Господин г-н Иванов и т-щ Петров
Улица ул. Ленина, д. 5, кв. 12
Город г. Москва и с. Никольское
Номер № 5 и параграф § 3
Единицы 10 кг, 5 м, 100 км/ч, 3 л
Деньги 100 руб., 50 долл., 5 000 р.
Дата 2026-09-09 и 09.09.2026
Понедельник, 9 сентября 2026 года
Время 10:30 и 10 ч. 30 мин.
Телефон +7 (999) 123-45-67
Сайт www.example.com и почта mail@example.com
Проценты 50 % и 100%
Слово в начале и предлог в конце строки
Повтор повтор слова
Ударение а́ и ё вместо е
Кавычки внутри "тега <b>жирный</b> текста"
Абзац первый

Абзац второй
```

- [ ] **Step 2: Написать скрипт снимка**

Создать `tools/oracle-snapshot.sh`:

```bash
#!/usr/bin/env bash
# Разовый снимок веб-сервиса Лебедева: вход -> выход оракула.
# Запускать вручную, результат коммитится. Тесты сеть не трогают.
#
# entityType=3 — символы, а не сущности (проверено пробным запросом).
set -euo pipefail

ENDPOINT="https://typograf.artlebedev.ru/webservices/typograf.asmx"
ACTION="http://typograf.artlebedev.ru/webservices/ProcessText"
INPUTS="$(dirname "$0")/oracle-inputs.txt"
OUT="$(dirname "$0")/../docs/oracle/lebedev.md"

escape() { printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'; }

mkdir -p "$(dirname "$OUT")"
{
    echo "# Снимок веб-сервиса Лебедева"
    echo
    echo "Снят скриптом \`tools/oracle-snapshot.sh\` для планов 2b и 2c."
    echo "Настройки запроса: entityType=3 (символы), useBr=false, useP=false, maxNobr=0."
    echo "Неразрывный пробел показан как \`_\`, длинное тире как \`—\`."
    echo
    echo "| Вход | Выход оракула |"
    echo "|---|---|"
} > "$OUT"

while IFS= read -r line; do
    [ -z "$line" ] && continue
    body="<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><ProcessText xmlns=\"http://typograf.artlebedev.ru/webservices/\"><text>$(escape "$line")</text><entityType>3</entityType><useBr>false</useBr><useP>false</useP><maxNobr>0</maxNobr></ProcessText></soap:Body></soap:Envelope>"

    response=$(curl -sS -m 30 -X POST \
        -H 'Content-Type: text/xml; charset=utf-8' \
        -H "SOAPAction: \"$ACTION\"" \
        --data-binary "$body" "$ENDPOINT")

    result=$(printf '%s' "$response" \
        | tr '\n' '\x01' \
        | sed -e 's/.*<ProcessTextResponse[^>]*>//' -e 's|</ProcessTextResponse>.*||' \
        | tr '\x01' '\n' \
        | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' \
        | sed -e 's/\xc2\xa0/_/g' -e 's/|/\\|/g')

    printf '| %s | %s |\n' "$(printf '%s' "$line" | sed -e 's/|/\\|/g')" "$result" >> "$OUT"
    sleep 1   # сервис чужой: один запрос в секунду, не чаще
done < "$INPUTS"

echo "Снимок записан в $OUT"
```

- [ ] **Step 3: Прогнать скрипт**

Run: `bash tools/oracle-snapshot.sh`
Expected: `docs/oracle/lebedev.md` со всеми строками входа и непустым выходом в каждой.

Если сервис недоступен или отвечает ошибкой — не выдумывать содержимое файла. Записать в `docs/oracle/lebedev.md` дату и текст ошибки, сообщить об этом, и планы 2b/2c писать по приоритету источников из спецификации: ГОСТ Р 7.0.110-2025 ⇒ Мильчин ⇒ практика Лебедева по документации ⇒ JS-typograf.

- [ ] **Step 4: Проверить снимок глазами**

Прочитать `docs/oracle/lebedev.md` целиком. Отметить в конце файла разделом «Расхождения с нашим Default» случаи, где оракул ведёт себя не так, как текущая реализация. Пробный запрос уже показал один такой: `1941-1945` оракул оставляет с дефисом, а наше правило `ru/dash/years` ставит длинное тире — решение по нему принимает план 2b, здесь только фиксируется факт.

- [ ] **Step 5: Коммит**

```bash
rtk git add -A && rtk git commit -F - <<'EOF'
docs: снимок веб-сервиса Лебедева для планов 2b и 2c

Тридцать с лишним входов по группам будущих правил прогнаны через
typograf.artlebedev.ru один раз, ответы лежат в docs/oracle/lebedev.md.
Правила пишутся глядя на реальное поведение оракула, а не на догадку о
нём; тесты при этом остаются оффлайновыми.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
EOF
```

---

## Приёмка плана

- [ ] `dotnet test` — все тесты зелёные на всех целевых платформах.
- [ ] `rtk dotnet build -c Release` — без предупреждений (`TreatWarningsAsErrors` включён).
- [ ] Фаза `Bind` видит слово, разорванное строчным тегом, как одно слово; блочный тег и защищённая зона слово завершают.
- [ ] В `HtmlTypograf.Run` три буфера на документ и ни одного на сегмент.
- [ ] Правила фазы `Scan` лежат в `Internal/Scan/*` и вызываются из `switch` по символу-триггеру.
- [ ] `docs/oracle/lebedev.md` в репозитории, расхождения с текущим `Default` перечислены в конце файла.
- [ ] Ветка `feature/rules-infra` влита в `develop` и удалена.

## Что этот план передаёт планам 2b и 2c

| Пункт | Что появилось |
|---|---|
| Сигнатура правила фазы Scan | `TryApply(source, index, previous, rules, ref state, ref buffer)`, `true` — символ обработан |
| Диспетчер | `switch` по символу-триггеру в `TextScanner.Run`; новое правило добавляется в свою группу и в ветку своего символа |
| Состояние фазы Bind | `BindState` живёт сквозь сегменты; словарные правила получают слово целиком |
| Проверка включённости | `RuleSet.Contains` — один бит-тест, флаги перед циклом поднимать не нужно |
| Реестр | тест ловит дубль индекса и выход за 127 бит |
| Эталон | `docs/oracle/lebedev.md` |

Остаётся открытым и планом 2a не закрывается: состояние фазы `Bind` не переживает границу ДОКУМЕНТА (это и не нужно), словари многобуквенных сокращений (`FrozenSet` + `AlternateLookup` на `net8.0`, `HashSet` на `netstandard2.0`) появятся вместе с правилами плана 2c, расхождения пресетов `Lebedev`/`Gost`/`Typograf` — там же.
