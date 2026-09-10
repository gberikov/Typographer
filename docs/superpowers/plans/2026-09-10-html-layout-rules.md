# План 2d: правила фаз Layout и Emit

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Реализовать последние одиннадцать правил — группы `common/html/*` и `ru/optalign/*`, — доведя реестр с 96 записей до 107 и закрыв состав правил спецификации целиком.

**Architecture:** Правила этих групп СОЗДАЮТ разметку из текста (ссылки, висячая пунктуация, переносы, абзацы) или преобразуют её целиком (экранирование). Поэтому все они, кроме `common/html/quot`, выключены в `Default` — гарантия 4 обещает, что правило `Default` тега из текста не делает. Те, что вставляют теги внутрь текстового узла, живут одним новым проходом фазы `Layout`; экранирование — в фазе `Emit`; декодирование `&quot;` — в фазе `Prepare`. Проход не запускается, пока ни одно из его правил не включено, поэтому обычный вызов за них не платит.

**Tech Stack:** C# 13, .NET 10 SDK; целевые платформы `netstandard2.0`, `net8.0`, `net10.0`; xunit.v3 поверх Microsoft.Testing.Platform.

**Spec:** `docs/spec.md`, раздел 6 «Состав правил». Эталон поведения — `docs/oracle/lebedev.md`. Соглашения фаз, на которые план ссылается, — планы `2026-09-09-scan-rules.md` (фаза Scan) и `2026-09-10-bind-rules.md` (фаза Bind).

## Global Constraints

- Целевые платформы ядра: `netstandard2.0;net8.0;net10.0`. Внешних зависимостей нет; на `netstandard2.0` допускаются только `System.Memory` и `PolySharp`, уже подключённые.
- Никаких регулярных выражений. `System.Text.RegularExpressions` в `src/Typographer` не используется.
- `TreatWarningsAsErrors` включён, `GenerateDocumentationFile` включён. Каждый публичный член — с XML-комментарием **на русском языке**.
- Ноль аллокаций на пути `Process(ReadOnlySpan<char>, IBufferWriter<char>)` после прогрева пула. `CharBuffer` — изменяемая структура, только по `ref`, не боксируется, не захватывается замыканием.
- Правила этого плана работают ТОЛЬКО в HTML-режиме (`HtmlTypograf`). Группа называется `common/html/*`, и в обычном тексте угловая скобка — литерал; `TextTypograf` эти правила молча не применяет. Висячая пунктуация тоже требует разметки и CSS.
- Приоритет источников при любом сомнении: ГОСТ Р 7.0.110-2025 ⇒ Мильчин ⇒ практика студии Лебедева ⇒ JS-typograf. Имена правил и имена CSS-классов висячей пунктуации совпадают с JS-typograf дословно.
- Ветка `feature/html-layout-rules` от `develop`. Прямой коммит в `master` и `develop` запрещён.
- Каждый коммит заканчивается строками:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
  ```
- Тесты гоняются командой `dotnet test` БЕЗ префикса `rtk` (под Microsoft.Testing.Platform фильтр rtk врёт «0 tests, exit code 5»). Остальные команды — с `rtk`.

---

## Что этот план НЕ делает

- **`common/html/stripTags` не реализуется.** Правило удаляет все теги. Это не типографика, а санитайзинг, для которого есть отдельные библиотеки; гарантия 3 обещает разметку байт в байт, и правило противоречит ей по своей сути. Регистрируется с честным XML-комментарием — так же, как `common/punctuation/quoteLink`.
- **`common/html/processingAttrs` не реализуется.** Правило типографирует значения атрибутов. Гарантия 3 обещает атрибуты байт в байт, а реализация требует разбора значений внутри тега и повторного запуска всего конвейера на каждом из них. Цена несоразмерна: правило выключено по умолчанию и у JS-typograf.
- Правила `ru/typo/switchingKeyboardLayout` этот план не касается: оно зарегистрировано планом 1 и по-прежнему не действует.
- Долги, перечисленные в отчёте по плану 2c (ревью веток 2b и 2c, тринадцать находок ревью 2a, неработающий CI, неизмеренная производительность), закрываются не здесь.

## Решения, принятые до начала работы

1. **`common/html/nbr` и `common/html/p` — это `HtmlOptions.UseBr` и `HtmlOptions.UseP`, выраженные правилом.** Заводить для них отдельный механизм незачем: поведение уже написано и оттестировано, а два способа сказать одно и то же разъедутся. Правило и опция складываются по «или»: включено любое — тег ставится. Оговорка про идемпотентность, записанная на опциях, распространяется и на правило `nbr` (см. решение 4).

2. **`common/html/escape` реализуется, `stripTags` — нет.** Экранирование названо в разделе 6 спецификации прямо («сущности, `br`, `p`, автоссылки, экранирование») и представляет собой честное преобразование: документ выходит текстом, из которого исходный документ восстанавливается однозначно. Удаление тегов такого свойства не имеет — оно теряет информацию, и его в спецификации нет. Гарантия 3 получает ОДНО исключение (`escape`), а не два.

3. **Автоссылки не трогают то, что уже внутри `<a>`.** Правила `url` и `e-mail` пропускают текстовые узлы, лежащие внутри элемента ссылки. Без этого второй прогон вкладывал бы ссылку в ссылку, а разметка входа вида `<a href="#">http://example.com</a>` ломалась бы на первом же.

4. **Идемпотентность: два новых исключения к гарантии 5.** `common/html/nbr` — ровно то же, что `UseBr`: прогон по собственному выводу вкладывает `<br />` повторно. `common/html/escape` неподвижной точки не имеет по определению: амперсанд, ставший `&amp;`, на втором прогоне станет `&amp;amp;`. Оба вне `Default`, оба описываются в спецификации рядом с уже записанными исключениями (`UseBr`, `MaxNobr`, `replaceNbsp`). Правила `url`, `e-mail`, `p` и висячая пунктуация идемпотентны и остаются под гарантией.

