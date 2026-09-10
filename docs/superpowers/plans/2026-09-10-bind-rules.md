# План 2c: правила фазы Bind

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Реализовать все правила фазы `Bind` — неразрывные пробелы, словарные сокращения, даты, деньги, ударения и телефоны, — доведя реестр с 65 зарегистрированных правил до 96 и закрыв последнюю большую группу правил, которой нужен словарь, а не соседний символ.

**Architecture:** Фаза `Bind` перестаёт быть проходом «патчим пробелы на месте» и становится ПИШУЩИМ проходом с окном из двух токенов. Причина в задаче 2. Правила живут в `src/Typographer/Internal/Bind/*` тремя видами: перезапись токена, склейка пробела и символьное правило с чтением вперёд. Четвёртый буфер на документ при этом не появляется — буфер фазы `Prepare` переиспользуется.

**Tech Stack:** C# 13, .NET 10 SDK; целевые платформы `netstandard2.0`, `net8.0`, `net10.0`; xunit.v3 поверх Microsoft.Testing.Platform.

**Spec:** `docs/spec.md`, раздел 6 «Состав правил». Эталон поведения — `docs/oracle/lebedev.md`. Инварианты, на которые опирается план, — `docs/superpowers/plans/2026-09-08-input-for-plan-2.md`. Соглашения фазы `Scan`, на которые план ссылается по аналогии, — `docs/superpowers/plans/2026-09-09-scan-rules.md`.

## Global Constraints

