# План 2b: правила фазы Scan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Реализовать оставшиеся правила фазы `Scan` — пробелы, пунктуация, символы, числа и тире — доведя реестр с 14 зарегистрированных правил до 65 и закрыв все группы, которым хватает символьного контекста.

**Architecture:** Правила пишутся по соглашению, введённому планом 2a: `TryApply(source, index, previous, floor, rules, ref state, ref buffer)` в тематическом файле `src/Typographer/Internal/Scan/*`, вызов из `switch` по символу-триггеру в `TextScanner.Run`. Ни одно правило не заводит новых буферов и не аллоцирует. Правила, которым нужен не символ, а строка или документ целиком (обрезка краёв, повторные переводы строки), живут отдельной задачей и работают на уровне документа.

**Tech Stack:** C# 13, .NET 10 SDK; целевые платформы `netstandard2.0`, `net8.0`, `net10.0`; xunit.v3 поверх Microsoft.Testing.Platform.

**Spec:** `docs/spec.md`, раздел 6 «Состав правил». Эталон поведения — `docs/oracle/lebedev.md`. Инварианты, на которые опирается план, — `docs/superpowers/plans/2026-09-08-input-for-plan-2.md`.

## Global Constraints

- Целевые платформы ядра: `netstandard2.0;net8.0;net10.0`. Внешних зависимостей нет; на `netstandard2.0` допускаются только `System.Memory` и `PolySharp`, уже подключённые.
- `SearchValues<char>` и `FrozenSet<string>` — только под `#if NET8_0_OR_GREATER`, с эквивалентом для `netstandard2.0`. Поведение веток обязано совпадать.
- Никаких регулярных выражений. `System.Text.RegularExpressions` в `src/Typographer` не используется.
- `TreatWarningsAsErrors` включён, `GenerateDocumentationFile` включён. Каждый публичный член — с XML-комментарием **на русском языке**.
- Ноль аллокаций на пути `Process(ReadOnlySpan<char>, IBufferWriter<char>)` после прогрева пула. `CharBuffer` — изменяемая структура, только по `ref`, не боксируется, не захватывается замыканием. Никаких делегатов и интерфейсов в горячем пути.
- Левый контекст правила читается ИЗ БУФЕРА и не левее `floor`, правый — из исходной строки. Нарушение возвращает разрывы идемпотентности и порчу разметки.
- Приоритет источников при любом сомнении о правильном поведении: ГОСТ Р 7.0.110-2025 ⇒ Мильчин ⇒ практика студии Лебедева (`docs/oracle/lebedev.md`) ⇒ JS-typograf.
- Имена правил совпадают с именами JS-typograf дословно (`docs/RULES.ru.md` проекта typograf/typograf).
- Ветка `feature/scan-rules` от `develop`. Прямой коммит в `master` и `develop` запрещён.
- Каждый коммит заканчивается строками:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01Q5EzzmnERgMbT25oneg7wy
  ```
- Тесты гоняются командой `dotnet test` БЕЗ префикса `rtk` (под Microsoft.Testing.Platform фильтр rtk врёт «0 tests, exit code 5»). Остальные команды — с `rtk`.

---

## Что этот план НЕ делает

- **`common/punctuation/quoteLink` не реализуется.** Правило выносит кавычки за пределы ссылки, то есть перемещает текст через границу тега. Гарантия 3 спецификации обещает разметку байт в байт, и это правило её нарушает по своей сути. Регистрируется в реестре с XML-комментарием «не реализуется, противоречит гарантии 3» — так же, как сейчас честно помечены `ano` и `switchingKeyboardLayout`.
- Группы `common/nbsp/*`, `ru/nbsp/*`, `ru/date/*`, `ru/money/*`, `ru/other/*`, `ru/optalign/*`, `common/html/*`, `common/other/repeatWord` — это фазы Bind, Layout и Emit, они уходят в план 2c.
- Большие словари (единицы измерения, адресные сокращения, «млн»/«млрд», «ООО») — план 2c. Здесь заводятся только месяцы (12 слов) и дни недели (7): ради двух массивов не стоит рвать группу `ru/dash` между планами.

## Решения, принятые до начала работы

1. **Шесть правил нормализации текста реализуются, но выключены в `Default`:** `trimLeft`, `trimRight`, `delLeadingBlanks`, `delTrailingBlanks`, `replaceTab`, `insertFinalNewline`. Они меняют текст за пределами оформления: обрезают фрагмент и трогают отступы. Тот, кто вызвал `Typograf.Html(text)`, такого не ожидает. Причина та же, по которой в `Default` уже выключены `ano` и `switchingKeyboardLayout`. Раздел 6 спецификации, где сказано «`common/space/*` — в Default: да», правится этим планом.
2. **Малые словари вводит этот план.** `Dictionaries.Months` и `Dictionaries.Weekdays` появляются в задаче 9 и используются правилами `ru/dash/month`, `ru/dash/daysMonth`, `ru/dash/weekday`.
3. **`ru/dash/years` остаётся с длинным тире в `Default`.** Оракул Лебедева оставляет `1941-1945` с дефисом (`docs/oracle/lebedev.md`, расхождение 1), но ГОСТ Р 7.0.110-2025, 14.3 требует тире, а ГОСТ в приоритете источников выше практики Лебедева. Расхождение переносится в пресет `Lebedev` задачей 14.

---

## Формат этого плана

Правил пятьдесят одно, и писать реализацию каждого прозой в плане значило бы написать код
дважды. Поэтому носитель требований здесь — **таблица правила**: вход, выход, источник,
участие в `Default`. Каждая строка таблицы прямо переводится в строку `[InlineData]`, и
именно она, а не пересказ, определяет поведение. Прозой описано только спорное: где
источники расходятся, где правило конфликтует с соседним, где легко ошибиться.

Сигнатура правила и место его вызова заданы планом 2a и не обсуждаются:
`TryApply(source, index, previous, floor, rules, ref state, ref buffer)`, вызов из `switch`
по символу-триггеру. Полностью выписанный образец задачи — **Task 2** (правило с помощником
и починкой предиката) и **Task 4** (документный проход с нуля); остальные задачи следуют им.

## Структура файлов

| Файл | Что с ним происходит |
|---|---|
| `src/Typographer/Rules/RuleId.cs` | реестр растёт с 14 записей до 65: индексы 15–65 |
| `src/Typographer/Rules/RuleSet.cs` | пресеты `Default`, `Lebedev`, `Gost`, `Typograf` расходятся по составу |
| `src/Typographer/Internal/Scan/SpaceRules.cs` | + пунктуационные пробелы, скобки, процент |
| `src/Typographer/Internal/Scan/PunctuationRules.cs` | + двойная пунктуация, `?..`, `!..`, `!?` |
| `src/Typographer/Internal/Scan/SymbolRules.cs` | новый: стрелки, градусы, `(c)`, `(tm)`, `(r)`, `№№` |
| `src/Typographer/Internal/Scan/NumberRules.cs` | новый: дроби, знаки сравнения, знак умножения, разряды, десятичная запятая, порядковые |
| `src/Typographer/Internal/Scan/DashRules.cs` | + дефисные частицы, интервалы, месяцы, дни недели, английское тире |
| `src/Typographer/Internal/Scan/DocumentSpaceRules.cs` | новый: обрезка краёв, повторные переводы строки, табы, финальный перевод строки |
| `src/Typographer/Internal/Dictionaries.cs` | + `Months`, `Weekdays` |
| `src/Typographer/Internal/TextScanner.cs` | новые ветви `switch` по символам-триггерам |
| `src/Typographer/Internal/LayoutWriter.cs` | вызов документных правил пробелов |
| `tests/Typographer.Tests/Rules/*` | по файлу тестов на группу правил |
| `docs/spec.md` | раздел 6: состав `Default` приводится в соответствие |

Порядок задач: сперва реестр (1), затем группы правил (2–13), затем пресеты и сверка с оракулом (14).

---

### Task 1: Реестр на 65 правил и пресеты

**Files:**
- Modify: `src/Typographer/Rules/RuleId.cs`
- Modify: `src/Typographer/Rules/RuleSet.cs`
- Test: `tests/Typographer.Tests/Rules/RuleSetTests.cs`

**Interfaces:**
- Consumes: `RuleId(int index, string name, RulePhase phase)`, `RuleId.Registry.All`, `RuleId.Registry.Unsafe`, `RuleSet.None/All/Default/With/Without` — всё существующее.
- Produces: 51 новый `RuleId` с индексами 15–65 и типизированные свойства в дереве `RuleId.Common.*` и `RuleId.Ru.*`; массив `RuleId.Registry.Normalization` — шесть правил нормализации, исключаемых из `Default`.

Имена правил берутся дословно из таблицы ниже. Индексы идут подряд, начиная с 15; ноль зарезервирован за `default(RuleId)`, маска `RuleSet` — 128 бит, тест `RegistryIndicesAreUniqueAndFitTheMask` из плана 2a это стережёт.

| Имя | Фаза | В `Default` | Задача |
|---|---|---|---|
| `common/space/afterColon` | Scan | да | 2 |
| `common/space/afterExclamationMark` | Scan | да | 2 |
| `common/space/afterQuestionMark` | Scan | да | 2 |
| `common/space/afterSemicolon` | Scan | да | 2 |
| `common/space/delBeforeDot` | Scan | да | 2 |
| `common/space/delBeforePercent` | Scan | да | 2 |
| `common/space/delBetweenExclamationMarks` | Scan | да | 2 |
| `common/space/beforeBracket` | Scan | да | 3 |
| `common/space/bracket` | Scan | да | 3 |
| `common/space/squareBracket` | Scan | да | 3 |
| `common/space/trimLeft` | Scan | **нет** | 4 |
| `common/space/trimRight` | Scan | **нет** | 4 |
| `common/space/delLeadingBlanks` | Scan | **нет** | 4 |
| `common/space/delTrailingBlanks` | Scan | **нет** | 4 |
| `common/space/delRepeatN` | Scan | да | 4 |
| `common/space/replaceTab` | Scan | **нет** | 4 |
| `common/space/insertFinalNewline` | Scan | **нет** | 4 |
| `common/punctuation/delDoublePunctuation` | Scan | да | 5 |
| `common/punctuation/quoteLink` | Scan | **не реализуется** | 1 |
| `common/symbols/arrow` | Scan | да | 6 |
| `common/symbols/cf` | Scan | да | 6 |
| `common/symbols/copy` | Scan | да | 6 |
| `ru/symbols/NN` | Scan | да | 6 |
| `common/number/fraction` | Scan | да | 7 |
| `common/number/mathSigns` | Scan | да | 7 |
| `common/number/times` | Scan | да | 7 |
| `common/number/digitGrouping` | Scan | **нет** | 7 |
| `ru/dash/to` | Scan | да | 8 |
| `ru/dash/ka` | Scan | да | 8 |
| `ru/dash/taki` | Scan | да | 8 |
| `ru/dash/koe` | Scan | да | 8 |
| `ru/dash/izpod` | Scan | да | 8 |
| `ru/dash/izza` | Scan | да | 8 |
| `ru/dash/kakto` | Scan | да | 8 |
| `ru/dash/de` | Scan | **нет** | 8 |
| `ru/dash/centuries` | Scan | да | 9 |
| `ru/dash/decade` | Scan | да | 9 |
| `ru/dash/time` | Scan | да | 9 |
| `ru/dash/daysMonth` | Scan | да | 10 |
| `ru/dash/month` | Scan | да | 10 |
| `ru/dash/weekday` | Scan | да | 10 |
| `ru/dash/surname` | Scan | да | 10 |
| `ru/punctuation/exclamation` | Scan | да | 11 |
| `ru/punctuation/exclamationQuestion` | Scan | да | 11 |
| `ru/punctuation/hellipQuestion` | Scan | да | 11 |
| `ru/number/comma` | Scan | да | 12 |
| `ru/number/ordinals` | Scan | да | 12 |
| `ru/space/afterHellip` | Scan | да | 12 |
| `ru/space/year` | Scan | да | 12 |
| `en-GB/dash/main` | Scan | **нет** | 13 |
| `en-US/dash/main` | Scan | **нет** | 13 |

- [x] **Step 1: Написать падающий тест**

В `tests/Typographer.Tests/Rules/RuleSetTests.cs`:

```csharp
[Fact]
public void RegistryHasEveryScanRuleOfTheSpec()
{
    // Реестр — источник имён для docs/rules.md и для RuleId.TryParse. Пропущенное имя
    // означает, что правило нельзя включить по имени, даже если код его реализует.
    Assert.Equal(65, RuleId.Registry.All.Length);
    Assert.True(RuleId.TryParse("common/space/afterColon", out _));
    Assert.True(RuleId.TryParse("ru/dash/kakto", out _));
    Assert.True(RuleId.TryParse("en-GB/dash/main", out _));
}

[Fact]
public void NormalizationRulesAreOutOfDefault()
{
    // Обрезка краёв и замена табов меняют текст за пределами оформления: тот, кто
    // вызвал Typograf.Html(text), такого не ожидает.
    foreach (RuleId rule in RuleId.Registry.Normalization)
    {
        Assert.False(RuleSet.Default.Contains(rule), rule.Name);
        Assert.True(RuleSet.All.Contains(rule), rule.Name);
    }
}
```

- [x] **Step 2: Прогнать тесты**

Run: `dotnet test`
Expected: FAIL — в реестре 14 записей, `Registry.Normalization` не существует.

- [x] **Step 3: Дописать реестр**

В `RuleId.Registry` добавить 51 запись по таблице выше, индексы 15–65, в том же стиле, что существующие:

```csharp
public static readonly RuleId SpaceAfterColon = new(15, "common/space/afterColon", RulePhase.Scan);
```

`quoteLink` регистрируется с XML-комментарием на своём свойстве:

```csharp
/// <summary>
/// Вынос кавычек за пределы ссылки. НЕ РЕАЛИЗУЕТСЯ: правило перемещает текст через
/// границу тега, а гарантия 3 спецификации обещает разметку байт в байт.
/// Зарегистрировано ради паритета имён с JS-typograf.
/// </summary>
public static RuleId QuoteLink => Registry.QuoteLink;
```

Добавить массив:

```csharp
/// <summary>Правила нормализации текста: выключены в Default, меняют текст за пределами оформления.</summary>
public static readonly RuleId[] Normalization =
[
    TrimLeft, TrimRight, DelLeadingBlanks, DelTrailingBlanks, ReplaceTab, InsertFinalNewline,
];
```

В `RuleSet.Default` исключить `Normalization`, `DigitGrouping`, `QuoteLink` вдобавок к существующему `Unsafe`.

- [x] **Step 4: Прогнать тесты**

Run: `dotnet test`
Expected: PASS.

- [x] **Step 5: Коммит**

```bash
rtk git add -A && rtk git commit -m "feat: реестр правил вырос до 65 записей

Имена и состав взяты из docs/RULES.ru.md проекта typograf/typograf — паритет
имён обещан спецификацией. Шесть правил нормализации (обрезка краёв, табы,
финальный перевод строки) зарегистрированы, но исключены из Default: они
меняют текст за пределами оформления. quoteLink зарегистрирован как
нереализуемый — он переносит текст через границу тега вопреки гарантии 3.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Q5EzzmnERgMbT25oneg7wy"
```

---

### Task 2: Пунктуационные пробелы

**Files:**
- Modify: `src/Typographer/Internal/Scan/SpaceRules.cs`
- Modify: `src/Typographer/Internal/TextScanner.cs`
- Test: `tests/Typographer.Tests/Rules/SpaceRulesTests.cs`

**Interfaces:**
- Consumes: соглашение `TryApply(ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules, ref ScanState state, ref CharBuffer buffer)`; `SpaceRules.IsPunctuation(char)`.
- Produces: `SpaceRules.WriteSpaceAfterPunctuation` — общий помощник для двоеточия, точки с запятой, восклицательного и вопросительного знаков, по образцу существующего `WriteSpaceAfterComma`.

Эта задача попутно чинит два известных дефекта предиката `IsPunctuation`, записанных в `docs/superpowers/plans/2026-09-08-input-for-plan-2.md`.

| Правило | Вход | Выход | Источник |
|---|---|---|---|
| `afterColon` | `текст:ещё` | `текст: ещё` | Мильчин, 5.1 |
| `afterSemicolon` | `раз;два` | `раз; два` | Мильчин, 5.1 |
| `afterExclamationMark` | `Ура!Победа` | `Ура! Победа` | Мильчин, 5.1 |
| `afterQuestionMark` | `Что?Как` | `Что? Как` | Мильчин, 5.1 |
| `delBeforeDot` | `слово .` | `слово.` | ГОСТ 8.1 |
| `delBeforePercent` | `50 %` → без правила | `50%` | JS-typograf |
| `delBetweenExclamationMarks` | `Ура ! ! !` | `Ура!!!` | JS-typograf |
| **починка** `delBeforePunctuation` | `8 != 9` | `8 != 9` (пробел цел) | здравый смысл: `!=` — оператор |
| **починка** `delBeforePunctuation` | `текст ....` | `текст ....` (пробел цел) | четыре точки — не многоточие |

Внимание к `delBeforePercent`: оракул Лебедева ставит перед процентом неразрывный пробел (`50_%`, см. `docs/oracle/lebedev.md`), а не удаляет его. Это расхождение источников: правило `common/space/delBeforePercent` из JS-typograf удаляет пробел, практика Лебедева — привязывает. ГОСТ Р 7.0.110-2025, 9.6 требует неразрывный пробел перед знаком процента. **Решение: правило реализуется как в JS-typograf (удаляет пробел) и включается в `Default`, а неразрывный пробел перед `%` ставит правило фазы Bind из плана 2c; при конфликте побеждает ГОСТ, поэтому в плане 2c `delBeforePercent` уходит из `Default`.** Записать это в передачу плану 2c.

- [x] **Step 1: Написать падающие тесты**

```csharp
[Theory]
[InlineData("текст:ещё", "текст: ещё")]
[InlineData("раз;два", "раз; два")]
[InlineData("Ура!Победа", "Ура! Победа")]
[InlineData("Что?Как", "Что? Как")]
public void AddsSpaceAfterPunctuation(string source, string expected)
    => Assert.Equal(expected, Run(source));

[Theory]
[InlineData("8 != 9", "8 != 9")]
[InlineData("текст ....", "текст ....")]
public void KeepsSpaceWhenPunctuationIsNotPunctuation(string source, string expected)
    => Assert.Equal(expected, Run(source));
```

Здесь `Run` — уже существующий в файле приватный помощник; расширь его набор правил новыми именами.

- [x] **Step 2: Прогнать тесты**

Run: `dotnet test`
Expected: FAIL по всем шести случаям.

- [x] **Step 3: Реализовать**

Ветви для `:`, `;`, `!`, `?` добавляются в `SpaceRules.TryApply` по образцу существующей ветви запятой; общий помощник:

```csharp
/// <summary>
/// Дописывает пробел после знака препинания, если справа его нет. Условия те же, что у
/// запятой (см. NeedsSpaceAfterComma): пробел не ставится перед знаком препинания,
/// перед закрывающей скобкой или кавычкой и внутри числа.
/// </summary>
public static void WriteSpaceAfterPunctuation(
    ReadOnlySpan<char> source, int index, char previous, RuleSet rules,
    ref ScanState state, ref CharBuffer buffer)
```

Починка `IsPunctuation` делается не расширением предиката, а проверкой правого контекста в правиле удаления пробела: пробел сохраняется, если за знаком идёт `=` (оператор `!=`, `<=`, `>=`) или если это четвёртая точка подряд.

- [x] **Step 4: Прогнать тесты**

Run: `dotnet test`
Expected: PASS, включая все прежние тесты пробелов.

- [x] **Step 5: Коммит**

Заголовок: `feat: пробелы после двоеточия, точки с запятой и знаков конца предложения`. В теле объяснить починку `!=` и четырёх точек.

---

### Task 3: Пробелы вокруг скобок

**Files:**
- Modify: `src/Typographer/Internal/Scan/SpaceRules.cs`
- Test: `tests/Typographer.Tests/Rules/SpaceRulesTests.cs`

**Interfaces:**
- Consumes: `SpaceRules.TryApply`, `SpaceRules.IsClosing`.
- Produces: ветви `(`, `)`, `[`, `]` в диспетчере `TextScanner`.

| Правило | Вход | Выход | Источник |
|---|---|---|---|
| `beforeBracket` | `слово(текст)` | `слово (текст)` | Мильчин, 5.2 |
| `bracket` | `( текст )` | `(текст)` | Мильчин, 5.2 |
| `squareBracket` | `[ текст ]` | `[текст]` | Мильчин, 5.2 |

Известное отложенное поведение, которое эта задача обязана НЕ сломать: `(раз,)` сейчас даёт `(раз, )` — пробел после запятой ставится перед закрывающей скобкой (см. передачу плана 1). Правило `bracket` удаляет пробел перед закрывающей скобкой и тем самым чинит этот случай. Добавь тест `(раз,)` → `(раз,)`.

- [x] **Step 1: Написать падающие тесты**

```csharp
[Theory]
[InlineData("слово(текст)", "слово (текст)")]
[InlineData("( текст )", "(текст)")]
[InlineData("[ текст ]", "[текст]")]
[InlineData("(раз,)", "(раз,)")]
public void NormalizesSpacesAroundBrackets(string source, string expected)
    => Assert.Equal(expected, Run(source));
```

- [x] **Step 2: Прогнать тесты** — Run: `dotnet test`, Expected: FAIL по всем четырём.
- [x] **Step 3: Реализовать** — ветви скобок в `SpaceRules.TryApply`; пробел перед открывающей скобкой дописывается только если слева буква или цифра, иначе `слово ( текст` даст два пробела.
- [x] **Step 4: Прогнать тесты** — Expected: PASS.
- [x] **Step 5: Коммит** — `feat: пробелы вокруг круглых и квадратных скобок`.

---

### Task 4: Нормализация текста на уровне документа

**Files:**
- Create: `src/Typographer/Internal/Scan/DocumentSpaceRules.cs`
- Modify: `src/Typographer/Internal/LayoutWriter.cs`
- Test: `tests/Typographer.Tests/Rules/DocumentSpaceRulesTests.cs`

**Interfaces:**
- Consumes: `MarkupScanner`, `Segment`, `CharBuffer`, `RuleSet`.
- Produces: `internal static void DocumentSpaceRules.Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)` — документный проход, вызывается фазой Layout ДО расстановки переносов и абзацев.

Этим правилам не хватает символьного контекста: они работают с началом и концом документа и со строками целиком. Поэтому они не в `TextScanner`, а отдельным документным проходом — так же, как переносы и абзацы.

| Правило | Вход | Выход | В `Default` |
|---|---|---|---|
| `trimLeft` | `«  текст»` | `«текст»` | нет |
| `trimRight` | `«текст  »` | `«текст»` | нет |
| `delLeadingBlanks` | строка с отступом | строка без отступа | нет |
| `delTrailingBlanks` | `текст   \nдалее` | `текст\nдалее` | нет |
| `delRepeatN` | `текст\n\n\n\nдалее` | `текст\n\nдалее` | **да** |
| `replaceTab` | `текст\tещё` | `текст    ещё` | нет |
| `insertFinalNewline` | `текст` | `текст\n` | нет |

`delRepeatN` оставлен в `Default`: повторные переводы строки — это оформление, а не содержание, и схлопывание их до одного пустого межабзацного интервала соответствует ГОСТ 8.2. Остальные шесть выключены (см. «Решения, принятые до начала работы»).

Правила применяются ТОЛЬКО к текстовым сегментам: перевод строки внутри `<pre>` или значения атрибута трогать нельзя.

- [x] **Step 1: Написать падающие тесты**

```csharp
private static string Run(string html, params RuleId[] rules)
    => new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None.With(rules) }).Process(html);

[Fact]
public void CollapsesRepeatedLineBreaks()
    => Assert.Equal("текст\n\nдалее", Run("текст\n\n\n\nдалее", RuleId.Common.Space.DelRepeatN));

[Fact]
public void TrimsDocumentEdgesOnlyWhenAsked()
{
    Assert.Equal("  текст  ", Run("  текст  "));
    Assert.Equal("текст", Run("  текст  ", RuleId.Common.Space.TrimLeft, RuleId.Common.Space.TrimRight));
}

[Fact]
public void DoesNotTouchProtectedContent()
    => Assert.Equal(
        "<pre>текст\n\n\n\nдалее</pre>",
        Run("<pre>текст\n\n\n\nдалее</pre>", RuleId.Common.Space.DelRepeatN));
```

- [x] **Step 2: Прогнать тесты** — Expected: FAIL, класса `DocumentSpaceRules` нет.
- [x] **Step 3: Реализовать** проход по образцу `LayoutWriter.WriteBreaks`: `MarkupScanner` по документу, обработка только `SegmentKind.Text`, разметка и защищённые зоны — байт в байт.
- [x] **Step 4: Прогнать тесты** — Expected: PASS.
- [x] **Step 5: Коммит** — `feat: нормализация пробелов и переводов строк на уровне документа`.

---

### Task 5: Двойная пунктуация

**Files:**
- Modify: `src/Typographer/Internal/Scan/PunctuationRules.cs`
- Test: `tests/Typographer.Tests/Rules/PunctuationRulesTests.cs`

| Правило | Вход | Выход | Источник |
|---|---|---|---|
| `delDoublePunctuation` | `слово,,ещё` | `слово,ещё` | JS-typograf |
| `delDoublePunctuation` | `слово;;ещё` | `слово;ещё` | JS-typograf |
| `delDoublePunctuation` | `слово!!ещё` | `слово!ещё` — НЕТ, см. ниже | — |

Разграничение с `ru/punctuation/exclamation` (задача 11): `!!` → `!` делает русское правило, потому что для восклицательного знака удвоение осмысленно в разговорной речи и решение о нём — вопрос языка, а не общей типографики. `delDoublePunctuation` работает с `,` `;` `:` и точкой, но НЕ с `!` и `?`. Оракул Лебедева `Двойная пунктуация!! и ?? и ?!` не меняет вовсе (`docs/oracle/lebedev.md`) — это подтверждает, что удвоение знаков конца предложения трогать рискованно.

- [x] **Step 1: Написать падающие тесты** — по таблице, плюс `Двойная!! и ??` остаётся без изменений при выключенном `ru/punctuation/exclamation`.
- [x] **Step 2: Прогнать тесты** — Expected: FAIL.
- [x] **Step 3: Реализовать** ветвь в `PunctuationRules.TryApply`: если текущий символ равен предыдущему в буфере (не левее `floor`) и входит в набор `,` `;` `:` `.`, символ не пишется. Точка требует осторожности: три точки уже схлопывает `hellip`, поэтому `delDoublePunctuation` не должен трогать точки вовсе — иначе многоточие перестанет собираться. Ограничь набор `,` `;` `:`.
- [x] **Step 4: Прогнать тесты** — Expected: PASS, все прежние тесты многоточия зелёные.
- [x] **Step 5: Коммит** — `feat: удаление двойной пунктуации`.

---

### Task 6: Символы

**Files:**
- Create: `src/Typographer/Internal/Scan/SymbolRules.cs`
- Modify: `src/Typographer/Internal/Chars.cs`, `src/Typographer/Internal/TextScanner.cs`
- Test: `tests/Typographer.Tests/Rules/SymbolRulesTests.cs`

| Правило | Вход | Выход | Источник |
|---|---|---|---|
| `common/symbols/copy` | `(c) 2026` | `© 2026` | JS-typograf, оракул |
| `common/symbols/copy` | `(tm)` | `™` | JS-typograf, оракул |
| `common/symbols/copy` | `(r)` | `®` | JS-typograf, оракул |
| `common/symbols/arrow` | `->` | `→` | JS-typograf |
| `common/symbols/arrow` | `<-` | `←` | JS-typograf |
| `common/symbols/cf` | `25 C` | `25 °C` | JS-typograf |
| `common/symbols/cf` | `451 F` | `451 °F` | JS-typograf |
| `ru/symbols/NN` | `№№ 5` | `№ 5` | JS-typograf |

Новые константы в `Chars`: `Copyright = '©'`, `Trademark = '™'`, `Registered = '®'`, `ArrowRight = '→'`, `ArrowLeft = '←'`.

Осторожно с `arrow`: `<-` начинается с `<`. После починки из плана 2a правило пробелов не создаёт псевдотегов, но здесь символ `<` уже есть во входе. `MarkupScanner` отдаёт `<-` текстом (за `<` не следует буква), поэтому правило работает внутри текстового сегмента — но тест на это обязателен.

Осторожно с `cf`: `C` и `F` — латинские буквы, и правило обязано срабатывать только после числа с пробелом, иначе `10 Cm` превратится в `10 °Cm`. Требуй, чтобы за буквой не следовала буква.

- [x] **Step 1: Написать падающие тесты** — по таблице плюс `10 Cm` → `10 Cm` (правило не сработало) и `a<-b` внутри текста.
- [x] **Step 2: Прогнать тесты** — Expected: FAIL.
- [x] **Step 3: Реализовать** `SymbolRules.TryApply` с ветвями `(`, `-`, `<`, `C`, `F`, `№`. Ветвь `-` в диспетчере уже занята `DashRules`: вызывай `SymbolRules` первым и только для случая `->`, иначе тире.
- [x] **Step 4: Прогнать тесты** — Expected: PASS.
- [x] **Step 5: Коммит** — `feat: типографские символы — копирайт, стрелки, градусы, знак номера`.

---

### Task 7: Числа

**Files:**
- Create: `src/Typographer/Internal/Scan/NumberRules.cs`
- Modify: `src/Typographer/Internal/Chars.cs`, `src/Typographer/Internal/TextScanner.cs`
- Test: `tests/Typographer.Tests/Rules/NumberRulesTests.cs`

| Правило | Вход | Выход | В `Default` | Источник |
|---|---|---|---|---|
| `fraction` | `1/2` | `½` | да | JS-typograf |
| `fraction` | `1/4`, `3/4` | `¼`, `¾` | да | JS-typograf |
| `mathSigns` | `!=` | `≠` | да | JS-typograf |
| `mathSigns` | `<=`, `>=` | `≤`, `≥` | да | JS-typograf |
| `mathSigns` | `+-` | `±` | да | JS-typograf |
| `times` | `10 x 5` | `10×5` | да | JS-typograf, оракул |
| `digitGrouping` | `1000000` | `1 000 000` | **нет** | ГОСТ 9.5 |

`digitGrouping` выключен в `Default`, как и было в спецификации: разбиение разрядов меняет запись числа, а это ближе к смыслу, чем к оформлению. Оракул его применяет (`1000000` → `1_000_000`), поэтому в пресете `Lebedev` он включается — задача 14.

`mathSigns` снимает первый из двух дефектов `delBeforePunctuation`: с включённым правилом `8 != 9` становится `8 ≠ 9` ещё до того, как правило пробелов посмотрит на `!`. Починка из задачи 2 остаётся нужна: правила независимы, и `mathSigns` может быть выключен.

Осторожно с `times`: латинская `x` между числами превращается в знак умножения вместе с окружающими пробелами (`10 x 5` → `10×5`). Кириллическая `х` не трогается — это буква.

- [x] **Step 1: Написать падающие тесты** — по таблице плюс `5 хорошо` (кириллическая `х` цела) и `1/2 текста` (дробь только между цифрами).
- [x] **Step 2: Прогнать тесты** — Expected: FAIL.
- [x] **Step 3: Реализовать** `NumberRules.TryApply`. Дроби — только для трёх пар из таблицы: остальные дроби в Unicode неполны, и `5/7` оставлять как есть честнее, чем выдумывать.
- [x] **Step 4: Прогнать тесты** — Expected: PASS.
- [x] **Step 5: Коммит** — `feat: дроби, знаки сравнения, знак умножения и разряды чисел`.

---

### Task 8: Дефисные частицы

**Files:**
- Modify: `src/Typographer/Internal/Scan/DashRules.cs`
- Test: `tests/Typographer.Tests/Rules/DashRulesTests.cs`

Эти правила не ставят тире, а НЕ дают правилу `ru/dash/main` превратить дефис в тире там, где дефис обязателен, и заодно чинят пропущенный дефис.

| Правило | Вход | Выход | Источник |
|---|---|---|---|
| `to` | `кто то`, `где либо`, `что нибудь` | `кто-то`, `где-либо`, `что-нибудь` | Розенталь, §63 |
| `ka` | `скажи ка`, `ну кась` | `скажи-ка`, `ну-кась` | Розенталь, §63 |
| `taki` | `верно таки` | `верно-таки` | Розенталь, §63 |
| `koe` | `кое что`, `кой какой` | `кое-что`, `кой-какой` | Розенталь, §63 |
| `izpod` | `из под стола` | `из-под стола` | Розенталь, §63 |
| `izza` | `из за угла` | `из-за угла` | Розенталь, §63 |
| `kakto` | `как то так` | `как-то так` | Розенталь, §63 |
| `de` (вне `Default`) | `Иванов де сказал` | `Иванов-де сказал` | спорно, см. ниже |

`de` выключено в `Default` вслед за JS-typograf: частица «де» омонимична предлогу в иностранных фамилиях (`Шарль де Голль`), и правило без словаря фамилий ошибается. Тест на `Шарль де Голль` обязателен: с выключенным правилом текст не меняется.

Правило работает по левому контексту из буфера (слово перед пробелом) и правому из исходной строки (слово после пробела). Слово собирается посимвольно, без аллокаций, сравнение — посимвольное, как в `Dictionaries.IsAbbreviationPart`.

- [x] **Step 1: Написать падающие тесты** — по таблице, плюс `Шарль де Голль` без изменений и `кто то` внутри `<code>` без изменений.
- [x] **Step 2: Прогнать тесты** — Expected: FAIL.
- [x] **Step 3: Реализовать** ветвь пробела в `DashRules`: увидев пробел, посмотреть слово справа; если оно из списка частиц и слово слева не пусто, записать дефис вместо пробела.
- [x] **Step 4: Прогнать тесты** — Expected: PASS.
- [x] **Step 5: Коммит** — `feat: дефис в частицах «то», «либо», «нибудь», «ка», «таки», «кое»`.

---

### Task 9: Числовые интервалы и малые словари

**Files:**
- Modify: `src/Typographer/Internal/Scan/DashRules.cs`, `src/Typographer/Internal/Dictionaries.cs`
- Test: `tests/Typographer.Tests/Rules/DashRulesTests.cs`

**Interfaces:**
- Produces: `Dictionaries.Months` (12 слов в родительном падеже: «января» … «декабря»), `Dictionaries.Weekdays` (7 слов), оба — `internal static bool IsMonth(ReadOnlySpan<char>)` и `IsWeekday(ReadOnlySpan<char>)` со сравнением без аллокаций; на `net8.0` и выше — `FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>>`, на `netstandard2.0` — сравнение по длине и посимвольно.

| Правило | Вход | Выход | Источник |
|---|---|---|---|
| `centuries` | `XIX-XX вв.` | `XIX—XX вв.` | ГОСТ 14.3 |
| `decade` | `80-90-е гг.` | `80—90-е гг.` | ГОСТ 14.3 |
| `time` | `10:00-11:00` | `10:00—11:00` | ГОСТ 14.3 |

Тире здесь длинное и без отбивки, как у `ru/dash/years`, — единообразие важнее, чем разнобой между интервалами.

- [x] **Step 1: Написать падающие тесты** — по таблице, плюс `10:00-11:00` внутри ссылки не трогается и `XIX-XX` без «вв.» тоже становится тире (римские цифры сами по себе достаточный признак).
- [x] **Step 2: Прогнать тесты** — Expected: FAIL.
- [x] **Step 3: Реализовать** словари и три правила.
- [x] **Step 4: Прогнать тесты** — Expected: PASS.
- [x] **Step 5: Коммит** — `feat: тире в веках, десятилетиях и интервалах времени`.

---

### Task 10: Месяцы, дни недели и сокращения с тире

**Files:**
- Modify: `src/Typographer/Internal/Scan/DashRules.cs`
- Test: `tests/Typographer.Tests/Rules/DashRulesTests.cs`

| Правило | Вход | Выход | Источник |
|---|---|---|---|
| `daysMonth` | `5-10 января` | `5—10 января` | ГОСТ 14.3 |
| `month` | `январь-февраль` | `январь—февраль` | ГОСТ 14.3 |
| `weekday` | `понедельник-среда` | `понедельник—среда` | ГОСТ 14.3 |
| `surname` | `Салтыков-Щедрин` | без изменений | Розенталь |

`surname` — не преобразование, а защита: двойная фамилия пишется через дефис, и `ru/dash/main` не должен его трогать. Правило срабатывает, когда с обеих сторон дефиса заглавные буквы. Тест обязателен для `Салтыков-Щедрин` и для `Иванов - Петров` (с пробелами — тире ставится).

- [x] **Step 1: Написать падающие тесты** — по таблице.
- [x] **Step 2: Прогнать тесты** — Expected: FAIL для первых трёх, PASS для `surname` (сейчас `ru/dash/main` и так не трогает дефис внутри слова — тест закрепляет).
- [x] **Step 3: Реализовать**.
- [x] **Step 4: Прогнать тесты** — Expected: PASS.
- [x] **Step 5: Коммит** — `feat: тире между месяцами, днями недели и днями одного месяца`.

---

### Task 11: Русская пунктуация

**Files:**
- Modify: `src/Typographer/Internal/Scan/PunctuationRules.cs`
- Test: `tests/Typographer.Tests/Rules/PunctuationRulesTests.cs`

| Правило | Вход | Выход | В `Default` | Источник |
|---|---|---|---|---|
| `exclamation` | `Ура!!` | `Ура!` | да | JS-typograf |
| `exclamationQuestion` | `Что!?` | `Что?!` | да | Розенталь, §68 |
| `hellipQuestion` | `Что?…` | `Что?..` | да | Мильчин, 5.4 |
| `hellipQuestion` | `Ура!…` | `Ура!..` | да | Мильчин, 5.4 |
| `ano` | `Пришёл а ушёл` | `Пришёл, а ушёл` | **нет** | спорно |

`ano` реализуется здесь (сейчас зарегистрировано и честно помечено как нереализованное), но остаётся выключенным в `Default`: правило само расставляет запятые, то есть меняет смысл, а не оформление. При реализации поправь его XML-комментарий — он сейчас говорит «не реализовано».

Оракул `Двойная пунктуация!! и ?? и ?!` не меняет. Наше `exclamation` — расхождение с оракулом в пользу JS-typograf; в пресете `Lebedev` правило выключается задачей 14.

- [x] **Step 1: Написать падающие тесты** — по таблице.
- [x] **Step 2: Прогнать тесты** — Expected: FAIL.
- [x] **Step 3: Реализовать**.
- [x] **Step 4: Прогнать тесты** — Expected: PASS.
- [x] **Step 5: Коммит** — `feat: русская пунктуация — «?..», «!..», «?!» и запятые перед «а» и «но»`.

---

### Task 12: Числа, пробелы и знаки русского языка

**Files:**
- Modify: `src/Typographer/Internal/Scan/NumberRules.cs`, `src/Typographer/Internal/Scan/SpaceRules.cs`
- Test: `tests/Typographer.Tests/Rules/NumberRulesTests.cs`, `tests/Typographer.Tests/Rules/SpaceRulesTests.cs`

| Правило | Вход | Выход | Источник |
|---|---|---|---|
| `ru/number/comma` | `3.14` | `3,14` | ГОСТ 9.4 |
| `ru/number/ordinals` | `25-ый`, `25-ое` | `25-й`, `25-е` | Розенталь, §64 |
| `ru/space/afterHellip` | `Что?..Как` | `Что?.. Как` | Мильчин, 5.4 |
| `ru/space/year` | `2026год` | `2026 год` | ГОСТ 9.3 |

`ru/number/comma` осторожен: точка между цифрами становится запятой только если это не дата (`09.09.2026`) и не версия (`1.2.3`). Признак — ровно одна точка в числе и не более двух цифр слева от неё в дате… Надёжного признака по одному символу нет, поэтому правило требует, чтобы слева и справа от точки были цифры И чтобы во всём числе была ровно одна точка. Тест обязателен: `09.09.2026` не меняется, `1.2.3` не меняется.

- [x] **Step 1: Написать падающие тесты** — по таблице плюс два случая-исключения.
- [x] **Step 2: Прогнать тесты** — Expected: FAIL.
- [x] **Step 3: Реализовать**.
- [x] **Step 4: Прогнать тесты** — Expected: PASS.
- [x] **Step 5: Коммит** — `feat: десятичная запятая, порядковые числительные, пробел после многоточия и перед «год»`.

---

### Task 13: Английское тире

**Files:**
- Modify: `src/Typographer/Internal/Scan/DashRules.cs`
- Test: `tests/Typographer.Tests/Rules/DashRulesTests.cs`

| Правило | Вход | Выход | В `Default` |
|---|---|---|---|
| `en-GB/dash/main` | `word - word` | `word – word` (короткое тире с отбивкой) | нет |
| `en-US/dash/main` | `word - word` | `word—word` (длинное тире без отбивки) | нет |

Оба выключены в `Default`: спецификация, раздел 7, говорит, что язык только русский, детектор языка не делается, а латинские вставки обрабатываются общими правилами. Правила доступны через `RuleSet` тому, кто типографирует английский текст осознанно.

Оба правила конфликтуют с `ru/dash/main` на одном и том же входе. Разрешение конфликта: если включено русское правило, оно побеждает — оно идёт раньше в ветви символа `-`. Тест обязателен: с обоими включёнными правилами результат русский.

- [x] **Step 1: Написать падающие тесты** — по таблице плюс тест на конфликт.
- [x] **Step 2: Прогнать тесты** — Expected: FAIL.
- [x] **Step 3: Реализовать**.
- [x] **Step 4: Прогнать тесты** — Expected: PASS.
- [x] **Step 5: Коммит** — `feat: английское тире en-GB и en-US`.

---

### Task 14: Пресеты и сверка со снимком оракула

**Files:**
- Modify: `src/Typographer/Rules/RuleSet.cs`, `docs/spec.md`
- Test: `tests/Typographer.Tests/Rules/RuleSetTests.cs`, `tests/Typographer.Tests/Corpus/HardCases.cs`

**Interfaces:**
- Consumes: все правила задач 2–13.
- Produces: пресеты `Lebedev`, `Gost`, `Typograf`, расходящиеся с `Default` по составу; обновлённый раздел 6 спецификации.

Расхождения пресетов, установленные снимком оракула (`docs/oracle/lebedev.md`) и ГОСТом:

| Пресет | Отличие от `Default` | Основание |
|---|---|---|
| `Lebedev` | без `ru/dash/years` (оракул оставляет `1941-1945` с дефисом) | снимок, расхождение 1 |
| `Lebedev` | без `ru/punctuation/exclamation` (оракул не трогает `!!`) | снимок |
| `Lebedev` | с `common/number/digitGrouping` (оракул разбивает `1000000`) | снимок, расхождение 2 |
| `Gost` | с `ru/dash/years`, без `common/space/delBeforePercent` | ГОСТ 14.3 и 9.6 |
| `Typograf` | `Default` плюс `ano` и `switchingKeyboardLayout` | паритет дефолтов JS-typograf |

- [x] **Step 1: Написать падающий тест**

```csharp
[Fact]
public void PresetsDivergeFromDefault()
{
    // Пресеты перестали быть псевдонимами Default: у каждого своё основание,
    // записанное в XML-комментарии рядом.
    Assert.NotEqual(RuleSet.Default.Count, RuleSet.Lebedev.Count);
    Assert.False(RuleSet.Lebedev.Contains(RuleId.Ru.Dash.Years));
    Assert.True(RuleSet.Lebedev.Contains(RuleId.Common.Number.DigitGrouping));
    Assert.True(RuleSet.Gost.Contains(RuleId.Ru.Dash.Years));
    Assert.False(RuleSet.Gost.Contains(RuleId.Common.Space.DelBeforePercent));
}

[Theory]
[InlineData("1941-1945 гг.", "1941—1945 гг.")]
public void GostKeepsDashInYearRange(string source, string expected)
    => Assert.Equal(expected, new TextTypograf(new TextOptions { Rules = RuleSet.Gost }).Process(source));
```

- [x] **Step 2: Прогнать тест** — Expected: FAIL, пресеты сейчас совпадают с `Default`.
- [x] **Step 3: Развести пресеты** и поправить их XML-комментарии: сейчас там написано «совпадает с Default, разойдётся в следующей версии».
- [x] **Step 4: Пополнить корпус** — добавить в `HardCases.All` по одному входу из каждой группы правил этого плана; корпус гоняется тестами идемпотентности и целостности разметки, то есть новые правила автоматически проверяются на гарантии 3 и 5.
- [x] **Step 5: Поправить спецификацию** — раздел 6: `common/space/*` больше не «в Default: да» целиком; указать шесть исключений и причину.
- [x] **Step 6: Прогнать тесты** — Expected: PASS.
- [x] **Step 7: Коммит** — `feat: пресеты Lebedev, Gost и Typograf разошлись с Default`.

---

## Приёмка плана

- [x] `dotnet test` — все тесты зелёные на всех целевых платформах.
- [x] `rtk dotnet build -c Release` — без предупреждений.
- [x] `RuleId.Registry.All.Length == 65`; `RuleId.TryParse` находит каждое имя из таблицы задачи 1.
- [x] Ни одно правило не аллоцирует: путь `Process(ReadOnlySpan<char>, IBufferWriter<char>)` по-прежнему даёт 0 B в бенчмарке.
- [x] Гарантии 3, 4, 5 держатся на расширенном корпусе `HardCases.All`.
- [x] Пресеты `Lebedev`, `Gost`, `Typograf` отличаются от `Default` и друг от друга; отличия объяснены в XML-комментариях.
- [x] Раздел 6 спецификации соответствует коду.

## Что этот план передаёт плану 2c

| Пункт | Что нужно знать |
|---|---|
| `delBeforePercent` | реализован по JS-typograf (удаляет пробел), в `Gost` выключен; неразрывный пробел перед `%` ставит правило фазы Bind — при конфликте побеждает ГОСТ, и правило уходит из `Default` |
| Малые словари | `Dictionaries.Months` и `Weekdays` уже есть; большие словари единиц, адресов и «млн»/«млрд» — работа 2c |
| Сигнатура правила | `TryApply(source, index, previous, floor, rules, ref state, ref buffer)`, `true` — символ обработан |
| `quoteLink` | зарегистрирован как нереализуемый: перемещает текст через границу тега вопреки гарантии 3 |
| Оракул | `docs/oracle/lebedev.md`, раздел «Расхождения» — пункты 2 и 3 целиком про фазу Bind, это прямой список работ 2c |