5. **Имена CSS-классов висячей пунктуации берутся у JS-typograf дословно** — `typograf-oa-lquote`, `typograf-oa-n-lquote`, `typograf-oa-sp-lquote`, `typograf-oa-lbracket`, `typograf-oa-n-lbracket`, `typograf-oa-sp-lbracket`, `typograf-oa-comma`, `typograf-oa-comma-sp`. Правило само по себе ничего не выравнивает: выравнивание делает CSS на стороне сайта, и чужие стили должны подходить к нашему выводу без переписывания.

---

## Формат этого плана

Правил одиннадцать, из них два не реализуются. Носитель требований — **таблица правила**:
вход, выход, источник, участие в `Default`. Каждая строка прямо переводится в строку
`[InlineData]`. Прозой описано только спорное.

Полностью выписанные образцы — **Task 2** (маленькое правило целиком) и **Task 4** (новый
проход фазы `Layout` с нуля). Остальные задачи следуют им.

## Структура файлов

| Файл | Что с ним происходит |
|---|---|
| `src/Typographer/Rules/RuleId.cs` | реестр растёт с 96 записей до 107: индексы 97–107 |
| `src/Typographer/Rules/RuleSet.cs` | маска фазы `Layout`, массив правил, меняющих разметку |
| `src/Typographer/Internal/Preparer.cs` | + декодирование `&quot;` под правилом |
| `src/Typographer/Internal/Layout/InlineMarkupWriter.cs` | новый: проход, вставляющий теги внутрь текстовых узлов |
| `src/Typographer/Internal/Layout/LinkRules.cs` | новый: автоссылки для адресов и почты |
| `src/Typographer/Internal/Layout/OptAlignRules.cs` | новый: висячая пунктуация |
| `src/Typographer/Internal/LayoutWriter.cs` | вызов нового прохода, `nbr` и `p` рядом с опциями |
| `src/Typographer/Internal/Emitter.cs` | + экранирование документа |
| `src/Typographer/HtmlTypograf.cs` | передача правил в фазу `Emit` |
| `tests/Typographer.Tests/Rules/*` | по файлу тестов на группу правил |
| `docs/spec.md` | раздел 6 и гарантии 3 и 5 |

Порядок задач: реестр (1), фаза `Prepare` (2), автоссылки (3–4), висячая пунктуация (5),
`nbr` и `p` (6), экранирование (7), спецификация и приёмка (8).

---

### Task 1: Реестр на 107 правил

**Files:**
- Modify: `src/Typographer/Rules/RuleId.cs`
- Modify: `src/Typographer/Rules/RuleSet.cs`
- Test: `tests/Typographer.Tests/Rules/RuleSetTests.cs`

**Interfaces:**
- Consumes: `RuleId(int index, string name, RulePhase phase)`, `RuleId.Registry.All/Unsafe/Normalization/OptIn`, `RuleSet.None/All/Default/With/Without/Overlaps`, `RuleSet.FromPhase(RulePhase)`.
- Produces: 11 новых `RuleId` с индексами 97–107; классы `RuleId.Common.Html` и `RuleId.Ru.OptAlign`; `RuleId.Registry.MarkupChanging` — массив правил, которые создают или преобразуют разметку; `internal static RuleSet RuleSet.LayoutPhase`.

| Имя | Фаза | В `Default` | Задача |
|---|---|---|---|
| `common/html/quot` | Prepare | да | 2 |
| `common/html/url` | Layout | **нет** | 4 |
| `common/html/e-mail` | Layout | **нет** | 4 |
| `ru/optalign/quote` | Layout | **нет** | 5 |
| `ru/optalign/bracket` | Layout | **нет** | 5 |
| `ru/optalign/comma` | Layout | **нет** | 5 |
| `common/html/nbr` | Layout | **нет** | 6 |
| `common/html/p` | Layout | **нет** | 6 |
| `common/html/escape` | Emit | **нет** | 7 |
| `common/html/stripTags` | Emit | **не реализуется** | 1 |
| `common/html/processingAttrs` | Protect | **не реализуется** | 1 |

Десять из одиннадцати уходят в `Registry.OptIn` — все, кроме `quot`. Девять из них (всё,
кроме `quot` и `processingAttrs`) уходят ещё и в новый массив `Registry.MarkupChanging`:
на нём стоят прогоны гарантии 3, которые сравнивают ЧИСЛО тегов до и после, а эти правила
теги добавляют или снимают законно.

- [ ] **Step 1: Красный тест на состав реестра**

```csharp
[Fact]
public void RegistryHasAllHundredSevenRules()
{
    Assert.Equal(107, RuleId.Registry.All.Length);
    Assert.True(RuleId.TryParse("common/html/quot", out _));
    Assert.True(RuleId.TryParse("common/html/e-mail", out _));
    Assert.True(RuleId.TryParse("ru/optalign/quote", out _));
}

[Fact]
public void HtmlRulesAreOutOfDefaultExceptQuot()
{
    Assert.True(RuleSet.Default.Contains(RuleId.Common.Html.Quot));
    Assert.False(RuleSet.Default.Contains(RuleId.Common.Html.Url));
    Assert.False(RuleSet.Default.Contains(RuleId.Common.Html.EMail));
    Assert.False(RuleSet.Default.Contains(RuleId.Common.Html.Nbr));
    Assert.False(RuleSet.Default.Contains(RuleId.Common.Html.P));
    Assert.False(RuleSet.Default.Contains(RuleId.Common.Html.Escape));
    Assert.False(RuleSet.Default.Contains(RuleId.Ru.OptAlign.Quote));
    Assert.False(RuleSet.Default.Contains(RuleId.Ru.OptAlign.Bracket));
    Assert.False(RuleSet.Default.Contains(RuleId.Ru.OptAlign.Comma));
}

[Fact]
public void MarkupChangingRulesAreAllOutOfDefault()
{
    // Гарантия 4: ни одно правило Default не делает тега из текста.
    foreach (RuleId rule in RuleId.Registry.MarkupChanging)
    {
        Assert.False(RuleSet.Default.Contains(rule), rule.Name);
        Assert.True(RuleSet.All.Contains(rule), rule.Name);
    }
}
```

- [ ] **Step 2: Прогнать, убедиться, что не компилируется**

Run: `dotnet test tests/Typographer.Tests --framework net10.0`
Expected: ошибки компиляции — `RuleId.Common.Html` и `RuleId.Ru.OptAlign` не существуют.