- Целевые платформы ядра: `netstandard2.0;net8.0;net10.0`. Внешних зависимостей нет; на `netstandard2.0` допускаются только `System.Memory` и `PolySharp`, уже подключённые.
- `SearchValues<char>` и `FrozenSet<string>` — только под `#if NET8_0_OR_GREATER`, с эквивалентом для `netstandard2.0`. Поведение веток обязано совпадать.
- Никаких регулярных выражений. `System.Text.RegularExpressions` в `src/Typographer` не используется.
- `TreatWarningsAsErrors` включён, `GenerateDocumentationFile` включён. Каждый публичный член — с XML-комментарием **на русском языке**.
- Ноль аллокаций на пути `Process(ReadOnlySpan<char>, IBufferWriter<char>)` после прогрева пула. `CharBuffer` — изменяемая структура, только по `ref`, не боксируется, не захватывается замыканием. Никаких делегатов и интерфейсов в горячем пути. Словари фазы — статические массивы строк, сравнение посимвольное, без аллокации ключа из спана.
- Правило фазы `Bind` не имеет права переписывать буфер левее `BindState.SafeFrom`: там лежит уже скопированная разметка, а гарантия 3 обещает её байт в байт.
- Приоритет источников при любом сомнении о правильном поведении: ГОСТ Р 7.0.110-2025 ⇒ Мильчин ⇒ практика студии Лебедева (`docs/oracle/lebedev.md`) ⇒ JS-typograf.
- Имена правил совпадают с именами JS-typograf дословно.
- Ветка `feature/bind-rules` от `develop`. Прямой коммит в `master` и `develop` запрещён.
- Каждый коммит заканчивается строками:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
  ```
- Тесты гоняются командой `dotnet test` БЕЗ префикса `rtk` (под Microsoft.Testing.Platform фильтр rtk врёт «0 tests, exit code 5»). Остальные команды — с `rtk`.

---

## Что этот план НЕ делает

- **Группы `common/html/*` (8 правил) и `ru/optalign/*` (3 правила) остаются плану 2d.** Это фазы `Layout` и `Emit`: автоссылки, экранирование, `br`, `p`, атрибуты, снятие тегов, `&quot;` и висячая пунктуация. Они не словарные и не работают с токенами — общего кода с этим планом у них нет, зато есть общая тема «правило создаёт или уничтожает разметку», которую надо решать один раз и целиком. Реестр после плана 2c — 96 правил из 107, после 2d — 107.
- Правило `common/punctuation/quoteLink` по-прежнему не реализуется (гарантия 3), решение принято планом 2b.
- Производительность конвейера в этом плане не улучшается. Задача 2 добавляет фазе `Bind` один сквозной проход записи вместо точечных патчей — это дороже, чем сейчас, и об этом сказано вслух в самой задаче.

## Решения, принятые до начала работы

1. **`common/nbsp/replaceNbsp` выключается в `Default`.** Правило заменяет уже стоящие в тексте неразрывные пробелы на обычные, чтобы типограф расставил свои. Это уничтожение авторского решения: неразрывный пробел во входе поставлен руками и означает «здесь рвать нельзя». Причина та же, по которой планом 2b выведены из `Default` семь правил нормализации. Раздел 6 спецификации, где группа `common/nbsp/*` помечена «в Default: да», правится этим планом с оговоркой.

2. **`ru/nbsp/addr` остаётся в `Default`, хотя оракул Лебедева его не применяет.** Снимок (`docs/oracle/lebedev.md`) показывает `ул. Ленина`, `г. Москва`, `с. Никольское` с ОБЫЧНЫМ пробелом, а число после сокращения — с неразрывным (`д._5`, `кв._12`). ГОСТ Р 7.0.110-2025, 9.5 и Мильчин требуют не отрывать сокращение от относящегося к нему слова; приоритет источников выше практики Лебедева. Расхождение переносится в пресет `Lebedev` задачей 16.

3. **После знака номера и параграфа ставится УЗКИЙ неразрывный пробел U+202F.** ГОСТ 16.4 требует полукруглую (узкую) отбивку, JS-typograf делает то же. Оракул Лебедева ставит обычный неразрывный (`№_5`) — расхождение зафиксировано, но не воспроизводится: пресетом его не выразить, потому что выбор символа лежит внутри правила. U+202F уже помечен невидимым в таблице сущностей и выводится числовым кодом `&#8239;` (план 2a, задача 8).

4. **`ru/date/fromISO` остаётся в `Default`, но с жёстким предикатом.** Правило переставляет `2018-10-10` в `10.10.2018` — то есть меняет данные, а не оформление, и на техническом тексте это вред. Предикат сужается до безусловно датного вида: ровно `dddd-dd-dd`, месяц не больше 12, день не больше 31, по обе стороны токена не цифра. Всё остальное (`1941-1945`, `192.168.0.1`, `10-15`) правилом не затрагивается по построению. Разбор проверяется отрицательными тестами наравне с положительными.

5. **`ru/other/phone-number` реализуется в узком виде.** Правило распознаёт ТОЛЬКО последовательность, начинающуюся с `+7` или `8` и содержащую ровно 11 цифр, разделённых пробелами, дефисами и круглыми скобками. Ничего похожего на «любые десять цифр» правило не трогает: оно в `Default`, и ложное срабатывание испортило бы артикул, номер счёта и версию. Вывод — канонический `+7_999_123-45-67` с неразрывными пробелами; оборачивать телефон в `<nobr>`, как делает оракул, нельзя (гарантия 4).

6. **Большие словари вводит этот план.** `Dictionaries` растёт с двух списков до одиннадцати; все они — статические массивы строк в одном файле, потому что живут одну фазу и меняются вместе.

---

## Формат этого плана

Правил тридцать одно, и писать реализацию каждого прозой значило бы написать код дважды.
Носитель требований здесь — **таблица правила**: вход, выход, источник, участие в `Default`.
Каждая строка прямо переводится в строку `[InlineData]`, и именно она, а не пересказ,
определяет поведение. Прозой описано только спорное: где источники расходятся, где правило
конфликтует с соседним, где легко ошибиться.

Полностью выписанные образцы — **Task 2** (инфраструктура фазы и все три соглашения о
правилах) и **Task 4** (типичная задача-правило поверх этой инфраструктуры). Остальные
задачи следуют им.

В таблицах: `_` — неразрывный пробел U+00A0, `~` — узкий неразрывный пробел U+202F.
В тестах их писать ТОЛЬКО через `Chars.Nbsp` и `Chars.NarrowNbsp` в интерполяции — литерал
неразрывного пробела в исходнике теста невидим глазом и ревью его не поймает.

## Структура файлов

| Файл | Что с ним происходит |
|---|---|
| `src/Typographer/Rules/RuleId.cs` | реестр растёт с 65 записей до 96: индексы 66–96 |
| `src/Typographer/Rules/RuleSet.cs` | `Overlaps`, маска фазы `Bind`, расхождения пресетов |
| `src/Typographer/Internal/WordBinder.cs` | переписывается: пишущий проход с окном из двух токенов |
| `src/Typographer/Internal/Bind/NbspRules.cs` | новый: склейки по числу, словарю и позиции в предложении |
| `src/Typographer/Internal/Bind/MarkRules.cs` | новый: `№`, `§`, `¶` — узкий неразрывный пробел со вставкой |
| `src/Typographer/Internal/Bind/RewriteRules.cs` | новый: `гг.`, `вв.`, `P. S.`, `м²`, повтор слова, ударение |
| `src/Typographer/Internal/Bind/DateRules.cs` | новый: ISO-дата, регистр месяца и дня недели |
| `src/Typographer/Internal/Bind/MoneyRules.cs` | новый: символ валюты, рубль |
| `src/Typographer/Internal/Bind/PhoneRules.cs` | новый: телефонные номера |
| `src/Typographer/Internal/Dictionaries.cs` | + девять словарей |
| `src/Typographer/Internal/Preparer.cs` | + калитки `delBOM` и `replaceNbsp` |
| `src/Typographer/Internal/Chars.cs` | + `Section`, `Pilcrow`, `Ruble`, `Acute`, `Square`, `Cube` |
| `src/Typographer/HtmlTypograf.cs`, `src/Typographer/TextTypograf.cs` | фаза `Bind` пишет в переиспользованный буфер |
| `tests/Typographer.Tests/Rules/*` | по файлу тестов на группу правил |
| `docs/spec.md` | раздел 6: состав `Default` приводится в соответствие |

Порядок задач: реестр (1), инфраструктура фазы (2), фаза `Prepare` (3), склейки (4–9),
перезаписи (10–11), даты, деньги, ударение, повтор, телефон (12–15), пресеты и сверка (16).

---

### Task 1: Реестр на 96 правил

**Files:**
- Modify: `src/Typographer/Rules/RuleId.cs`
- Modify: `src/Typographer/Rules/RuleSet.cs`
- Test: `tests/Typographer.Tests/Rules/RuleSetTests.cs`

**Interfaces:**
- Consumes: `RuleId(int index, string name, RulePhase phase)`, `RuleId.Registry.All`, `RuleId.Registry.Unsafe`, `RuleId.Registry.Normalization`, `RuleId.Registry.OptIn`, `RuleSet.None/All/Default/With/Without/Contains`.
- Produces: 31 новый `RuleId` с индексами 66–96 и типизированные свойства в дереве; `RuleId.Registry.OptIn` пополняется; `public bool RuleSet.Overlaps(RuleSet other)`; `internal static RuleSet RuleSet.BindPhase`.

Имена берутся дословно из таблицы. Индексы идут подряд с 66; маска — 128 бит, 96 в неё
входит с запасом, тест `RegistryIndicesAreUniqueAndFitTheMask` из плана 2a это стережёт.

| Имя | Фаза | В `Default` | Задача |
|---|---|---|---|
| `common/other/delBOM` | Prepare | да | 3 |
| `common/nbsp/replaceNbsp` | Prepare | **нет** | 3 |
| `common/nbsp/afterNumber` | Bind | да | 4 |
| `ru/nbsp/dayMonth` | Bind | да | 4 |
| `ru/nbsp/year` | Bind | да | 4 |
| `ru/nbsp/mln` | Bind | да | 5 |
| `ru/nbsp/rubleKopek` | Bind | да | 5 |
| `common/nbsp/dpi` | Bind | да | 5 |
| `ru/nbsp/addr` | Bind | да | 6 |
| `ru/nbsp/page` | Bind | да | 6 |
| `ru/nbsp/see` | Bind | да | 6 |
| `ru/nbsp/ooo` | Bind | да | 6 |
| `ru/nbsp/beforeParticle` | Bind | да | 7 |
| `common/nbsp/afterShortWordByList` | Bind | да | 7 |
| `common/nbsp/beforeShortLastWord` | Bind | да | 8 |
| `common/nbsp/beforeShortLastNumber` | Bind | да | 8 |
| `ru/nbsp/afterNumberSign` | Bind | да | 9 |
| `common/nbsp/afterSectionMark` | Bind | да | 9 |
| `common/nbsp/afterParagraphMark` | Bind | да | 9 |
| `ru/nbsp/years` | Bind | да | 10 |
| `ru/nbsp/centuries` | Bind | да | 10 |
| `ru/nbsp/ps` | Bind | да | 10 |
| `ru/nbsp/m` | Bind | да | 10 |
| `common/nbsp/nowrap` | Bind | да | 11 |
| `ru/date/fromISO` | Bind | да | 12 |
| `ru/date/weekday` | Bind | да | 12 |
| `ru/money/currency` | Bind | **нет** | 13 |
| `ru/money/ruble` | Bind | **нет** | 13 |
| `ru/other/accent` | Bind | **нет** | 14 |
| `common/other/repeatWord` | Bind | **нет** | 14 |
| `ru/other/phone-number` | Bind | да | 15 |

Пять правил уходят в `Registry.OptIn` (они выключены в `Default` по своим причинам, а не
потому, что нормализуют письмо и не потому, что меняют смысл радикально):
`Common.Nbsp.ReplaceNbsp`, `Ru.Money.Currency`, `Ru.Money.Ruble`, `Ru.Other.Accent`,
`Common.Other.RepeatWord`.

- [ ] **Step 1: Красный тест на состав реестра**

```csharp
[Fact]
public void RegistryHasNinetySixRules()
    => Assert.Equal(96, RuleSet.All.Count);

[Fact]
public void EveryRuleNameParsesBack()
{
    foreach (RuleId rule in RuleSet.All)
    {
        Assert.True(RuleId.TryParse(rule.Name, out RuleId parsed), rule.Name);
        Assert.Equal(rule, parsed);
    }
}

[Fact]
public void BindPhaseMaskMatchesRegistry()
{
    int bind = RuleSet.All.Count(r => r.Phase == RulePhase.Bind);
    Assert.Equal(bind, RuleSet.BindPhase.Count);
    Assert.True(RuleSet.Default.Overlaps(RuleSet.BindPhase));
    Assert.False(RuleSet.None.Overlaps(RuleSet.BindPhase));
}

[Fact]
public void MoneyAndAccentAreOutOfDefault()
{
    Assert.False(RuleSet.Default.Contains(RuleId.Ru.Money.Ruble));
    Assert.False(RuleSet.Default.Contains(RuleId.Ru.Money.Currency));
    Assert.False(RuleSet.Default.Contains(RuleId.Ru.Other.Accent));
    Assert.False(RuleSet.Default.Contains(RuleId.Common.Other.RepeatWord));
    Assert.False(RuleSet.Default.Contains(RuleId.Common.Nbsp.ReplaceNbsp));
    Assert.True(RuleSet.Default.Contains(RuleId.Ru.Other.PhoneNumber));
    Assert.True(RuleSet.Default.Contains(RuleId.Common.Other.DelBom));
}
```

- [ ] **Step 2: Прогнать, убедиться, что не компилируется**

Run: `dotnet test tests/Typographer.Tests`
Expected: ошибки компиляции — свойств `RuleId.Ru.Money.*`, `RuleId.Ru.Other.*`,
`RuleId.Common.Other.*`, `RuleSet.BindPhase`, `RuleSet.Overlaps` не существует.

- [ ] **Step 3: Реестр**

31 запись в `RuleId.Registry` с индексами 66–96, по одной строке на правило, имена из
таблицы. Дерево типизированных свойств пополняется классами `Common.Other`, `Ru.Date`,
`Ru.Money`, `Ru.Other` и свойствами в существующих `Common.Nbsp`, `Ru.Nbsp`. У каждого
свойства XML-комментарий на русском: что делает и, если правило вне `Default`, почему.
Массив `Registry.All` пополняется теми же 31 значениями.

- [ ] **Step 4: Маска фазы и пересечение**

```csharp
// RuleSet.cs
/// <summary>Правила, включённые и здесь, и в переданном множестве, пересекаются.</summary>
/// <exception cref="ArgumentNullException"><paramref name="other"/> — null.</exception>
public bool Overlaps(RuleSet other)
{
    Throw.IfNull(other, nameof(other));
    return (_low & other._low) != 0 || (_high & other._high) != 0;
}

/// <summary>Все правила фазы Bind. Нужна фазе, чтобы не запускать проход впустую.</summary>
internal static RuleSet BindPhase { get; } = FromPhase(RulePhase.Bind);

private static RuleSet FromPhase(RulePhase phase)
{
    RuleSet set = None;
    foreach (RuleId rule in RuleId.Registry.All)
    {
        if (rule.Phase == phase)
        {
            set = set.With(rule);
        }
    }

    return set;
}
```

- [ ] **Step 5: Прогнать тесты**

Run: `dotnet test tests/Typographer.Tests`
Expected: PASS. Правила зарегистрированы, но ни одна фаза их ещё не читает — вывод
типографа не меняется, и все ранее написанные тесты остаются зелёными.

- [ ] **Step 6: Коммит**

```bash
rtk git add src/Typographer/Rules tests/Typographer.Tests/Rules/RuleSetTests.cs
rtk git commit -m "feat: реестр правил вырос до 96 записей"
```

---

### Task 2: Фаза Bind становится пишущим проходом

**Files:**
- Modify: `src/Typographer/Internal/WordBinder.cs` (целиком)
- Create: `src/Typographer/Internal/Bind/NbspRules.cs`
- Modify: `src/Typographer/HtmlTypograf.cs:66-105`
- Modify: `src/Typographer/TextTypograf.cs`
- Test: `tests/Typographer.Tests/Rules/NbspRulesTests.cs`, `tests/Typographer.Tests/Rules/BindAcrossMarkupTests.cs` (существующие, должны остаться зелёными)

**Interfaces:**
- Consumes: `CharBuffer.Write/PatchAt/Truncate/AsSpan/CharAt/Length`, `MarkupScanner`, `Segment`, `SegmentKind`, `RuleSet.Overlaps`, `RuleSet.BindPhase`, `Dictionaries.IsShortWord/IsAbbreviationPart`.
- Produces: `internal enum TokenKind { Word, Number, Mixed }`; `internal struct BindState` с полями `TokenStart, TokenLength, TokenOverflow, HasLetter, HasDigit, SpaceIndex, PrevLength, SafeFrom, GlueForward, Skip` и методами `Reset()`, `ResetToken()`, `readonly TokenKind Kind`; `WordBinder.IsEnabled(RuleSet)`, `WordBinder.Run(ReadOnlySpan<char>, RuleSet, ref CharBuffer)`, `WordBinder.RunDocument(ReadOnlySpan<char>, RuleSet, ref CharBuffer)`; `Bind.NbspRules.TryGlue(...)`.

#### Зачем переписывать

Сейчас фаза патчит буфер фазы `Scan` НА МЕСТЕ: она только меняет отдельные пробелы на
неразрывные, длина не меняется, и записанные ранее индексы остаются валидными. Одиннадцать
правил этого плана меняют длину: `г.г.` → `гг.`, `в. в.` → `вв.`, `1 руб.` → `1 ₽`,
`$100` → `100_$`, `№5` → `№~5`, `зАмок` → `за́мок`, «повтор повтор» → «повтор», телефон
целиком. Патчем на месте это не выражается.

Рассматривались две альтернативы, обе отклонены:

- **Оставить `Bind` на месте, а переписывающие правила отдать фазе `Scan`.** Фаза `Scan`
  умеет усекать и дописывать буфер (так работает американское тире). Но правила `ru/nbsp/years`
  и `ru/nbsp/centuries` делают И перезапись, И склейку пробела; правило оказалось бы разорвано
  между двумя фазами при одном `RuleId` с одним полем `Phase`. А правила, которым нужен левый
  токен, в фазе `Scan` не могут читать его через тег: `floor` запрещает читать буфер левее
  текущего узла — ровно тот дефект, который план 2a починил введением документного прохода.
- **Второй документный проход рядом с существующим.** Два прохода по словам, два места, где
  живёт логика токенов, и удвоенная цена сопровождения. Хуже во всём.

**Цена решения названа вслух:** фаза перестаёт быть почти бесплатной. Сейчас она читает
документ и пишет несколько символов; станет читать документ и писать документ. Четвёртого
буфера при этом НЕ появляется: буфер фазы `Prepare` после фазы `Scan` мёртв, и фаза `Bind`
пишет в него. Аллокаций не добавляется, добавляется один сквозной проход записи.

#### Модель прохода

Проход держит окно из ДВУХ токенов. Токен — последовательность букв, цифр, точек и дефисов;
точка и дефис токен не НАЧИНАЮТ (иначе `.дом` перестаёт быть коротким словом `дом`, а `-5`
становится словом). Токены копятся в два стековых буфера по 32 символа, которые на каждой
границе меняются местами — копирования нет, меняются местами два спана.

Три вида правил, каждый со своим соглашением:

```csharp
// Правило-склейка: решает про ПРОБЕЛ. Вперёд — выставив state.GlueForward (диспетчер
// напишет неразрывный вместо обычного); назад — запатчив state.SpaceIndex.
// Патч пробела разрешён на любой его позиции: пробел — это текст, и его позиция
// записана самим проходом. Через тег склейка назад работает: «<b>сло</b>во» — один
// токен, а пробел перед ним лежит в предыдущем узле и патчу доступен.
bool TryGlue(
    ReadOnlySpan<char> token, ReadOnlySpan<char> previous, char boundary,
    RuleSet rules, ref BindState state, ref CharBuffer buffer);

// Правило-перезапись: заменяет записанный токен другим. Усекает буфер до
// state.TokenStart и пишет замену, а ТАКЖЕ приводит в соответствие сам token и
// state.TokenLength — правила-склейки спрашиваются после и обязаны видеть новый токен.
// Работает только при state.TokenStart >= state.SafeFrom: левее лежит скопированная
// разметка, и усечение съело бы её байты (гарантия 3).
bool TryRewrite(
    Span<char> token, ReadOnlySpan<char> previous, char boundary,
    RuleSet rules, ref BindState state, ref CharBuffer buffer);

// Правило-символ: не выражается токеном, потому что читает вперёд через пробелы и
// скобки (телефон, валюта) или вставляет символ (знак номера). Пишет само и сообщает
// через state.Skip, сколько символов документа проглотило.
bool TryApply(
    ReadOnlySpan<char> document, int index, int end,
    RuleSet rules, ref BindState state, ref CharBuffer buffer);
```

- [ ] **Step 1: Красный тест на инварианты новой модели**

Существующие 11 тестов `NbspRulesTests` и `BindAcrossMarkupTests` — основная сеть
безопасности задачи, они уже написаны и должны остаться зелёными. Добавляются три случая на
то, чего в старой модели не было:

```csharp
// tests/Typographer.Tests/Rules/BindAcrossMarkupTests.cs
// Фаза стала пишущей: документ обязан выйти байт в байт, если ни одно правило не
// сработало. Проверяется на входе, где есть все виды сегментов сразу.
[Fact]
public void WritingPassCopiesEverythingItDoesNotChange()
{
    const string source = "<p title=\"a - b\">раз<!-- к --><code>x  y</code>два</p>";
    Assert.Equal(source, new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.None.With(RuleId.Ru.Nbsp.Initials),
    }).Process(source));
}

// Перезапись не имеет права уйти в разметку: токен, начатый до тега, для правила
// перезаписи недоступен. Здесь этот запрет проверяется на самом дешёвом наблюдаемом
// следствии — вывод совпадает с входом, разметка цела.
[Fact]
public void RewriteDoesNotReachBehindMarkup()
{
    const string source = "1990 г.<b>г.</b>";
    string result = new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.None.With(RuleId.Ru.Nbsp.Years),
    }).Process(source);
    Assert.Contains("<b>", result);
    Assert.Contains("</b>", result);
}

// Ради экономии буферов фаза пишет в буфер фазы Prepare. Если его забыли обнулить,
// вывод начнётся с копии подготовленного документа — тест ловит именно это.
[Fact]
public void RecycledBufferDoesNotLeakPreviousPhase()
    => Assert.Equal(
        $"в{Chars.Nbsp}доме",
        new TextTypograf(new TextOptions
        {
            Rules = RuleSet.None.With(RuleId.Common.Nbsp.AfterShortWord),
        }).Process("в доме"));
```

- [ ] **Step 2: Прогнать, убедиться, что падает**

Run: `dotnet test tests/Typographer.Tests`
Expected: `RewriteDoesNotReachBehindMarkup` компилируется только после Task 1 (правило
зарегистрировано) и падает или проходит вырожденно; два других теста проходят на старой
модели — они закрепляют то, что не должно сломаться. Зафиксировать вывод до правки.

- [ ] **Step 3: Состояние фазы**

```csharp
// src/Typographer/Internal/WordBinder.cs
/// <summary>Вид токена: из чего он состоит.</summary>
internal enum TokenKind
{
    /// <summary>Только буквы (плюс точки и дефисы): «дом», «т.», «из-за».</summary>
    Word,

    /// <summary>Только цифры (плюс точки и дефисы): «2026», «2018-10-10».</summary>
    Number,

    /// <summary>И буквы, и цифры: «5-й», «м2», «A4».</summary>
    Mixed,
}

/// <summary>Состояние фазы Bind, живущее сквозь текстовые сегменты документа.</summary>
internal struct BindState
{
    /// <summary>Позиция начала текущего токена в буфере ВЫВОДА.</summary>
    public int TokenStart;

    /// <summary>Сколько символов текущего токена накоплено в стековом буфере.</summary>
    public int TokenLength;

    /// <summary>Токен длиннее буфера: словарная проверка ему заведомо не нужна.</summary>
    public bool TokenOverflow;

    /// <summary>В токене есть буква.</summary>
    public bool HasLetter;

    /// <summary>В токене есть цифра.</summary>
    public bool HasDigit;

    /// <summary>Позиция пробела перед текущим токеном в буфере вывода, -1 — пробела нет.</summary>
    public int SpaceIndex;

    /// <summary>Длина предыдущего токена.</summary>
    public int PrevLength;

    /// <summary>Вид предыдущего токена: правила «число и слово» смотрят именно на него.</summary>
    public TokenKind PrevKind;

    /// <summary>
    /// Позиция в буфере вывода, левее которой переписывать нельзя: там лежит уже
    /// скопированная разметка или то, что записало символьное правило.
    /// </summary>
    public int SafeFrom;

    /// <summary>Пробел, закрывающий текущий токен, писать неразрывным.</summary>
    public bool GlueForward;

    /// <summary>Сколько ДОПОЛНИТЕЛЬНЫХ символов документа проглотило символьное правило.</summary>
    public int Skip;

    public BindState()
    {
        TokenStart = 0;
        TokenLength = 0;
        TokenOverflow = false;
        HasLetter = false;
        HasDigit = false;
        SpaceIndex = -1;
        PrevLength = 0;
        PrevKind = TokenKind.Word;
        SafeFrom = 0;
        GlueForward = false;
        Skip = 0;
    }

    /// <summary>Из чего состоит текущий токен.</summary>
    public readonly TokenKind Kind => HasLetter
        ? HasDigit ? TokenKind.Mixed : TokenKind.Word
        : TokenKind.Number;

    /// <summary>Граница, за которой ни токен, ни пробел перед ним не продолжаются.</summary>
    public void Reset()
    {
        ResetToken();
        SpaceIndex = -1;
        PrevLength = 0;
        GlueForward = false;
    }

    /// <summary>Токен закрыт: накопитель пуст, предыдущий токен остаётся.</summary>
    public void ResetToken()
    {
        TokenLength = 0;
        TokenOverflow = false;
        HasLetter = false;
        HasDigit = false;
    }
}
```

- [ ] **Step 4: Проход**

`WordBinder` переписывается целиком. `Run` и `RunDocument` по-прежнему делят одно тело
разбора — планом 2a установлено, что два тела разъезжаются. Отличие сигнатуры: обе точки
входа теперь принимают исходный документ и ПРИЁМНИК, а не правят буфер по `ref`.

```csharp
internal static class WordBinder
{
    /// <summary>
    /// Максимальная длина токена. Самое длинное словарное слово — «понедельник»
    /// (11 символов), самое длинное сокращение — «ПБОЮЛ». Тридцать два взяты с запасом;
    /// всё длиннее не короткое слово, не сокращение и не дата, и накопление прекращается.
    /// </summary>
    private const int MaxToken = 32;

    /// <summary>Хоть одно правило фазы включено, и проход имеет смысл запускать.</summary>
    public static bool IsEnabled(RuleSet rules) => rules.Overlaps(RuleSet.BindPhase);

    /// <summary>Фаза Bind по обычному тексту: весь вход — один текстовый сегмент.</summary>
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)
    {
        Span<char> token = stackalloc char[MaxToken];
        Span<char> previous = stackalloc char[MaxToken];
        var state = new BindState();

        BindSegment(source, 0, source.Length, rules, ref token, ref previous, ref state, ref buffer);
        FlushToken('\0', rules, ref token, ref previous, ref state, ref buffer);
    }

    /// <summary>
    /// Фаза Bind по документу: слово, разорванное СТРОЧНЫМ тегом, остаётся одним словом.
    /// «&lt;b&gt;сло&lt;/b&gt;во» — одно слово, и словарные правила обязаны видеть его
    /// целиком; иначе «во» выглядит коротким словом и получает неразрывный пробел, которого
    /// в тексте нет. Блочный тег и защищённая зона слово, наоборот, завершают.
    /// </summary>
    public static void RunDocument(ReadOnlySpan<char> document, RuleSet rules, ref CharBuffer buffer)
    {
        Span<char> token = stackalloc char[MaxToken];
        Span<char> previous = stackalloc char[MaxToken];
        var state = new BindState();
        var scanner = new MarkupScanner(document);

        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = document.Slice(segment.Start, segment.Length);

            if (segment.Kind == SegmentKind.Markup && !segment.IsBlock)
            {
                // Строчный тег для слова прозрачен: ни буквы, ни границы он не даёт. Но
                // байты тега уже в выводе между частями слова — переписывать токен через
                // них нельзя, и SafeFrom это запрещает.
                buffer.Write(slice);
                state.SafeFrom = buffer.Length;
                continue;
            }

            if (segment.Kind != SegmentKind.Text)
            {
                FlushToken('\0', rules, ref token, ref previous, ref state, ref buffer);
                buffer.Write(slice);
                state.Reset();
                state.SafeFrom = buffer.Length;
                continue;
            }

            BindSegment(
                document, segment.Start, segment.Start + segment.Length,
                rules, ref token, ref previous, ref state, ref buffer);
        }

        FlushToken('\0', rules, ref token, ref previous, ref state, ref buffer);
    }

    /// <summary>Разбирает один текстовый сегмент, продолжая токен, начатый в предыдущем.</summary>
    private static void BindSegment(
        ReadOnlySpan<char> document, int start, int end, RuleSet rules,
        ref Span<char> token, ref Span<char> previous, ref BindState state, ref CharBuffer buffer)
    {
        for (int i = start; i < end; i++)
        {
            char c = document[i];

            if (IsTokenChar(c) && (state.TokenLength > 0 || c is not ('.' or '-')))
            {
                if (state.TokenLength == 0)
                {
                    state.TokenStart = buffer.Length;
                }

                if (state.TokenLength < token.Length)
                {
                    token[state.TokenLength++] = c;
                    state.HasLetter |= char.IsLetter(c);
                    state.HasDigit |= char.IsDigit(c);
                }
                else
                {
                    state.TokenOverflow = true;
                }

                buffer.Write(c);
                continue;
            }

            FlushToken(c, rules, ref token, ref previous, ref state, ref buffer);

            if (c == ' ' || c == Chars.Nbsp)
            {
                // Позиция пробела запоминается ДО записи: правило-склейка следующего
                // токена патчит именно её.
                state.SpaceIndex = buffer.Length;
                buffer.Write(state.GlueForward ? Chars.Nbsp : c);
                state.GlueForward = false;
                continue;
            }

            buffer.Write(c);
            state.SpaceIndex = -1;
            state.GlueForward = false;
        }
    }

    /// <summary>
    /// Токен закрыт символом <paramref name="boundary"/> (ноль — концом сегмента или
    /// документа): спрашиваются правила, затем окно сдвигается на один токен.
    /// </summary>
    private static void FlushToken(
        char boundary, RuleSet rules,
        ref Span<char> token, ref Span<char> previous, ref BindState state, ref CharBuffer buffer)
    {
        if (state.TokenLength == 0)
        {
            return;
        }

        // Перезаписи спрашиваются ПЕРВЫМИ: склейка обязана видеть уже исправленный токен.
        // «1990 г.г.» — сперва «гг.», и только потом решение о неразрывном пробеле перед ним.
        _ = RewriteRules.TryRewrite(token, previous.Slice(0, state.PrevLength), boundary, rules, ref state, ref buffer);

        _ = NbspRules.TryGlue(
            token.Slice(0, state.TokenLength), previous.Slice(0, state.PrevLength),
            boundary, rules, ref state, ref buffer);

        // Окно сдвигается сменой спанов, а не копированием: спан — два машинных слова.
        Span<char> finished = token;
        token = previous;
        previous = finished;
        state.PrevLength = state.TokenLength;
        state.PrevKind = state.Kind;
        state.ResetToken();
    }

    /// <summary>
    /// Символ продолжает токен. Точка и дефис входят, потому что живут ВНУТРИ токена —
    /// «т. д.», «из-за», «2018-10-10», — но токен не начинают: решение об этом принимает
    /// вызывающий код.
    /// </summary>
    private static bool IsTokenChar(char c)
        => char.IsLetter(c) || char.IsDigit(c) || c is '.' or '-';
}
```

Правила-перезаписи в этой задаче ещё нет — `RewriteRules.TryRewrite` создаётся заглушкой,
возвращающей `false`, с XML-комментарием «правила добавляют задачи 10–14». Заглушка честнее
условной компиляции: диспетчер уже вызывает её в правильном месте и в правильном порядке.

- [ ] **Step 5: Существующие три правила переезжают в `NbspRules`**

```csharp
// src/Typographer/Internal/Bind/NbspRules.cs
/// <summary>Правила неразрывных пробелов фазы Bind.</summary>
/// <remarks>
/// Соглашение о правиле-склейке: <c>true</c> означает «решение о пробеле принято», склейка
/// вперёд выражается через <see cref="BindState.GlueForward"/>, склейка назад — патчем
/// <see cref="BindState.SpaceIndex"/>. Читать буфер левее <see cref="BindState.SafeFrom"/>
/// правило-склейка не обязано вовсе: ему хватает двух токенов и символа границы.
/// </remarks>
internal static class NbspRules
{
    public static bool TryGlue(
        ReadOnlySpan<char> token, ReadOnlySpan<char> previous, char boundary,
        RuleSet rules, ref BindState state, ref CharBuffer buffer)
    {
        bool hasDot = token[token.Length - 1] == '.';
        ReadOnlySpan<char> letters = hasDot ? token.Slice(0, token.Length - 1) : token;
        bool initial = rules.Contains(RuleId.Ru.Nbsp.Initials) && !state.TokenOverflow && IsInitial(token);

        // Инициал связывает себя не только со следующим словом, но и с предыдущим —
        // «Пушкин А.» нуждается в неразрывном пробеле по обе стороны от «А.».
        if (initial && state.SpaceIndex >= 0)
        {
            buffer.PatchAt(state.SpaceIndex, Chars.Nbsp);
        }

        bool glue = !state.TokenOverflow && boundary == ' '
            && ((rules.Contains(RuleId.Common.Nbsp.AfterShortWord)
                    && !hasDot && state.Kind == TokenKind.Word && Dictionaries.IsShortWord(letters))
                || (rules.Contains(RuleId.Ru.Nbsp.Abbr)
                    && hasDot && Dictionaries.IsAbbreviationPart(letters))
                || initial);

        if (glue)
        {
            state.GlueForward = true;
        }

        return glue || initial;
    }

    /// <summary>Инициал — одна прописная буква с точкой: «А.».</summary>
    private static bool IsInitial(ReadOnlySpan<char> token)
        => token.Length == 2 && token[1] == '.' && char.IsUpper(token[0]);
}
```

Старая привязка инициала в конце ввода (`BindTrailingInitial`) отдельным методом больше не
нужна: `FlushToken` вызывается и на конце сегмента, и на конце документа с `boundary == '\0'`,
и склейка назад от `boundary` не зависит. Тесты `InitialAtEndOfInput`, `InitialBeforeComma`
и `InitialBeforeLineBreak` — проверка именно этого.

Обрати внимание на `state.Kind == TokenKind.Word` в условии короткого слова: цифры стали
частью токена, и без проверки вида `100 рублей` склеивалось бы правилом короткого слова,
а не правилом числа. Результат тот же, но правило соврало бы в справочнике.

- [ ] **Step 6: Конвейер отдаёт фазе буфер фазы Prepare**

```csharp
// src/Typographer/HtmlTypograf.cs, метод Run
Preparer.RunDocument(source, _options.Rules, ref prepared);
bool canWrapParagraphs = TextScanner.RunDocument(prepared.AsSpan(), _options.Rules, ref scanned);

// Фаза Bind стала пишущей, и ей нужен приёмник. Четвёртого буфера не заводится: буфер
// фазы Prepare после фазы Scan мёртв, и Bind пишет в него. Когда ни одно правило фазы
// не включено, проход не запускается вовсе — Layout читает буфер фазы Scan.
ref CharBuffer bound = ref scanned;
if (WordBinder.IsEnabled(_options.Rules))
{
    prepared.Truncate(0);
    WordBinder.RunDocument(scanned.AsSpan(), _options.Rules, ref prepared);
    bound = ref prepared;
}
```

Дальше по тексту метода `scanned.AsSpan()` заменяется на `bound.AsSpan()` — и в ветке
нормализации, и в вызове `LayoutWriter.Run`. В `TextTypograf.Run` та же правка; там
`WordBinder.Run` вместо `RunDocument`.

- [ ] **Step 7: Прогнать все тесты**

Run: `dotnet test tests/Typographer.Tests`
Expected: PASS, 549 + 4 теста. Если падает `NobrChainDoesNotCrossTag` или тесты корпуса —
причина в порядке записи пробела: пробел пишется ПОСЛЕ вызова правил, но его позиция
запоминается ДО записи.

- [ ] **Step 8: Коммит**

```bash
rtk git add src/Typographer tests/Typographer.Tests
rtk git commit -m "refactor: фаза Bind стала пишущим проходом с окном из двух токенов"
```

---

### Task 3: Фаза Prepare — метка порядка байт и снятие неразрывных пробелов

**Files:**
- Modify: `src/Typographer/Internal/Preparer.cs`
- Test: `tests/Typographer.Tests/Rules/PrepareTests.cs`

**Interfaces:**
- Consumes: `Preparer.Run(ReadOnlySpan<char>, bool, RuleSet, ref CharBuffer, bool)`, `Chars.Bom`, `Chars.Nbsp`.
- Produces: поведение фазы `Prepare` под управлением двух новых правил. Сигнатуры не меняются.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `common/other/delBOM` | `﻿текст` | `текст` | JS-typograf | да |
| `common/other/delBOM` (выключено) | `﻿текст` | `﻿текст` | — | — |
| `common/nbsp/replaceNbsp` | `в_доме` | `в доме` → фаза Bind вернёт `в_доме` | JS-typograf | **нет** |
| `common/nbsp/replaceNbsp` (выключено) | `в_доме` | `в_доме` | — | — |

Удаление метки порядка байт уже написано и работает; задача — подчинить его правилу, чтобы
`RuleSet.None.With(...)` вело себя предсказуемо и правило попало в справочник. Внутренний
`U+FEFF` по-прежнему не удаляется никогда: он может разделять символы, которые после
удаления станут тегом или сущностью.

Снятие неразрывных пробелов — обратная операция к тому, что делает фаза `Bind`, и работает
только вместе с ней: включив `replaceNbsp` без правил фазы `Bind`, вызывающий получит текст
без неразрывных пробелов вовсе. Так и задумано, XML-комментарий обязан это сказать.

- [ ] **Step 1: Красные тесты** — четыре случая из таблицы, `[Fact]` на каждый; неразрывный
      пробел в тесте только через `Chars.Nbsp`.
- [ ] **Step 2: Прогнать** — Expected: FAIL на `delBOM` при выключенном правиле (метка
      удаляется безусловно) и на обоих случаях `replaceNbsp`.
- [ ] **Step 3: Реализация** — в `Preparer.Run` условие `isDocumentStart` дополняется
      проверкой правила; в цикле добавляется ветка замены `Chars.Nbsp` на пробел.
- [ ] **Step 4: Прогнать** — Expected: PASS.
- [ ] **Step 5: Коммит** — `feat: правила delBOM и replaceNbsp фазы Prepare`.

---

### Task 4: Число и слово

**Files:**
- Modify: `src/Typographer/Internal/Bind/NbspRules.cs`
- Modify: `src/Typographer/Internal/Dictionaries.cs`
- Test: `tests/Typographer.Tests/Rules/NbspNumberTests.cs` (создать)

**Interfaces:**
- Consumes: `BindState.Kind/PrevLength/SpaceIndex/GlueForward/TokenOverflow`, `Dictionaries.IsMonth`.
- Produces: `Dictionaries.IsYearAbbreviation(ReadOnlySpan<char>)` — «г.», «гг.», «г», «гг»;
  ветви правил `afterNumber`, `dayMonth`, `year` в `NbspRules.TryGlue`.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `common/nbsp/afterNumber` | `10 кг` | `10_кг` | ГОСТ 9.4 | да |
| `common/nbsp/afterNumber` | `100 км/ч` | `100_км/ч` | ГОСТ 9.4 | да |
| `common/nbsp/afterNumber` | `30 мин.` | `30_мин.` | оракул | да |
| `common/nbsp/afterNumber` | `дом 5` | `дом 5` (число слева, не справа) | — | — |
| `common/nbsp/afterNumber` | `2026 2027` | `2026 2027` (справа не слово) | — | — |
| `ru/nbsp/dayMonth` | `5 января` | `5_января` | Мильчин | да |
| `ru/nbsp/dayMonth` | `31 декабря` | `31_декабря` | Мильчин | да |
| `ru/nbsp/dayMonth` | `5 январь` | `5_январь` (в словаре оба падежа) | Мильчин | да |
| `ru/nbsp/year` | `2012 г.` | `2012_г.` | Мильчин | да |
| `ru/nbsp/year` | `1990 гг.` | `1990_гг.` | Мильчин | да |
| `ru/nbsp/year` | `дом г.` | `дом г.` (слева не число) | — | — |

Все три правила делают одно и то же действие — склейку НАЗАД, патч `state.SpaceIndex`, —
и отличаются только предикатом. `afterNumber` перекрывает два других полностью, и при
включённом `Default` разницы в выводе нет; отдельные правила нужны для того, кто выключил
`afterNumber` (так делает пресет `Typograf`, см. задачу 16) и хочет оставить даты и годы
склеенными. Из-за перекрытия правила спрашиваются подряд, а не через `||`: любое
сработавшее ставит патч, остальные молча увидят, что пробел уже неразрывный.

- [ ] **Step 1: Красный тест**

```csharp
// tests/Typographer.Tests/Rules/NbspNumberTests.cs
using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class NbspNumberTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypograf(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Theory]
    [InlineData("10 кг", "10 кг")]
    [InlineData("100 км/ч", "100 км/ч")]
    [InlineData("30 мин.", "30 мин.")]
    [InlineData("дом 5", "дом 5")]
    [InlineData("2026 2027", "2026 2027")]
    public void NumberBindsToFollowingWord(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Common.Nbsp.AfterNumber));

    [Theory]
    [InlineData("5 января", "5 января")]
    [InlineData("31 декабря", "31 декабря")]
    [InlineData("5 яблок", "5 яблок")]
    public void DayBindsToMonthWithoutAfterNumber(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.DayMonth));

    [Theory]
    [InlineData("2012 г.", "2012 г.")]
    [InlineData("1990 гг.", "1990 гг.")]
    [InlineData("дом г.", "дом г.")]
    [InlineData("5 г.", "5 г.")]
    public void YearBindsToAbbreviation(string source, string expected)
        => Assert.Equal(expected, Run(source, RuleId.Ru.Nbsp.Year));
}
```

Литерал ` ` здесь допустим и предпочтителен: в `[InlineData]` интерполяция
недоступна, а escape-последовательность в ревью видна, в отличие от самого символа.

- [ ] **Step 2: Прогнать, убедиться, что падает**

Run: `dotnet test tests/Typographer.Tests --filter NbspNumberTests`
Expected: FAIL — все склейки не происходят, выход равен входу.

- [ ] **Step 3: Словарь**

```csharp
// src/Typographer/Internal/Dictionaries.cs
/// <summary>Сокращение года: «г.», «гг.» и те же без точки.</summary>
public static bool IsYearAbbreviation(ReadOnlySpan<char> word)
    => Is(word, "г.") || Is(word, "гг.") || Is(word, "г") || Is(word, "гг");

/// <summary>Сравнение со образцом без учёта регистра и без аллокаций.</summary>
private static bool Is(ReadOnlySpan<char> word, string sample)
{
    if (word.Length != sample.Length)
    {
        return false;
    }

    for (int i = 0; i < sample.Length; i++)
    {
        if (char.ToLowerInvariant(word[i]) != sample[i])
        {
            return false;
        }
    }

    return true;
}
```

- [ ] **Step 4: Правила**

Блок встаёт в `TryGlue` СРАЗУ ПОСЛЕ вычисления `hasDot` и `letters` и до решения об инициале:
предикаты правил читают `letters`, а порядок действий с пробелом значения не имеет — склейка
назад патчит уже записанную позицию.

```csharp
// src/Typographer/Internal/Bind/NbspRules.cs
// Склейка НАЗАД по предыдущему токену: число слева, слово справа. Три правила делают
// одно действие и различаются предикатом, поэтому спрашиваются подряд.
if (state.SpaceIndex >= 0 && state.PrevKind == TokenKind.Number && state.Kind != TokenKind.Number)
{
    bool bindBack =
        rules.Contains(RuleId.Common.Nbsp.AfterNumber)
        || (rules.Contains(RuleId.Ru.Nbsp.DayMonth) && Dictionaries.IsMonth(letters))
        || (rules.Contains(RuleId.Ru.Nbsp.Year) && Dictionaries.IsYearAbbreviation(token));

    if (bindBack)
    {
        buffer.PatchAt(state.SpaceIndex, Chars.Nbsp);
    }
}
```

`BindState` пополняется полем `PrevKind` (вид предыдущего токена) — `FlushToken`
присваивает его вместе с `PrevLength`. Условие `state.Kind != TokenKind.Number` нужно, чтобы
`2026 2027` не склеивалось: перечисление чисел рвать можно, а число и слово — нет.

- [ ] **Step 5: Прогнать тесты**

Run: `dotnet test tests/Typographer.Tests`
Expected: PASS. Тесты корпуса (`HardCases`) могут потребовать правки ожиданий: `Default`
теперь склеивает число со словом. Каждое изменение эталона проверить глазами по таблице —
неразрывный пробел появился там, где его требует ГОСТ 9.4, и только там.

- [ ] **Step 6: Коммит**

```bash
rtk git add src/Typographer tests/Typographer.Tests
rtk git commit -m "feat: неразрывный пробел между числом и словом, датой и годом"
```

---

### Task 5: Число и словарное сокращение

**Files:**
- Modify: `src/Typographer/Internal/Bind/NbspRules.cs`
- Modify: `src/Typographer/Internal/Dictionaries.cs`
- Test: `tests/Typographer.Tests/Rules/NbspNumberTests.cs`

**Interfaces:**
- Consumes: то же, что задача 4.
- Produces: `Dictionaries.IsMagnitude` («тыс.», «млн», «млрд», «трлн» с точкой и без),
  `Dictionaries.IsMoneyAbbreviation` («руб.», «коп.», «р.», «к.»), `Dictionaries.IsResolution` («dpi», «lpi», «ppi»).

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `ru/nbsp/mln` | `10 млн` | `10_млн` | Мильчин | да |
| `ru/nbsp/mln` | `5 тыс. рублей` | `5_тыс. рублей` | Мильчин | да |
| `ru/nbsp/mln` | `3 млрд` | `3_млрд` | Мильчин | да |
| `ru/nbsp/mln` | `много млн` | `много млн` (слева не число) | — | — |
| `ru/nbsp/rubleKopek` | `100 руб.` | `100_руб.` | оракул | да |
| `ru/nbsp/rubleKopek` | `50 коп.` | `50_коп.` | оракул | да |
| `ru/nbsp/rubleKopek` | `5 000 р.` | `5 000_р.` | оракул | да |
| `common/nbsp/dpi` | `600 dpi` | `600_dpi` | JS-typograf | да |
| `common/nbsp/dpi` | `150 lpi` | `150_lpi` | JS-typograf | да |
| `common/nbsp/dpi` | `600 dpiX` | `600 dpiX` (не слово из словаря) | — | — |

Правила — та же склейка назад с другим словарём; вся задача в словарях и в отрицательных
случаях. `5 000 р.` в таблице показывает границу ответственности: пробел между разрядами
ставит `common/number/digitGrouping` (вне `Default`), и склейка его не трогает.

- [ ] **Step 1: Красный тест** — `[Theory]` на каждое правило по строкам таблицы.
- [ ] **Step 2: Прогнать** — Expected: FAIL, склеек нет.
- [ ] **Step 3: Три словаря** в `Dictionaries` по образцу `IsYearAbbreviation`.
      `IsMagnitude`: `тыс.`, `тыс`, `млн`, `млн.`, `млрд`, `млрд.`, `трлн`, `трлн.`.
      `IsMoneyAbbreviation`: `руб.`, `руб`, `коп.`, `коп`, `р.`, `к.`, `долл.`, `долл`.
      `IsResolution`: `dpi`, `lpi`, `ppi` (латиница, регистр не значим).
- [ ] **Step 4: Три ветви** в условии `bindBack` задачи 4.
- [ ] **Step 5: Прогнать** — Expected: PASS.
- [ ] **Step 6: Коммит** — `feat: неразрывный пробел перед «млн», «руб.» и «dpi»`.

---

### Task 6: Сокращение и следующее слово

**Files:**
- Modify: `src/Typographer/Internal/Bind/NbspRules.cs`
- Modify: `src/Typographer/Internal/Dictionaries.cs`
- Test: `tests/Typographer.Tests/Rules/NbspAbbreviationTests.cs` (создать)

**Interfaces:**
- Consumes: `BindState.GlueForward`, `Dictionaries` как в задачах 4–5.
- Produces: `Dictionaries.IsAddressAbbreviation`, `Dictionaries.IsPageAbbreviation`,
  `Dictionaries.IsReferenceAbbreviation`, `Dictionaries.IsOrganization`.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `ru/nbsp/addr` | `ул. Ленина` | `ул._Ленина` | ГОСТ 9.5, Мильчин | да |
| `ru/nbsp/addr` | `г. Москва` | `г._Москва` | ГОСТ 9.5 | да |
| `ru/nbsp/addr` | `д. 5, кв. 12` | `д._5, кв._12` | оракул | да |
| `ru/nbsp/addr` | `корп. 2` | `корп._2` | Мильчин | да |
| `ru/nbsp/addr` | `или г. далее` | `или г._далее` (ложное срабатывание принято) | — | да |
| `ru/nbsp/page` | `стр. 15` | `стр._15` | Мильчин | да |
| `ru/nbsp/page` | `рис. 3` | `рис._3` | Мильчин | да |
| `ru/nbsp/page` | `гл. 2` | `гл._2` | Мильчин | да |
| `ru/nbsp/see` | `см. рис. 3` | `см._рис._3` | Мильчин | да |
| `ru/nbsp/see` | `им. Пушкина` | `им._Пушкина` | Мильчин | да |
| `ru/nbsp/ooo` | `ООО Ромашка` | `ООО_Ромашка` | JS-typograf | да |
| `ru/nbsp/ooo` | `НИИ Точмаш` | `НИИ_Точмаш` | JS-typograf | да |
| `ru/nbsp/ooo` | `ООО, а также` | `ООО, а также` (границей не пробел) | — | — |

Впервые в плане используется склейка ВПЕРЁД: решение принимается по ТЕКУЩЕМУ токену, а
неразрывным становится пробел, который диспетчер запишет следующим шагом. Отличие от склейки
назад не косметическое: пробел вперёд ещё не записан, патчить нечего, и правило обязано
выставить `state.GlueForward`, а не звать `PatchAt`.

Ложное срабатывание на `или г. далее` признано допустимым осознанно: отличить сокращение
«город» от слова «года» в такой позиции без разбора смысла невозможно, а цена ошибки —
неразрывный пробел там, где он не нужен. Так же поступает JS-typograf.

Оракул Лебедева `ул. Ленина` и `г. Москва` НЕ склеивает — расхождение внесено в решение 2
шапки плана и в пресет `Lebedev` задачей 16.

- [ ] **Step 1: Красный тест** — `[Theory]` по строкам таблицы, по правилу на метод.
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: Четыре словаря.** `AddressAbbr`: `г.`, `обл.`, `край`, `р-н`, `ул.`, `пр.`,
      `пр-т`, `пер.`, `пл.`, `наб.`, `бул.`, `ш.`, `д.`, `корп.`, `стр.`, `кв.`, `оф.`,
      `под.`, `эт.`, `пос.`, `с.`, `дер.`, `ст.`, `мкр.`. `PageAbbr`: `стр.`, `с.`, `гл.`,
      `рис.`, `илл.`, `табл.`, `п.`, `пп.`, `ч.`, `т.`. `ReferenceAbbr`: `см.`, `им.`,
      `ср.`, `напр.`. `Organizations`: `ООО`, `ОАО`, `ЗАО`, `ПАО`, `АО`, `НИИ`, `ПБОЮЛ`,
      `ИП`, `НПО`, `КБ` (регистр значим: `ООО` — только прописными).
- [ ] **Step 4: Ветвь склейки вперёд** в `NbspRules.TryGlue`.
- [ ] **Step 5: Прогнать** — Expected: PASS. Проверить, что `с.` в двух словарях сразу
      (село и страница) не приводит к разному поведению — действие у правил одно.
- [ ] **Step 6: Коммит** — `feat: неразрывный пробел после адресных и ссылочных сокращений`.

---

### Task 7: Частицы и служебные слова

**Files:**
- Modify: `src/Typographer/Internal/Bind/NbspRules.cs`
- Modify: `src/Typographer/Internal/Dictionaries.cs`
- Test: `tests/Typographer.Tests/Rules/NbspParticleTests.cs` (создать)

**Interfaces:**
- Consumes: `BindState.SpaceIndex/GlueForward/Kind`.
- Produces: `Dictionaries.IsParticle`, `Dictionaries.IsFunctionWord`.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `ru/nbsp/beforeParticle` | `так ли это` | `так_ли это` | ГОСТ 9.4 | да |
| `ru/nbsp/beforeParticle` | `он же` | `он_же` | ГОСТ 9.4 | да |
| `ru/nbsp/beforeParticle` | `если бы` | `если_бы` | ГОСТ 9.4 | да |
| `ru/nbsp/beforeParticle` | `пошёл бы я` | `пошёл_бы я` | ГОСТ 9.4 | да |
| `ru/nbsp/beforeParticle` | `лишь бытие` | `лишь бытие` (не частица, а слово) | — | — |
| `common/nbsp/afterShortWordByList` | `через дорогу` | `через_дорогу` | ГОСТ 9.4 | да |
| `common/nbsp/afterShortWordByList` | `между нами` | `между_нами` | ГОСТ 9.4 | да |
| `common/nbsp/afterShortWordByList` | `чтобы успеть` | `чтобы_успеть` | ГОСТ 9.4 | да |
| `common/nbsp/afterShortWordByList` | `дерево стоит` | `дерево стоит` (не в списке) | — | — |

Частица склеивается НАЗАД (пробел перед ней), служебное слово — ВПЕРЁД (пробел после него);
в одной задаче они потому, что бьются об одну и ту же ловушку — слово, которое начинается с
частицы. `бытие` начинается на `бы`, `лишь` — на `ли`. Правило обязано сверять токен ЦЕЛИКОМ,
а не его начало. Тест `лишь бытие` — страховка именно от этого.

Список служебных слов держится коротким и закрытым: предлоги и союзы длиной четыре-шесть
букв, которые нельзя оставлять в конце строки. Слова до трёх букв уже покрыты правилом
`afterShortWord`, и дублировать их в списке не нужно.

- [ ] **Step 1: Красный тест** — по строкам таблицы.
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: Два словаря.** `Particles`: `ли`, `ль`, `же`, `ж`, `бы`, `б`.
      `FunctionWords`: `без`, `близ`, `вместо`, `вопреки`, `перед`, `после`, `около`,
      `среди`, `сквозь`, `через`, `между`, `кроме`, `около`, `чтобы`, `когда`, `хотя`,
      `если`, `либо`, `или`, `ибо`, `итак`, `зато`, `даже`, `лишь`, `пусть`, `будто`.
- [ ] **Step 4: Две ветви** в `TryGlue`.
- [ ] **Step 5: Прогнать** — Expected: PASS.
- [ ] **Step 6: Коммит** — `feat: неразрывный пробел вокруг частиц и служебных слов`.

---

### Task 8: Последнее слово предложения

**Files:**
- Modify: `src/Typographer/Internal/Bind/NbspRules.cs`
- Test: `tests/Typographer.Tests/Rules/NbspLastWordTests.cs` (создать)

**Interfaces:**
- Consumes: `BindState.SpaceIndex/Kind/TokenLength`, символ границы `boundary`.
- Produces: ветви `beforeShortLastWord` и `beforeShortLastNumber` в `NbspRules.TryGlue`.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `common/nbsp/beforeShortLastWord` | `Дом стоит там.` | `Дом стоит_там.` | ГОСТ 9.4 | да |
| `common/nbsp/beforeShortLastWord` | `Кто это был?` | `Кто это_был?` | ГОСТ 9.4 | да |
| `common/nbsp/beforeShortLastWord` | `Беги вон!` | `Беги_вон!` | ГОСТ 9.4 | да |
| `common/nbsp/beforeShortLastWord` | `Он ушёл домой.` | `Он ушёл домой.` (слово длинное) | — | — |
| `common/nbsp/beforeShortLastWord` | `Там был дом, и всё.` | `Там был_дом, и всё.` (запятая не конец) | — | — |
| `common/nbsp/beforeShortLastNumber` | `Всего 5.` | `Всего_5.` | ГОСТ 9.4 | да |
| `common/nbsp/beforeShortLastNumber` | `Итого 42!` | `Итого_42!` | ГОСТ 9.4 | да |
| `common/nbsp/beforeShortLastNumber` | `Год 2026.` | `Год 2026.` (больше двух цифр) | — | — |

Конец предложения распознаётся так: граница токена — `!`, `?` или многоточие `…`, либо сам
токен оканчивается точкой (точка входит в токен), либо токен закрыт концом документа. Пятая
строка таблицы — отрицательный случай на запятую: она границу предложения не образует, и
короткое слово перед ней склеивается не этим правилом, а `afterShortWord` (пробел ПОСЛЕ
короткого слова `был`), поэтому в ожидании стоит `был_дом`, а не `был дом`.

Ловушка, которую надо проверить отдельно: `и т. д.` — токен `д.` короткий и оканчивается
точкой, то есть правило сработает и склеит пробел перед ним. Это ровно то, что делает
`ru/nbsp/abbr`, действие совпадает, конфликта нет. Тест на `и т. д.` при включённом только
`beforeShortLastWord` фиксирует это как ожидаемое.

- [ ] **Step 1: Красный тест** — по строкам таблицы плюс случай `и т. д.`.
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: Две ветви** с общим предикатом `IsSentenceEnd(char boundary, ReadOnlySpan<char> token)`.
- [ ] **Step 4: Прогнать** — Expected: PASS; тесты корпуса обновить по глазам.
- [ ] **Step 5: Коммит** — `feat: неразрывный пробел перед последним коротким словом`.

---

### Task 9: Узкий неразрывный пробел после знака

**Files:**
- Create: `src/Typographer/Internal/Bind/MarkRules.cs`
- Modify: `src/Typographer/Internal/WordBinder.cs` (вызов символьного правила)
- Modify: `src/Typographer/Internal/Chars.cs`
- Test: `tests/Typographer.Tests/Rules/MarkRulesTests.cs` (создать)

**Interfaces:**
- Consumes: `BindState.Skip/SafeFrom`, `Chars.NarrowNbsp`.
- Produces: `Chars.Section = '§'`, `Chars.Pilcrow = '¶'`; `MarkRules.TryApply(ReadOnlySpan<char> document, int index, int end, RuleSet rules, ref BindState state, ref CharBuffer buffer)` — первое символьное правило фазы, соглашение о котором задано задачей 2.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `ru/nbsp/afterNumberSign` | `№ 5` | `№~5` | ГОСТ 16.4 | да |
| `ru/nbsp/afterNumberSign` | `№5` | `№~5` | ГОСТ 16.4 | да |
| `ru/nbsp/afterNumberSign` | `№~5` | `№~5` (идемпотентность) | гарантия 5 | да |
| `ru/nbsp/afterNumberSign` | `№ дома` | `№~дома` | ГОСТ 16.4 | да |
| `ru/nbsp/afterNumberSign` | `№` | `№` (справа ничего) | — | — |
| `ru/nbsp/afterNumberSign` | `№, потом` | `№, потом` (справа знак препинания) | — | — |
| `common/nbsp/afterSectionMark` | `§ 3` | `§~3` | ГОСТ 16.4 | да |
| `common/nbsp/afterSectionMark` | `§3` | `§~3` | ГОСТ 16.4 | да |
| `common/nbsp/afterParagraphMark` | `¶ 3` | `¶_3` | JS-typograf | да |
| `common/nbsp/afterParagraphMark` | `¶3` | `¶_3` | JS-typograf | да |

Правило ВСТАВЛЯЕТ символ, которого во входе не было, — первое такое в плане. Из-за этого
оно символьное: токеном знак номера не выражается, а решение зависит от того, что стоит
справа в документе. Реализация: на знаке пишется сам знак и узкий неразрывный пробел, а
если справа во входе стоял обычный или неразрывный пробел, он проглатывается через
`state.Skip`. Пробел, съеденный так, из вывода не исчезает — его место занимает узкий.

Знак абзаца `¶` получает ОБЫЧНЫЙ неразрывный пробел, а не узкий: узкая отбивка в ГОСТ 16.4
предписана знакам номера и параграфа, про знак абзаца там нет ничего, а JS-typograf ставит
обычный. Разнобой умышленный и задокументированный в XML-комментарии.

- [ ] **Step 1: Красный тест** — по строкам таблицы; узкий пробел только через
      `Chars.NarrowNbsp`, в `[InlineData]` — ` `.
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: `MarkRules`** и вызов из `BindSegment` перед записью границы.
- [ ] **Step 4: Прогнать** — Expected: PASS. Отдельно прогнать `IdempotencyTests`: правило
      со вставкой — главный источник разрывов идемпотентности.
- [ ] **Step 5: Коммит** — `feat: узкий неразрывный пробел после знаков номера и параграфа`.

---

### Task 10: Перезапись токена

**Files:**
- Create: `src/Typographer/Internal/Bind/RewriteRules.cs` (заменяет заглушку задачи 2)
- Modify: `src/Typographer/Internal/Chars.cs`
- Test: `tests/Typographer.Tests/Rules/RewriteRulesTests.cs` (создать)

**Interfaces:**
- Consumes: `BindState.TokenStart/SafeFrom/TokenLength/PrevLength/SpaceIndex`, `CharBuffer.Truncate/Write`.
- Produces: `Chars.Square = '²'`, `Chars.Cube = '³'`; рабочая реализация
  `RewriteRules.TryRewrite(Span<char> token, ReadOnlySpan<char> previous, char boundary, RuleSet rules, ref BindState state, ref CharBuffer buffer)`.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `ru/nbsp/years` | `1990 г.г.` | `1990_гг.` | Мильчин | да |
| `ru/nbsp/years` | `1990 гг.` | `1990_гг.` | Мильчин | да |
| `ru/nbsp/years` | `г.г.` без числа слева | `гг.` | Мильчин | да |
| `ru/nbsp/centuries` | `XIX в. в.` | `XIX_вв.` | Мильчин | да |
| `ru/nbsp/centuries` | `XIX в.в.` | `XIX_вв.` | Мильчин | да |
| `ru/nbsp/centuries` | `XIX вв.` | `XIX_вв.` | Мильчин | да |
| `ru/nbsp/ps` | `P.S. текст` | `P._S. текст` | Мильчин | да |
| `ru/nbsp/ps` | `P. S. текст` | `P._S. текст` | Мильчин | да |
| `ru/nbsp/ps` | `P.P.S. текст` | `P._P._S. текст` | Мильчин | да |
| `ru/nbsp/m` | `100 м2` | `100_м²` | ГОСТ 16.1 | да |
| `ru/nbsp/m` | `5 м3` | `5_м³` | ГОСТ 16.1 | да |
| `ru/nbsp/m` | `м2м` | `м2м` (не единица, а часть слова) | — | — |

Здесь впервые работает `state.SafeFrom`. Перезапись усекает буфер до `state.TokenStart` и
пишет замену; если `TokenStart < SafeFrom`, между началом токена и текущей позицией лежит
скопированная разметка, и усечение съело бы её байты. Правило в этом случае обязано
отказаться — тест `RewriteDoesNotReachBehindMarkup` из задачи 2 стережёт именно это.

`ru/nbsp/centuries` в форме `в. в.` — единственное правило плана, которое переписывает не
токен, а весь отрезок от `state.SpaceIndex`: два токена и пробел между ними сливаются в
один. Отрезок начинается с позиции, которую проход записал сам, поэтому проверка та же —
`state.SpaceIndex >= state.SafeFrom`.

`ru/nbsp/m` меняет цифру на надстрочный знак и склеивает назад: два действия в одном
правиле, оба обязательны по ГОСТ 16.1. Признак единицы — токен ровно `м2` или `м3` и число
в предыдущем токене; `м2м` в таблице отрицательным случаем закрывает разбор внутри слова.

- [ ] **Step 1: Красный тест** — по строкам таблицы плюс случай с тегом внутри токена.
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: Четыре правила** в `RewriteRules` c общим помощником
      `Replace(ref CharBuffer buffer, ref BindState state, Span<char> token, int from, ReadOnlySpan<char> replacement)`.
- [ ] **Step 4: Прогнать** — Expected: PASS. Проверить `IdempotencyTests`: `гг.` не должно
      превращаться в `г.` на втором прогоне.
- [ ] **Step 5: Коммит** — `feat: «гг.», «вв.», «P. S.» и квадратные метры`.

---

### Task 11: Неразрывные пробелы внутри nobr и nowrap

**Files:**
- Modify: `src/Typographer/Internal/WordBinder.cs`
- Test: `tests/Typographer.Tests/Rules/MarkRulesTests.cs`

**Interfaces:**
- Consumes: `Segment.Kind`, срез разметки, `Chars.Nbsp`.
- Produces: обработка глубины элементов `nobr`/`nowrap` в `WordBinder.RunDocument`.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `common/nbsp/nowrap` | `<nobr>в_доме</nobr>` | `<nobr>в доме</nobr>` | JS-typograf | да |
| `common/nbsp/nowrap` | `<nowrap>в доме</nowrap>` | `<nowrap>в доме</nowrap>` (склейки внутри нет) | JS-typograf | да |
| `common/nbsp/nowrap` | `<nobr>раз</nobr> в_доме` | `<nobr>раз</nobr> в_доме` | — | да |

Тег `nobr` уже запрещает перенос внутри себя, и неразрывный пробел там не нужен: он
превращает выделяемый текст в текст с невидимым символом на месте пробела. Правило делает
две вещи внутри такого элемента — снимает неразрывные пробелы, которые были во входе, и не
даёт правилам фазы ставить новые.

Порядок важен: правило работает в фазе `Bind`, а не в `Layout`, потому что `HtmlOptions.MaxNobr`
САМ создаёт теги `nobr` в фазе `Layout`, и правило, применённое после, разобрало бы обратно
только что склеенные цепочки. К тегу из входа оно применяется, к тегу из опции — нет; это
разные вещи, и их разделяет граница фаз.

- [ ] **Step 1: Красный тест** — три строки таблицы плюс случай `MaxNobr = 3` вместе с
      правилом (цепочки, созданные опцией, остаются со неразрывными пробелами).
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: Счётчик глубины** в `RunDocument` по образцу `LayoutWriter.UpdateElementDepth`
      (разбор имени тега уже написан — вынести в общий помощник, а не копировать).
- [ ] **Step 4: Прогнать** — Expected: PASS.
- [ ] **Step 5: Коммит** — `feat: снятие неразрывных пробелов внутри nobr и nowrap`.

---

### Task 12: Даты

**Files:**
- Create: `src/Typographer/Internal/Bind/DateRules.cs`
- Modify: `src/Typographer/Internal/WordBinder.cs`
- Modify: `src/Typographer/Internal/Dictionaries.cs`
- Test: `tests/Typographer.Tests/Rules/DateRulesTests.cs` (создать)

**Interfaces:**
- Consumes: `BindState.TokenStart/SafeFrom/Kind`, `Dictionaries.IsMonth/IsWeekday`.
- Produces: `DateRules.TryRewrite(...)` — та же сигнатура, что у `RewriteRules`; вызывается из `FlushToken` следом.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `ru/date/fromISO` | `2018-10-10` | `10.10.2018` | Мильчин | да |
| `ru/date/fromISO` | `2026-09-10` | `10.09.2026` | Мильчин | да |
| `ru/date/fromISO` | `2018-13-10` | `2018-13-10` (месяц больше 12) | — | — |
| `ru/date/fromISO` | `2018-10-32` | `2018-10-32` (день больше 31) | — | — |
| `ru/date/fromISO` | `1941-1945` | `1941—1945` (тире поставила фаза Scan) | — | — |
| `ru/date/fromISO` | `192.168.0.1` | `192.168.0.1` | — | — |
| `ru/date/fromISO` | `12018-10-10` | `12018-10-10` (пять цифр в году) | — | — |
| `ru/date/weekday` | `2 Мая` | `2 мая` | Мильчин | да |
| `ru/date/weekday` | `9 Сентября 2026` | `9 сентября 2026` | Мильчин | да |
| `ru/date/weekday` | `Понедельник, 9 сентября` | `понедельник, 9 сентября` | JS-typograf | да |
| `ru/date/weekday` | `Мая много` | `Мая много` (слева не число) | — | — |
| `ru/date/weekday` | `Москва` | `Москва` (не словарное слово) | — | — |

Предикат ISO-даты жёсткий по решению 4 шапки плана: ровно `dddd-dd-dd`, месяц 1–12, день
1–31, слева и справа от токена не цифра. Перед строкой `1941-1945` в таблице стоит вывод
фазы `Scan`, а не входа: к фазе `Bind` дефис между годами уже заменён длинным тире правилом
`ru/dash/years`, и токена вида `dddd-dddd` до правила даты просто не доходит. Проверять это
всё равно надо: правило `ru/dash/years` можно выключить, и тогда предикат остаётся
единственной защитой.

Понижение регистра у названия месяца делается только тогда, когда слева стоит число, а у
названия дня недели — только когда сразу за ним запятая. Иначе правило испортило бы
«Мая» как имя собственное и «Среда» в начале предложения.

- [ ] **Step 1: Красный тест** — по строкам таблицы; отрицательных случаев больше, чем
      положительных, и это нормально для правила, меняющего данные.
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: `DateRules`** и вызов из `FlushToken`.
- [ ] **Step 4: Прогнать** — Expected: PASS, включая `HardCases` (там уже есть строка
      `01.01.2020` и `192.168.0.1`).
- [ ] **Step 5: Коммит** — `feat: ISO-дата и регистр названий месяцев и дней недели`.

---

### Task 13: Деньги

**Files:**
- Create: `src/Typographer/Internal/Bind/MoneyRules.cs`
- Modify: `src/Typographer/Internal/WordBinder.cs`
- Modify: `src/Typographer/Internal/Chars.cs`
- Test: `tests/Typographer.Tests/Rules/MoneyRulesTests.cs` (создать)

**Interfaces:**
- Consumes: `BindState.Skip/TokenStart/SafeFrom`, `Dictionaries.IsMoneyAbbreviation` (задача 5).
- Produces: `Chars.Ruble = '₽'`; `MoneyRules.TryApply(...)` — символьное правило для валюты;
  ветвь `ruble` в `RewriteRules`.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `ru/money/currency` | `$100` | `100_$` | Мильчин | **нет** |
| `ru/money/currency` | `€50` | `50_€` | Мильчин | **нет** |
| `ru/money/currency` | `£5,50` | `5,50_£` | Мильчин | **нет** |
| `ru/money/currency` | `100 $` | `100_$` | Мильчин | **нет** |
| `ru/money/currency` | `$ и €` | `$ и €` (справа не число) | — | — |
| `ru/money/ruble` | `1 руб.` | `1_₽` | JS-typograf | **нет** |
| `ru/money/ruble` | `100 руб` | `100_₽` | JS-typograf | **нет** |
| `ru/money/ruble` | `руб. за штуку` | `руб. за штуку` (слева не число) | — | — |

Символ валюты перед числом переставляется за число — это перенос текста, а не замена
символа, и потому правило символьное: оно читает число из документа вперёд и пишет его само.
Четвёртая строка таблицы — про идемпотентность: `100 $` уже в правильном порядке, и правило
обязано только заменить пробел на неразрывный, ничего не переставляя.

Оба правила вне `Default` по спецификации, раздел 6: замена «руб.» на знак рубля меняет
запись суммы, а не её оформление, и в юридическом тексте это недопустимо.

- [ ] **Step 1: Красный тест** — по строкам таблицы.
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: `MoneyRules`** плюс ветвь `ruble` в `RewriteRules`.
- [ ] **Step 4: Прогнать** — Expected: PASS, отдельно `IdempotencyTests` с включённым
      набором `RuleSet.All`.
- [ ] **Step 5: Коммит** — `feat: символ валюты после числа и знак рубля`.

---

### Task 14: Ударение и повтор слова

**Files:**
- Modify: `src/Typographer/Internal/Bind/RewriteRules.cs`
- Modify: `src/Typographer/Internal/Chars.cs`
- Test: `tests/Typographer.Tests/Rules/RewriteRulesTests.cs`

**Interfaces:**
- Consumes: `BindState.TokenStart/SpaceIndex/SafeFrom/PrevLength`, `CharBuffer.Truncate`.
- Produces: `Chars.Acute = '\u0301'` (комбинирующий акут — в исходнике только escape-последовательностью, литерал невидим); ветви `accent` и `repeatWord` в `RewriteRules`.

| Правило | Вход | Выход | Источник | В `Default` |
|---|---|---|---|---|
| `ru/other/accent` | `зАмок` | `за́мок` | JS-typograf | **нет** |
| `ru/other/accent` | `корОва` | `коро́ва` | JS-typograf | **нет** |
| `ru/other/accent` | `Москва` | `Москва` (прописная первая — не ударение) | — | — |
| `ru/other/accent` | `СССР` | `СССР` (прописные все) | — | — |
| `ru/other/accent` | `иПод` | `иПод` (две прописные) | — | — |
| `common/other/repeatWord` | `повтор повтор слова` | `повтор слова` | JS-typograf | **нет** |
| `common/other/repeatWord` | `Повтор повтор` | `Повтор` | JS-typograf | **нет** |
| `common/other/repeatWord` | `все все же` | `все же` | JS-typograf | **нет** |
| `common/other/repeatWord` | `дом дома` | `дом дома` (слова разные) | — | — |
| `common/other/repeatWord` | `так так-то` | `так так-то` (слова разные) | — | — |

Ударение ставится комбинирующим акутом U+0301 после гласной, а прописная буква становится
строчной. Признак — РОВНО одна прописная буква, и не первая в токене; иначе правило
испортило бы имя собственное и аббревиатуру. Строки 3–5 таблицы закрывают все три
вырождения.

Удаление повтора — единственное правило плана, которое стирает слово. Отрезок стирания
начинается с `state.SpaceIndex` (пробел между двумя одинаковыми токенами) и тянется до
текущей позиции записи; проверка `state.SpaceIndex >= state.SafeFrom` обязательна, иначе
между повторами мог быть тег, и стирание съело бы его.

- [ ] **Step 1: Красный тест** — по строкам таблицы плюс `раз <b>раз</b>` (повтор через
      тег не удаляется).
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: Две ветви** в `RewriteRules`.
- [ ] **Step 4: Прогнать** — Expected: PASS.
- [ ] **Step 5: Коммит** — `feat: ударение и удаление повтора слова`.

---

### Task 15: Телефонные номера

**Files:**
- Create: `src/Typographer/Internal/Bind/PhoneRules.cs`
- Modify: `src/Typographer/Internal/WordBinder.cs`
- Test: `tests/Typographer.Tests/Rules/PhoneRulesTests.cs` (создать)

**Interfaces:**
- Consumes: `BindState.Skip/SafeFrom`, `CharBuffer.Write`.
- Produces: `PhoneRules.TryApply(ReadOnlySpan<char> document, int index, int end, RuleSet rules, ref BindState state, ref CharBuffer buffer)`; вызов из `BindSegment` на первом символе токена и на символе `+`.

| Вход | Выход | В `Default` |
|---|---|---|
| `+7 (999) 123-45-67` | `+7_999_123-45-67` | да |
| `+79991234567` | `+7_999_123-45-67` | да |
| `+7-999-123-45-67` | `+7_999_123-45-67` | да |
| `8 (999) 123-45-67` | `8_999_123-45-67` | да |
| `89991234567` | `8_999_123-45-67` | да |
| `+7_999_123-45-67` | `+7_999_123-45-67` (идемпотентность) | да |
| `+7 999 123-45-6` | без изменений (девять цифр) | да |
| `+7 999 123-45-678` | без изменений (двенадцать цифр) | да |
| `+380 44 123 4567` | без изменений (не российский код) | да |
| `1234567890` | без изменений (нет префикса) | да |
| `артикул 89991234567890` | без изменений (цифр больше одиннадцати) | да |

Правило самое рискованное в плане: оно в `Default` и переписывает цифры. Отсюда узкий
предикат решения 5 шапки: префикс `+7` или `8`, ровно одиннадцать цифр, разделители только
пробел, неразрывный пробел, дефис и круглые скобки, и по обе стороны от найденной
последовательности не цифра. Отрицательных случаев в таблице больше, чем положительных, и
каждый из них — отдельная строка `[InlineData]`.

Оракул Лебедева оборачивает телефон в `<nobr class="phone">`. Мы так не делаем: правило
`Default` не имеет права создавать разметку из текста (гарантия 4), поэтому неразрывность
даётся неразрывными пробелами. Расхождение уже зафиксировано в снимке оракула, пункт 3.

- [ ] **Step 1: Красный тест** — все одиннадцать строк таблицы.
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: `PhoneRules`** — чтение вперёд с накоплением до одиннадцати цифр в
      `Span<char> digits = stackalloc char[12]`, запись канонической формы, `state.Skip`.
- [ ] **Step 4: Прогнать** — Expected: PASS; отдельно `RobustnessTests` (правило читает
      вперёд и обязано не выходить за `end`).
- [ ] **Step 5: Коммит** — `feat: форматирование телефонных номеров`.

---

### Task 16: Пресеты, спецификация и сверка с оракулом

**Files:**
- Modify: `src/Typographer/Rules/RuleSet.cs`
- Modify: `docs/spec.md:247-292`
- Modify: `docs/oracle/lebedev.md`
- Modify: `tests/Typographer.Tests/Corpus/HardCases.cs`
- Test: `tests/Typographer.Tests/Rules/RuleSetTests.cs`

**Interfaces:**
- Consumes: все правила задач 1–15.
- Produces: расхождения пресетов `Lebedev` и `Typograf`; актуальный раздел 6 спецификации; раздел расхождений в снимке оракула.

Пресеты расходятся ещё на два правила:

| Пресет | Правка | Почему |
|---|---|---|
| `Lebedev` | `.Without(RuleId.Ru.Nbsp.Addr)` | оракул не склеивает `ул. Ленина`, `г. Москва`, `с. Никольское` |
| `Lebedev` | `.Without(RuleId.Common.Space.DelBeforePercent)` | оракул оставляет `50_%`, а правило пробел съедает |
| `Typograf` | `.Without(RuleId.Common.Nbsp.AfterNumber)` | в JS-typograf это правило выключено по умолчанию |

Раздел 6 спецификации правится в трёх местах: группа `common/nbsp/*` получает сноску про
`replaceNbsp` вне `Default`; группа `common/other/*` — «только `delBOM`» подтверждается;
абзац «Выключены в `Default`, но реализованы» пополняется правилами `replaceNbsp` и
`accent`, а `common/html/url`, `common/html/e-mail` и `ru/optalign/*` из него НЕ убираются —
они всё ещё не реализованы, и это план 2d.

- [ ] **Step 1: Красные тесты на пресеты** — по одному `[Fact]` на строку таблицы правок.
- [ ] **Step 2: Прогнать** — Expected: FAIL.
- [ ] **Step 3: Правка `RuleSet`** и XML-комментариев пресетов: комментарий обязан назвать
      причину каждого расхождения, а не только перечислить правила.
- [ ] **Step 4: Прогон всех 36 строк снимка оракула через `Default`** и обновление раздела
      «Расхождения с нашим Default» в `docs/oracle/lebedev.md`: пункты 2 (неразрывные
      пробелы) и 4 закрыты этим планом полностью или частично — отметить, что именно
      закрыто, а что осталось, фактами, а не оценками.
- [ ] **Step 5: Корпус** — `HardCases` пополняется строками из таблиц задач 4–15, которые
      ловят взаимодействие правил: `ул. Ленина, д. 5, кв. 7`, `в 3 г. IV в. до н. э.`,
      `+7 (999) 123-45-67`, `2026-09-10`, `100 руб.`, `№ 5 и § 3`.
- [ ] **Step 6: Прогнать всё** — `dotnet test`. Expected: PASS.
- [ ] **Step 7: Коммит** — `feat: пресеты Lebedev и Typograf учли правила фазы Bind`.

---

## Приёмка плана

- 96 правил в реестре, `RuleSet.All.Count == 96`, каждое имя разбирается через `TryParse`.
- Все правила фазы `Bind` действуют: включение через `RuleSet` меняет вывод, отключение —
  возвращает. Правил, зарегистрированных «на будущее», после плана 2c остаётся ровно
  двенадцать: одиннадцать из плана 2d и `common/punctuation/quoteLink`, который не будет
  реализован никогда.
- Все семь гарантий раздела 8 спецификации проверены тестами на наборе `RuleSet.All`, а не
  только на `Default`. Идемпотентность — отдельным прогоном по каждому правилу в одиночку.
- `docs/spec.md` и `docs/oracle/lebedev.md` соответствуют коду.
- Ни одного регулярного выражения, ни одной аллокации в горячем пути, ни одного публичного
  члена без русского XML-комментария.