- [ ] **Step 3: Реестр**

Одиннадцать записей в `RuleId.Registry` с индексами 97–107, имена из таблицы. Новые классы
`Common.Html` и `Ru.OptAlign` с XML-комментариями на русском; у двух нереализуемых правил
комментарий говорит об этом прямо и называет причину, как у `QuoteLink`. `Registry.All`
пополняется теми же одиннадцатью значениями, `Registry.OptIn` — десятью.

```csharp
// RuleId.cs, Registry
/// <summary>
/// Правила, которые создают разметку из текста или преобразуют её целиком. Все они вне
/// Default (гарантия 4), и на них не действуют прогоны гарантии 3, сравнивающие число
/// тегов до и после: эти правила теги добавляют или снимают законно.
/// </summary>
public static readonly RuleId[] MarkupChanging =
[
    HtmlUrl, HtmlEmail, HtmlNbr, HtmlP, HtmlEscape, HtmlStripTags,
    OptAlignQuote, OptAlignBracket, OptAlignComma,
];
```

- [ ] **Step 4: Маска фазы Layout**

```csharp
// RuleSet.cs, рядом с BindPhase
/// <summary>Все правила фазы Layout. Нужна самой фазе: без пересечения проход не запускается.</summary>
internal static RuleSet LayoutPhase { get; } = FromPhase(RulePhase.Layout);
```

- [ ] **Step 5: Прогнать тесты**

Run: `dotnet test tests/Typographer.Tests --framework net10.0`
Expected: PASS. Правила зарегистрированы, но ни одна фаза их не читает — вывод не меняется.

- [ ] **Step 6: Коммит**

```bash
rtk git add src/Typographer/Rules tests/Typographer.Tests/Rules/RuleSetTests.cs
rtk git commit -m "feat: реестр правил закрыт на 107 записях"
```

---

### Task 2: `common/html/quot` — кавычка из сущности

**Files:**
- Modify: `src/Typographer/Internal/Preparer.cs`
- Test: `tests/Typographer.Tests/Rules/HtmlRulesTests.cs` (создать)

**Interfaces:**
- Consumes: `Preparer.Run(ReadOnlySpan<char>, bool decodeEntities, RuleSet, ref CharBuffer, bool isDocumentStart)`, `EntityTable.TryDecode`.
- Produces: поведение фазы `Prepare` под управлением правила. Сигнатуры не меняются.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `common/html/quot` | `&quot;цитата&quot;` | `«цитата»` (вместе с правилом кавычек) | JS-typograf | да |
| `common/html/quot` | `&#34;цитата&#34;` | `«цитата»` | JS-typograf | да |
| `common/html/quot` | `&quot;текст&quot;` без правила кавычек | `"текст"` | — | да |
| `common/html/quot` выключено | `&quot;текст&quot;` | `&quot;текст&quot;` | — | — |
| `common/html/quot` | `&amp;quot;` | `&amp;quot;` (экранированный амперсанд — не сущность) | гарантия 3 | да |

Сущности разметки (`&amp;`, `&lt;`, `&gt;`, `&quot;`) в таблицу `EntityTable` намеренно не
входят: они несут смысл разметки и проходят насквозь. Правило `quot` — единственное
исключение, и оно остаётся ЛОКАЛЬНЫМ для фазы `Prepare`: добавить `quot` в таблицу нельзя,
потому что тогда фаза `Emit` начнёт КОДИРОВАТЬ каждую прямую кавычку обратно в `&quot;`, чего
не просил никто.

- [ ] **Step 1: Красный тест**

```csharp
// tests/Typographer.Tests/Rules/HtmlRulesTests.cs
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class HtmlRulesTests
{
    private static string Run(string source, params RuleId[] rules)
        => new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Fact]
    public void QuotEntityBecomesQuoteCharacter()
    {
        Assert.Equal(
            "«цитата»",
            Run("&quot;цитата&quot;", RuleId.Common.Html.Quot, RuleId.Common.Punctuation.Quote));
        Assert.Equal(
            "«цитата»",
            Run("&#34;цитата&#34;", RuleId.Common.Html.Quot, RuleId.Common.Punctuation.Quote));
        Assert.Equal("\"текст\"", Run("&quot;текст&quot;", RuleId.Common.Html.Quot));
    }

    [Fact]
    public void QuotEntityStaysWithoutTheRule()
        => Assert.Equal(
            "&quot;текст&quot;",
            Run("&quot;текст&quot;", RuleId.Common.Punctuation.Quote));

    [Fact]
    public void EscapedAmpersandIsNotAnEntity()
        => Assert.Equal(
            "&amp;quot;",
            Run("&amp;quot;", RuleId.Common.Html.Quot, RuleId.Common.Punctuation.Quote));
}
```

- [ ] **Step 2: Прогнать, убедиться, что падает**

Run: `dotnet test tests/Typographer.Tests --framework net10.0`
Expected: FAIL — `&quot;` доходит до вывода без изменений.

- [ ] **Step 3: Реализация**

```csharp
// Preparer.Run, рядом с веткой EntityTable.TryDecode
// Сущность прямой кавычки декодируется отдельным правилом и только здесь. В таблицу
// типографских сущностей она не входит намеренно: иначе фаза Emit начала бы кодировать
// обратно каждую прямую кавычку в тексте.
if (decodeEntities && c == '&' && quot && TryDecodeQuot(source.Slice(i), out int quotLength))
{
    buffer.Write('"');
    i += quotLength - 1;
    continue;
}
```

```csharp
/// <summary>Сущность прямой кавычки: «&amp;quot;», «&amp;#34;» и «&amp;#x22;».</summary>
private static bool TryDecodeQuot(ReadOnlySpan<char> source, out int length)
{
    ReadOnlySpan<char> quot = "&quot;".AsSpan();
    if (source.StartsWith(quot, StringComparison.Ordinal))
    {
        length = quot.Length;
        return true;
    }

    if (source.StartsWith("&#".AsSpan(), StringComparison.Ordinal))
    {
        int end = source.Slice(0, Math.Min(source.Length, 8)).IndexOf(';');
        if (end > 2)
        {
            ReadOnlySpan<char> digits = source.Slice(2, end - 2);
            bool decimalForm = digits.SequenceEqual("34".AsSpan());
            bool hexForm = digits.SequenceEqual("x22".AsSpan()) || digits.SequenceEqual("X22".AsSpan());
            if (decimalForm || hexForm)
            {
                length = end + 1;
                return true;
            }
        }
    }

    length = 0;
    return false;
}
```

`bool quot = rules.Contains(RuleId.Common.Html.Quot);` вычисляется один раз перед циклом,
рядом с уже существующим `replaceNbsp`.

- [ ] **Step 4: Прогнать тесты**

Run: `dotnet test tests/Typographer.Tests --framework net10.0`
Expected: PASS.

- [ ] **Step 5: Коммит**

```bash
rtk git add src/Typographer tests/Typographer.Tests
rtk git commit -m "feat: правило common/html/quot"
```

---

### Task 3: Проход, вставляющий разметку внутрь текста

**Files:**
- Create: `src/Typographer/Internal/Layout/InlineMarkupWriter.cs`
- Modify: `src/Typographer/Internal/LayoutWriter.cs`
- Test: `tests/Typographer.Tests/Rules/InlineMarkupTests.cs` (создать)

**Interfaces:**
- Consumes: `MarkupScanner`, `Segment`, `SegmentKind`, `Tags.TryReadName`, `Tags.NameIs`, `CharBuffer`.
- Produces:
  - `internal struct InlineState` с полями `Skip` (сколько символов узла проглотило правило), `InsideLink` (разбор внутри элемента ссылки), `InsideOptAlign` (разбор внутри тега висячей пунктуации), `AtLineStart` (текущая позиция — начало строки документа);
  - `InlineMarkupWriter.IsEnabled(RuleSet rules)` — включено ли хоть одно правило прохода;
  - `InlineMarkupWriter.Run(ReadOnlySpan<char> document, RuleSet rules, ref CharBuffer buffer)`;
  - соглашение о правиле прохода: `bool TryApply(ReadOnlySpan<char> node, int index, RuleSet rules, ref InlineState state, ref CharBuffer buffer)` — правило пишет само и сообщает `state.Skip`.

Этот проход — единственное место, где правила вставляют теги ВНУТРЬ текстового узла.
Правила `nbr` и `p` сюда не входят: они работают по документу целиком и уже написаны.

Проход стоит одного буфера на документ и одного прохода по нему, поэтому запускается только
когда включено хоть одно его правило — все пять вне `Default`, и обычный вызов за них не
платит. Тот же приём, что у `DocumentSpaceRules.IsEnabled`.

- [ ] **Step 1: Красный тест на каркас прохода**

```csharp
// tests/Typographer.Tests/Rules/InlineMarkupTests.cs
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class InlineMarkupTests
{
    private static string Run(string source, params RuleId[] rules)
        => new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    // Проход обязан вернуть документ байт в байт, если ни одно правило не сработало,
    // и не запускаться вовсе, когда его правила выключены.
    [Fact]
    public void PassCopiesEverythingItDoesNotChange()
    {
        const string source = "<p title=\"a - b\">раз<!-- к --><code>x  y</code>два</p>";
        Assert.Equal(source, Run(source, RuleId.Common.Html.Url));
        Assert.Equal(source, Run(source, RuleId.Common.Punctuation.Quote));
    }
}
```

- [ ] **Step 2: Прогнать** — Expected: PASS вырожденно (правило ещё ничего не делает).
      Зафиксировать вывод: тест закрепляет то, что не должно сломаться в задачах 4 и 5.

- [ ] **Step 3: Состояние и проход**

```csharp
// src/Typographer/Internal/Layout/InlineMarkupWriter.cs
/// <summary>Состояние прохода, вставляющего разметку внутрь текстовых узлов.</summary>
internal struct InlineState
{
    /// <summary>Сколько ДОПОЛНИТЕЛЬНЫХ символов узла проглотило сработавшее правило.</summary>
    public int Skip;

    /// <summary>Разбор идёт внутри элемента ссылки: вкладывать в него ещё одну нельзя.</summary>
    public bool InsideLink;

    /// <summary>
    /// Разбор идёт внутри тега, поставленного правилом висячей пунктуации на прошлом
    /// прогоне. Повторно оборачивать тот же символ нельзя — иначе теги вкладываются друг в
    /// друга, и правило теряет идемпотентность.
    /// </summary>
    public bool InsideOptAlign;

    /// <summary>Текущая позиция — начало строки документа.</summary>
    public bool AtLineStart;
}

/// <summary>
/// Проход фазы Layout, вставляющий разметку ВНУТРЬ текстовых узлов: автоссылки и висячая
/// пунктуация.
/// </summary>
/// <remarks>
/// Соглашение о правиле прохода: <c>TryApply(node, index, rules, ref state, ref buffer)</c>,
/// <c>true</c> означает «символ обработан и записан», <c>false</c> — «символ пишет
/// диспетчер». Правило, съевшее больше одного символа, сообщает об этом через
/// <see cref="InlineState.Skip"/>.
/// Правила прохода СОЗДАЮТ разметку из текста, поэтому все они вне
/// <see cref="RuleSet.Default"/> (гарантия 4), а сам проход не запускается, пока ни одно из
/// них не включено, — и не стоит ни буфера, ни копии документа.
/// </remarks>
internal static class InlineMarkupWriter
{
    /// <summary>Хоть одно правило прохода включено.</summary>
    public static bool IsEnabled(RuleSet rules)
        => rules.Contains(RuleId.Common.Html.Url)
           || rules.Contains(RuleId.Common.Html.EMail)
           || rules.Contains(RuleId.Ru.OptAlign.Quote)
           || rules.Contains(RuleId.Ru.OptAlign.Bracket)
           || rules.Contains(RuleId.Ru.OptAlign.Comma);

    /// <summary>Проход по документу.</summary>
    /// <param name="document">Документ после фаз Scan и Bind.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="buffer">Приёмник.</param>
    public static void Run(ReadOnlySpan<char> document, RuleSet rules, ref CharBuffer buffer)
    {
        var scanner = new MarkupScanner(document);
        var state = new InlineState { AtLineStart = true };

        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = document.Slice(segment.Start, segment.Length);

            if (segment.Kind != SegmentKind.Text)
            {
                if (segment.Kind == SegmentKind.Markup)
                {
                    UpdateElementFlags(slice, ref state);
                    state.AtLineStart |= segment.IsBlock;
                }

                buffer.Write(slice);
                continue;
            }

            WriteNode(slice, rules, ref state, ref buffer);
        }
    }

    private static void WriteNode(
        ReadOnlySpan<char> node, RuleSet rules, ref InlineState state, ref CharBuffer buffer)
    {
        for (int i = 0; i < node.Length; i++)
        {
            if (LinkRules.TryApply(node, i, rules, ref state, ref buffer)
                || OptAlignRules.TryApply(node, i, rules, ref state, ref buffer))
            {
                i += state.Skip;
                state.Skip = 0;
                state.AtLineStart = false;
                continue;
            }

            char c = node[i];
            buffer.Write(c);
            state.AtLineStart = c == '\n';
        }
    }

    /// <summary>
    /// Отмечает вход в элемент ссылки и в тег висячей пунктуации. Закрывающий тег снимает
    /// оба признака: вложенных ссылок в валидном HTML не бывает, а тег висячей пунктуации
    /// содержит ровно один символ и вложить в себя ничего не может.
    /// </summary>
    private static void UpdateElementFlags(ReadOnlySpan<char> tag, ref InlineState state)
    {
        if (!Tags.TryReadName(tag, out ReadOnlySpan<char> name, out bool closing))
        {
            return;
        }

        if (Tags.NameIs(name, "a"))
        {
            state.InsideLink = !closing;
            return;
        }

        if (Tags.NameIs(name, "span"))
        {
            state.InsideOptAlign = !closing && OptAlignRules.IsOptAlignTag(tag);
        }
    }
}
```

- [ ] **Step 4: Заглушки правил**

`LinkRules.TryApply` и `OptAlignRules.TryApply` создаются возвращающими `false`, с
XML-комментарием «правила добавляют задачи 4 и 5». `OptAlignRules.IsOptAlignTag` возвращает
`false`. Заглушка честнее условной компиляции: диспетчер уже вызывает правила в нужном месте
и в нужном порядке.

- [ ] **Step 5: Вызов из фазы Layout**

```csharp
// LayoutWriter.Run, в самом начале
// Вставка разметки внутрь текстовых узлов идёт ПЕРВОЙ: неразрывные цепочки и абзацы
// считаются уже по документу с этими тегами. Ссылка пробелов не содержит, поэтому
// цепочки от неё не страдают, а порядок «сперва вставили, потом сгруппировали» повторяет
// порядок фаз и не требует второго разбора.
if (InlineMarkupWriter.IsEnabled(options.Rules))
{
    var inlined = new CharBuffer(source.Length + (source.Length >> 2));
    try
    {
        InlineMarkupWriter.Run(source, options.Rules, ref inlined);
        RunLayout(inlined.AsSpan(), options, canWrapParagraphs, ref buffer);
    }
    finally
    {
        inlined.Dispose();
    }

    return;
}

RunLayout(source, options, canWrapParagraphs, ref buffer);
```

Тело нынешнего `Run` переименовывается в `RunLayout` с той же сигнатурой; больше в нём
ничего не меняется.

- [ ] **Step 6: Прогнать все тесты**

Run: `dotnet test tests/Typographer.Tests`
Expected: PASS, ни один существующий тест не изменил поведения.

- [ ] **Step 7: Коммит**

```bash
rtk git add src/Typographer tests/Typographer.Tests
rtk git commit -m "feat: проход фазы Layout для разметки внутри текстовых узлов"
```

---

### Task 4: Автоссылки

**Files:**
- Modify: `src/Typographer/Internal/Layout/LinkRules.cs`
- Test: `tests/Typographer.Tests/Rules/InlineMarkupTests.cs`

**Interfaces:**
- Consumes: `InlineState`, соглашение о правиле прохода из задачи 3.
- Produces: рабочая реализация `LinkRules.TryApply(ReadOnlySpan<char> node, int index, RuleSet rules, ref InlineState state, ref CharBuffer buffer)`.

| Правило | Вход | Выход | В `Default` |
|---|---|---|---|
| `common/html/url` | `http://example.com` | `<a href="http://example.com">http://example.com</a>` | **нет** |
| `common/html/url` | `https://a.ru/b?x=1&y=2` | `<a href="https://a.ru/b?x=1&amp;y=2">https://a.ru/b?x=1&y=2</a>` | **нет** |
| `common/html/url` | `Сайт http://a.ru.` | `Сайт <a href="http://a.ru">http://a.ru</a>.` | **нет** |
| `common/html/url` | `<a href="#">http://a.ru</a>` | без изменений (уже внутри ссылки) | **нет** |
| `common/html/url` | `текст без адреса` | без изменений | **нет** |
| `common/html/e-mail` | `mail@example.com` | `<a href="mailto:mail@example.com">mail@example.com</a>` | **нет** |
| `common/html/e-mail` | `Пишите на mail@example.com.` | `Пишите на <a href="mailto:mail@example.com">mail@example.com</a>.` | **нет** |
| `common/html/e-mail` | `@ и собака` | без изменений | **нет** |
| `common/html/e-mail` | `mail@example` | без изменений (домен без точки) | **нет** |
| `common/html/e-mail` | `<a href="#">m@e.com</a>` | без изменений | **нет** |

Ссылка обрывается на пробеле, угловой скобке и кавычке; хвостовые `.`, `,`, `;`, `:`, `!`,
`?` и `)` в неё не входят — точка в конце предложения к адресу не относится. Значение
атрибута `href` экранируется (`&` в `&amp;`, `"` в `&quot;`), текст ссылки остаётся как был:
это два разных контекста, и путать их нельзя.

Почта распознаётся не по «собаке», а с НАЧАЛА адреса: правило спрашивается на первом символе
слова, читает вперёд и проверяет форму целиком. Иначе локальная часть уже была бы записана в
буфер, и правилу пришлось бы её оттуда выковыривать.

- [ ] **Step 1: Красный тест**

```csharp
    [Theory]
    [InlineData("http://example.com", "<a href=\"http://example.com\">http://example.com</a>")]
    [InlineData(
        "https://a.ru/b?x=1&y=2",
        "<a href=\"https://a.ru/b?x=1&amp;y=2\">https://a.ru/b?x=1&y=2</a>")]
    [InlineData("Сайт http://a.ru.", "Сайт <a href=\"http://a.ru\">http://a.ru</a>.")]
    [InlineData("<a href=\"#\">http://a.ru</a>", "<a href=\"#\">http://a.ru</a>")]
    [InlineData("текст без адреса", "текст без адреса")]
    public void UrlBecomesLink(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Html.Url));

    [Theory]
    [InlineData("mail@example.com", "<a href=\"mailto:mail@example.com\">mail@example.com</a>")]
    [InlineData(
        "Пишите на mail@example.com.",
        "Пишите на <a href=\"mailto:mail@example.com\">mail@example.com</a>.")]
    [InlineData("@ и собака", "@ и собака")]
    [InlineData("mail@example", "mail@example")]
    [InlineData("<a href=\"#\">m@e.com</a>", "<a href=\"#\">m@e.com</a>")]
    public void EmailBecomesLink(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Html.EMail));

    // Прогон по собственному выводу вкладывал бы ссылку в ссылку.
    [Fact]
    public void LinkRulesAreIdempotent()
    {
        string once = Run("Сайт http://a.ru и почта m@e.com", RuleId.Common.Html.Url, RuleId.Common.Html.EMail);
        Assert.Equal(once, Run(once, RuleId.Common.Html.Url, RuleId.Common.Html.EMail));
    }
```

- [ ] **Step 2: Прогнать** — Expected: FAIL на всех положительных случаях.

- [ ] **Step 3: Реализация**

```csharp
// src/Typographer/Internal/Layout/LinkRules.cs
/// <summary>Автоссылки: веб-адрес и электронная почта становятся элементом ссылки.</summary>
/// <remarks>
/// Оба правила вне <see cref="RuleSet.Default"/>: они делают тег из текста, а гарантия 4
/// обещает, что ни одно правило <see cref="RuleSet.Default"/> так не поступает.
/// Внутрь уже открытого элемента ссылки правила не суются — вложенная ссылка невалидна, и на
/// этом же держится их идемпотентность.
/// </remarks>
internal static class LinkRules
{
    public static bool TryApply(
        ReadOnlySpan<char> node, int index, RuleSet rules, ref InlineState state, ref CharBuffer buffer)
    {
        if (state.InsideLink)
        {
            return false;
        }

        if (rules.Contains(RuleId.Common.Html.Url) && TryUrl(node, index, ref state, ref buffer))
        {
            return true;
        }

        return rules.Contains(RuleId.Common.Html.EMail) && TryEmail(node, index, ref state, ref buffer);
    }

    private static bool TryUrl(
        ReadOnlySpan<char> node, int index, ref InlineState state, ref CharBuffer buffer)
    {
        ReadOnlySpan<char> rest = node.Slice(index);
        if (!rest.StartsWith("http://".AsSpan(), StringComparison.OrdinalIgnoreCase)
            && !rest.StartsWith("https://".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int length = 0;
        while (length < rest.Length && !IsUrlBoundary(rest[length]))
        {
            length++;
        }

        length = TrimTrailingPunctuation(rest, length);
        if (length <= "https://".Length)
        {
            return false;
        }

        WriteLink(rest.Slice(0, length), default, ref buffer);
        state.Skip = length - 1;
        return true;
    }

    private static bool TryEmail(
        ReadOnlySpan<char> node, int index, ref InlineState state, ref CharBuffer buffer)
    {
        // Правило спрашивается на КАЖДОМ символе, поэтому первым делом отсекается всё, что
        // адресом заведомо не начинается: середина слова и небуквенный символ.
        if (!IsLocalChar(node[index]) || (index > 0 && IsLocalChar(node[index - 1])))
        {
            return false;
        }

        ReadOnlySpan<char> rest = node.Slice(index);
        int length = 0;
        while (length < rest.Length && (IsLocalChar(rest[length]) || rest[length] == '@'))
        {
            length++;
        }

        length = TrimTrailingPunctuation(rest, length);
        ReadOnlySpan<char> address = rest.Slice(0, length);
        if (!IsEmail(address))
        {
            return false;
        }

        WriteLink(address, "mailto:".AsSpan(), ref buffer);
        state.Skip = length - 1;
        return true;
    }

    /// <summary>Адрес: одна «собака», непустая локальная часть и домен с точкой и зоной.</summary>
    private static bool IsEmail(ReadOnlySpan<char> address)
    {
        int at = address.IndexOf('@');
        if (at <= 0 || at == address.Length - 1)
        {
            return false;
        }

        ReadOnlySpan<char> domain = address.Slice(at + 1);
        if (domain.IndexOf('@') >= 0)
        {
            return false;
        }

        int dot = domain.LastIndexOf('.');
        if (dot <= 0 || domain.Length - dot - 1 < 2)
        {
            return false;
        }

        for (int i = dot + 1; i < domain.Length; i++)
        {
            if (!char.IsLetter(domain[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Пишет элемент ссылки. Значение атрибута экранируется, текст ссылки — нет: это два
    /// разных контекста, и амперсанд в них означает разное.
    /// </summary>
    private static void WriteLink(ReadOnlySpan<char> target, ReadOnlySpan<char> scheme, ref CharBuffer buffer)
    {
        buffer.Write("<a href=\"");
        buffer.Write(scheme);
        foreach (char c in target)
        {
            switch (c)
            {
                case '&': buffer.Write("&amp;"); break;
                case '"': buffer.Write("&quot;"); break;
                default: buffer.Write(c); break;
            }
        }

        buffer.Write("\">");
        buffer.Write(target);
        buffer.Write("</a>");
    }

    private static bool IsUrlBoundary(char c) => char.IsWhiteSpace(c) || c is '<' or '>' or '"';

    private static bool IsLocalChar(char c)
        => char.IsLetterOrDigit(c) || c is '.' or '_' or '%' or '+' or '-';

    /// <summary>Хвостовые знаки препинания к адресу не относятся: «Сайт http://a.ru.».</summary>
    private static int TrimTrailingPunctuation(ReadOnlySpan<char> source, int length)
    {
        while (length > 0 && source[length - 1] is '.' or ',' or ';' or ':' or '!' or '?' or ')')
        {
            length--;
        }

        return length;
    }
}
```

- [ ] **Step 4: Прогнать тесты**

Run: `dotnet test tests/Typographer.Tests`
Expected: PASS. Прогон гарантии 3 (`TagCountUnchanged_AllRules`) обязан считать эти правила
законно добавляющими теги — он берёт набор `RuleSet.All.Without(RuleId.Registry.MarkupChanging)`,
и правка этого теста входит в задачу 8.

- [ ] **Step 5: Коммит**

```bash
rtk git add src/Typographer tests/Typographer.Tests
rtk git commit -m "feat: автоссылки для веб-адресов и почты"
```

---

### Task 5: Висячая пунктуация

**Files:**
- Modify: `src/Typographer/Internal/Layout/OptAlignRules.cs`
- Test: `tests/Typographer.Tests/Rules/OptAlignTests.cs` (создать)

**Interfaces:**
- Consumes: `InlineState`, соглашение о правиле прохода из задачи 3.
- Produces: рабочие `OptAlignRules.TryApply(...)` и `OptAlignRules.IsOptAlignTag(ReadOnlySpan<char> tag)`.

| Правило | Вход | Выход | В `Default` |
|---|---|---|---|
| `ru/optalign/quote` | `«цитата»` в начале строки | `<span class="typograf-oa-n-lquote">«</span>цитата»` | **нет** |
| `ru/optalign/quote` | `он сказал «да»` | `он сказал<span class="typograf-oa-sp-lquote"> </span><span class="typograf-oa-lquote">«</span>да»` | **нет** |
| `ru/optalign/quote` | `«да»` второй прогон | без изменений | **нет** |
| `ru/optalign/bracket` | `(текст)` в начале строки | `<span class="typograf-oa-n-lbracket">(</span>текст)` | **нет** |
| `ru/optalign/bracket` | `слово (текст)` | `слово<span class="typograf-oa-sp-lbracket"> </span><span class="typograf-oa-lbracket">(</span>текст)` | **нет** |
| `ru/optalign/comma` | `раз, два` | `раз<span class="typograf-oa-comma">,</span><span class="typograf-oa-comma-sp"> </span>два` | **нет** |
| `ru/optalign/comma` | `раз,два` | без изменений (за запятой нет пробела) | **нет** |
| `ru/optalign/comma` | `, два` в начале строки | без изменений (слева нет буквы) | **нет** |

Имена классов взяты у JS-typograf дословно (решение 5 шапки): правило само ничего не
выравнивает, выравнивание делает CSS на стороне сайта, и чужие стили обязаны подходить к
нашему выводу без переписывания.

Закрывающая кавычка и закрывающая скобка не оборачиваются: висит ЛЕВЫЙ край строки, и правая
граница набора к нему отношения не имеет. У JS-typograf так же.

Идемпотентность держится на признаке `InsideOptAlign`, который проход выставляет по тегу
`span` с классом правила: символ, уже обёрнутый на прошлом прогоне, второй раз не
оборачивается. Тег висячей пунктуации содержит ровно один символ, вкладывать в него нечего,
поэтому признак снимается первым же закрывающим тегом.

- [ ] **Step 1: Красный тест** — по строкам таблицы плюс отдельный тест на два прогона подряд.
- [ ] **Step 2: Прогнать** — Expected: FAIL на всех положительных случаях.
- [ ] **Step 3: Реализация.** `TryApply` спрашивает три правила по символу-триггеру:
      `Chars.Laquo` и `Chars.Bdquo` — кавычка, `(` — скобка, `,` — запятая. Для кавычки и
      скобки различаются два случая: начало строки (`state.AtLineStart` — класс `-n-`) и
      середина (пробел слева уже записан в буфер — он усекается и переписывается тегом
      `-sp-`). Запятая оборачивается, только если слева буква или цифра, а справа пробел.
- [ ] **Step 4: Прогнать** — Expected: PASS, включая тест на два прогона.
- [ ] **Step 5: Коммит** — `feat: висячая пунктуация`.

---

### Task 6: `common/html/nbr` и `common/html/p`

**Files:**
- Modify: `src/Typographer/Internal/LayoutWriter.cs`
- Test: `tests/Typographer.Tests/Rules/HtmlRulesTests.cs`

**Interfaces:**
- Consumes: `LayoutWriter.RunLayout(ReadOnlySpan<char>, HtmlOptions, bool canWrapParagraphs, ref CharBuffer)` из задачи 3, `HtmlOptions.UseBr`, `HtmlOptions.UseP`.
- Produces: правила читаются рядом с опциями; сигнатуры не меняются.

| Правило | Вход | Выход | В `Default` |
|---|---|---|---|
| `common/html/nbr` | `первая\nвторая` | `первая<br />\nвторая` | **нет** |
| `common/html/p` | `первый\n\nвторой` | `<p>первый</p>\n<p>второй</p>` | **нет** |
| `common/html/p` | `<ul><li>раз</li></ul>` | без изменений (блочная разметка уже есть) | **нет** |
| оба выключены, опции выключены | `первая\nвторая` | без изменений | — |

Правило и опция складываются по «или»: `bool useBr = options.UseBr || rules.Contains(Nbr);`
и то же для абзацев. Решение 1 шапки объясняет, почему второго механизма не заводится.

- [ ] **Step 1: Красный тест** — по строкам таблицы.
- [ ] **Step 2: Прогнать** — Expected: FAIL: правила ничего не меняют.
- [ ] **Step 3: Реализация** — две строки в `RunLayout`.
- [ ] **Step 4: Прогнать** — Expected: PASS, вместе с существующими тестами опций.
- [ ] **Step 5: Коммит** — `feat: правила common/html/nbr и common/html/p`.

---

### Task 7: `common/html/escape`

**Files:**
- Modify: `src/Typographer/Internal/Emitter.cs`
- Modify: `src/Typographer/HtmlTypograf.cs`
- Test: `tests/Typographer.Tests/Rules/HtmlRulesTests.cs`

**Interfaces:**
- Consumes: `Emitter.EncodeDocument(ReadOnlySpan<char> html, EntityMode mode, ref CharBuffer destination)`.
- Produces: `Emitter.EncodeDocument(ReadOnlySpan<char> html, EntityMode mode, RuleSet rules, ref CharBuffer destination)` — добавлен параметр набора правил; вызов в `HtmlTypograf.Run` обновляется.

| Правило | Вход | Выход | В `Default` |
|---|---|---|---|
| `common/html/escape` | `<b>жирный</b>` | `&lt;b&gt;жирный&lt;/b&gt;` | **нет** |
| `common/html/escape` | `a & b` | `a &amp; b` | **нет** |
| `common/html/escape` | `"цитата"` | `&quot;цитата&quot;` | **нет** |
| `common/html/escape` выключено | `<b>жирный</b>` | без изменений | — |

Экранирование идёт ПОСЛЕДНИМ, по готовому документу, и касается всех сегментов — тегов,
защищённых зон и текста. В этом и смысл правила: показать разметку как текст.

Гарантия 3 получает своё единственное исключение, гарантия 5 — второе новое (решение 4
шапки): амперсанд, ставший `&amp;`, на втором прогоне станет `&amp;amp;`.

- [ ] **Step 1: Красный тест** — по строкам таблицы плюс тест, фиксирующий НЕидемпотентность
      («&amp;» второго прогона), — по образцу `UseBr_SecondPassNestsBrTag`.
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: Реализация** — ветка в начале `EncodeDocument`.
- [ ] **Step 4: Прогнать** — Expected: PASS.
- [ ] **Step 5: Коммит** — `feat: правило common/html/escape`.

---

### Task 8: Спецификация, гарантии и приёмка на 107 правилах

**Files:**
- Modify: `docs/spec.md`
- Modify: `tests/Typographer.Tests/Guarantees/MarkupIntegrityTests.cs`
- Modify: `tests/Typographer.Tests/Guarantees/IdempotencyTests.cs`
- Modify: `tests/Typographer.Tests/Corpus/HardCases.cs`
- Modify: `docs/oracle/lebedev.md`

**Interfaces:**
- Consumes: все правила задач 1–7, `RuleId.Registry.MarkupChanging`.
- Produces: актуальный раздел 6 спецификации, гарантии 3 и 5 с исключениями, прогоны гарантий на наборе `All` без правил, меняющих разметку.

- [ ] **Step 1: Прогоны гарантий**

`MarkupIntegrityTests.TagCountUnchanged_AllRules` берёт
`RuleSet.All.Without(RuleId.Registry.MarkupChanging)`: тест проверяет сохранность
СУЩЕСТВУЮЩЕЙ разметки, а эти правила теги добавляют и снимают законно.
`IdempotencyTests.SecondPassChangesNothing_AllRules` дополнительно исключает
`RuleId.Common.Html.Nbr` и `RuleId.Common.Html.Escape` — они неидемпотентны по построению, и
это записано в спецификации, а не выдаётся за дефект.

- [ ] **Step 2: Корпус**

`HardCases` пополняется четырьмя строками на взаимодействие новых правил с существующими:
`"Сайт http://example.com/a-b?x=1&y=2 и почта user@example.com"` (уже есть — проверить, что
не сломался), `"«Цитата» в начале и (скобка) в середине, раз, два"`,
`"<a href=\"#\">http://a.ru</a> и m@e.com"`, `"&quot;цитата&quot; и &amp;quot;"`.

- [ ] **Step 3: Раздел 6 спецификации**

Строка `common/html/*` получает «Prepare / Layout / Emit» вместо «Protect / Layout / Emit» и
сноску про два нереализуемых правила. Абзац «Зарегистрированы, но пока НЕ РЕАЛИЗОВАНЫ»
переписывается: после этого плана нереализованных правил остаётся ТРИ, и все три —
осознанные отказы, а не отложенная работа: `common/punctuation/quoteLink`,
`common/html/stripTags`, `common/html/processingAttrs`. Правило
`ru/typo/switchingKeyboardLayout` остаётся отложенным.

- [ ] **Step 4: Гарантии 3 и 5**

Гарантия 3 получает исключение `common/html/escape` — единственное. Гарантия 5 получает
`common/html/nbr` и `common/html/escape` рядом с `UseBr`, `MaxNobr` и `replaceNbsp`.

- [ ] **Step 5: Снимок оракула**

В `docs/oracle/lebedev.md` дописывается, что закрыл план 2d: строка
`Сайт www.example.com и почта mail@example.com` — оракул ссылок не ставит и мы в `Default`
тоже, но правила теперь есть и включаются по имени. Адрес без схемы (`www.example.com`) не
распознаётся ни одним из наших правил — это факт, а не дефект.

- [ ] **Step 6: Прогнать всё**

Run: `dotnet test tests/Typographer.Tests` и `rtk dotnet build src/Typographer -c Release`
Expected: PASS, ноль предупреждений на всех трёх целевых платформах.

- [ ] **Step 7: Коммит**

```bash
rtk git add docs tests
rtk git commit -m "docs: состав правил закрыт на 107, гарантии учли новые исключения"
```

---

## Приёмка плана

- 107 правил в реестре, `RuleId.Registry.All.Length == 107`, каждое имя разбирается через `TryParse`.
- Нереализованных правил ровно три, и каждое — осознанный отказ с причиной в XML-комментарии:
  `common/punctuation/quoteLink`, `common/html/stripTags`, `common/html/processingAttrs`.
  Четвёртое, `ru/typo/switchingKeyboardLayout`, остаётся отложенным с планa 1.
- Все семь гарантий раздела 8 проверены тестами; исключения из гарантий 3 и 5 названы поимённо
  и закреплены тестами, фиксирующими РЕАЛЬНОЕ поведение.
- `RuleSet.Default` по-прежнему не создаёт разметку из текста: тест
  `MarkupChangingRulesAreAllOutOfDefault` стережёт это по массиву, а не по списку в голове.
- `docs/spec.md` и `docs/oracle/lebedev.md` соответствуют коду.
- Ни одного регулярного выражения, ни одной аллокации в горячем пути, ни одного публичного
  члена без русского XML-комментария.
