# План 1: ядро типографа

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Рабочее ядро типографа — шестифазный однопроходный конвейер, HTML-сканер, кодирование сущностей и три группы правил (пробелы и пунктуация, кавычки, тире), с гарантиями по производительности, идемпотентности и безопасности.

**Architecture:** Вход читается один раз. Фаза `Protect` режет его на сегменты (текст / разметка / защищённая зона). Текстовые сегменты проходят посимвольный сканер (`Scan`), затем словарный проход по словам (`Bind`), затем компоновку (`Layout`). Всё пишется в один растущий буфер поверх `ArrayPool<char>`, который разрешает патчить уже записанные позиции по индексу — за счёт этого правила с длинным контекстом работают без второго прохода. Фаза `Emit` кодирует результат по `EntityMode`.

**Tech Stack:** C# 13, .NET 10 SDK; целевые платформы `netstandard2.0`, `net8.0`, `net10.0`; xunit.v3 поверх Microsoft.Testing.Platform; BenchmarkDotNet; MinVer.

**Spec:** `docs/spec.md`

## Global Constraints

- Целевые платформы ядра: `netstandard2.0;net8.0;net10.0`. На `net8.0` и `net10.0` пакет `Typographer` не имеет ни одной внешней зависимости. На `netstandard2.0` допускается ровно один официальный полифил Microsoft — `System.Memory`, без которого там нет ни `Span<char>`, ни `ArrayPool<char>`; подключается через `PackageReference` с `Condition` на этот TFM.
- `SearchValues<char>` и `FrozenSet<string>` доступны только на `net8.0` и выше. Каждое их применение оборачивается в `#if NET8_0_OR_GREATER`, а для `netstandard2.0` пишется эквивалент на массиве или `HashSet<string>` с `StringComparer.Ordinal`. Поведение обеих веток обязано совпадать — это проверяется тем, что тесты гоняются на всех TFM.
- Никаких регулярных выражений в горячем пути. `System.Text.RegularExpressions` в проекте `src/Typographer` не используется вовсе.
- `TreatWarningsAsErrors` включён, `GenerateDocumentationFile` включён. Каждый публичный член имеет XML-комментарий **на русском языке**.
- Ноль аллокаций на пути `Process(ReadOnlySpan<char>, IBufferWriter<char>)` после прогрева пула. `CharBuffer` — изменяемая структура, передаётся только по `ref`, никогда не боксируется и не захватывается замыканием.
- Ветвление git flow. Работа ведётся в ветке `feature/core`, ответвлённой от `develop`. Прямой коммит в `master` и `develop` запрещён правилами репозитория.
- Каждый коммит заканчивается строками:
  ```
  Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01SvpPxF3qxdPDX6crRz3cFt
  ```
- Приоритет источников при любом сомнении о правильном поведении: ГОСТ Р 7.0.110-2025 ⇒ Мильчин ⇒ практика студии Лебедева ⇒ JS-typograf.

---

## Структура файлов

Создаются в ходе плана:

| Файл | Ответственность |
|---|---|
| `src/Typographer/Typograf.cs` | Статический фасад: `Typograf.Html`, `Typograf.PlainText` |
| `src/Typographer/HtmlTypograf.cs` | Иммутабельный обработчик HTML-фрагментов |
| `src/Typographer/TextTypograf.cs` | Иммутабельный обработчик обычного текста |
| `src/Typographer/HtmlOptions.cs` | Настройки HTML-режима |
| `src/Typographer/TextOptions.cs` | Настройки текстового режима |
| `src/Typographer/EntityMode.cs` | Режим вывода сущностей |
| `src/Typographer/OutputTooLargeException.cs` | Превышение `MaxOutputLength` |
| `src/Typographer/Rules/RuleId.cs` | Идентификаторы правил, типизированное дерево |
| `src/Typographer/Rules/RuleSet.cs` | Иммутабельное множество правил на битовой маске |
| `src/Typographer/Rules/RulePhase.cs` | Перечисление фаз конвейера |
| `src/Typographer/Internal/CharBuffer.cs` | Растущий буфер поверх `ArrayPool<char>` с патчем по индексу |
| `src/Typographer/Internal/Chars.cs` | Константы символов и наборы для поиска |
| `src/Typographer/Internal/EntityTable.cs` | Декодирование и кодирование типографских сущностей |
| `src/Typographer/Internal/MarkupScanner.cs` | Фаза `Protect`: сегментация на текст, разметку и защищённые зоны |
| `src/Typographer/Internal/TextScanner.cs` | Фаза `Scan`: посимвольные правила |
| `src/Typographer/Internal/QuoteStack.cs` | Стек уровней кавычек |
| `src/Typographer/Internal/WordBinder.cs` | Фаза `Bind`: словарные правила неразрывных пробелов |
| `src/Typographer/Internal/LayoutWriter.cs` | Фаза `Layout`: `nobr`, `br`, `p` |
| `src/Typographer/Internal/Emitter.cs` | Фаза `Emit`: кодирование по `EntityMode` |
| `tests/Typographer.Tests/**` | Unit- и property-тесты |
| `bench/Typographer.Bench/**` | Бенчмарки |

---

### Task 1: Буфер вывода с патчем по индексу

**Files:**
- Create: `src/Typographer/Internal/CharBuffer.cs`
- Create: `src/Typographer/OutputTooLargeException.cs`
- Modify: `src/Typographer/Typographer.csproj` (добавить `InternalsVisibleTo`)
- Test: `tests/Typographer.Tests/Internal/CharBufferTests.cs`

**Interfaces:**
- Consumes: ничего.
- Produces: `internal struct CharBuffer` с членами `CharBuffer(int initialCapacity, int maxLength = 0)`, `int Length`, `void Write(char value)`, `void Write(ReadOnlySpan<char> value)`, `void PatchAt(int index, char value)`, `char CharAt(int index)`, `ReadOnlySpan<char> AsSpan()`, `void Dispose()`. Публичный `OutputTooLargeException : InvalidOperationException` с конструктором `OutputTooLargeException(int limit)`.

- [ ] **Step 1: Открыть внутренние типы для тестов**

В `src/Typographer/Typographer.csproj` добавить в существующий `ItemGroup`:

```xml
<InternalsVisibleTo Include="Typographer.Tests" />
```

- [ ] **Step 2: Написать падающий тест**

Создать `tests/Typographer.Tests/Internal/CharBufferTests.cs`:

```csharp
using Typographer.Internal;

namespace Typographer.Tests.Internal;

public class CharBufferTests
{
    [Fact]
    public void Write_GrowsBeyondInitialCapacity()
    {
        var buffer = new CharBuffer(initialCapacity: 2);
        try
        {
            buffer.Write("длинная строка");
            Assert.Equal("длинная строка", buffer.AsSpan().ToString());
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Fact]
    public void PatchAt_ЗаменяетУжеЗаписанныйСимвол()
    {
        var buffer = new CharBuffer(initialCapacity: 8);
        try
        {
            buffer.Write("в доме");
            buffer.PatchAt(1, ' ');
            Assert.Equal("в доме", buffer.AsSpan().ToString());
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Fact]
    public void Write_БросаетПриПревышенииПредела()
    {
        var buffer = new CharBuffer(initialCapacity: 4, maxLength: 5);
        try
        {
            Assert.Throws<OutputTooLargeException>(() => buffer.Write("шесть!"));
        }
        finally
        {
            buffer.Dispose();
        }
    }
}
```

Соглашение об именах тестов: первая часть до подчёркивания — латиницей (имя проверяемого члена), вторая — по-русски (ожидаемое поведение).

- [ ] **Step 3: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter CharBufferTests`
Ожидается: ошибка компиляции «тип или имя пространства имён CharBuffer не найдено».

- [ ] **Step 4: Реализовать минимально**

Создать `src/Typographer/OutputTooLargeException.cs`:

```csharp
namespace Typographer;

/// <summary>Результат типографирования превысил предел, заданный в настройках.</summary>
public sealed class OutputTooLargeException : InvalidOperationException
{
    /// <summary>Создаёт исключение для указанного предела длины.</summary>
    /// <param name="limit">Предел длины результата в символах.</param>
    public OutputTooLargeException(int limit)
        : base($"Результат типографирования превысил предел в {limit} символов.")
        => Limit = limit;

    /// <summary>Предел длины результата в символах.</summary>
    public int Limit { get; }
}
```

Создать `src/Typographer/Internal/CharBuffer.cs`:

```csharp
using System.Buffers;

namespace Typographer.Internal;

/// <summary>
/// Растущий буфер символов поверх <see cref="ArrayPool{T}"/>.
/// Разрешает патчить уже записанные позиции — на этом построена однопроходность конвейера.
/// Изменяемая структура: передавать только по ссылке.
/// </summary>
internal struct CharBuffer
{
    private char[] _array;
    private int _length;
    private readonly int _maxLength;

    public CharBuffer(int initialCapacity, int maxLength = 0)
    {
        _array = ArrayPool<char>.Shared.Rent(Math.Max(initialCapacity, 16));
        _length = 0;
        _maxLength = maxLength;
    }

    public readonly int Length => _length;

    public readonly char CharAt(int index) => _array[index];

    public readonly ReadOnlySpan<char> AsSpan() => _array.AsSpan(0, _length);

    public void Write(char value)
    {
        EnsureCapacity(1);
        _array[_length++] = value;
    }

    public void Write(ReadOnlySpan<char> value)
    {
        EnsureCapacity(value.Length);
        value.CopyTo(_array.AsSpan(_length));
        _length += value.Length;
    }

    public readonly void PatchAt(int index, char value) => _array[index] = value;

    public void Dispose()
    {
        char[]? array = _array;
        _array = null!;
        _length = 0;
        if (array is not null)
        {
            ArrayPool<char>.Shared.Return(array);
        }
    }

    private void EnsureCapacity(int additional)
    {
        int required = _length + additional;
        if (_maxLength > 0 && required > _maxLength)
        {
            throw new OutputTooLargeException(_maxLength);
        }

        if (required <= _array.Length)
        {
            return;
        }

        int capacity = _array.Length * 2;
        while (capacity < required)
        {
            capacity *= 2;
        }

        char[] grown = ArrayPool<char>.Shared.Rent(capacity);
        _array.AsSpan(0, _length).CopyTo(grown);
        ArrayPool<char>.Shared.Return(_array);
        _array = grown;
    }
}
```

- [ ] **Step 5: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Tests --filter CharBufferTests`
Ожидается: PASS, 3 теста.

- [ ] **Step 6: Коммит**

```bash
git add src/Typographer/Internal/CharBuffer.cs src/Typographer/OutputTooLargeException.cs src/Typographer/Typographer.csproj tests/Typographer.Tests/Internal/CharBufferTests.cs
git commit -m "feat: буфер вывода с патчем по индексу"
```

---

### Task 2: Таблица типографских сущностей

**Files:**
- Create: `src/Typographer/Internal/Chars.cs`
- Create: `src/Typographer/Internal/EntityTable.cs`
- Test: `tests/Typographer.Tests/Internal/EntityTableTests.cs`

**Interfaces:**
- Consumes: ничего.
- Produces: `internal static class Chars` с константами `Nbsp = ' '`, `NarrowNbsp = ' '`, `ThinSpace = ' '`, `Laquo = '«'`, `Raquo = '»'`, `Bdquo = '„'`, `Ldquo = '“'`, `Lsquo = '‘'`, `Rsquo = '’'`, `MDash = '—'`, `NDash = '–'`, `Hellip = '…'`, `Numero = '№'`. `internal static class EntityTable` с членами `bool TryDecode(ReadOnlySpan<char> source, out char value, out int consumed)`, `string? NameOf(char value)`, `bool IsInvisible(char value)`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/Internal/EntityTableTests.cs`:

```csharp
using Typographer.Internal;

namespace Typographer.Tests.Internal;

public class EntityTableTests
{
    [Theory]
    [InlineData("&laquo;текст", '«', 8)]
    [InlineData("&nbsp;", ' ', 6)]
    [InlineData("&mdash;", '—', 7)]
    [InlineData("&#171;", '«', 6)]
    [InlineData("&#x00AB;", '«', 8)]
    public void TryDecode_РаспознаётТипографскиеСущности(string source, char expected, int expectedConsumed)
    {
        Assert.True(EntityTable.TryDecode(source.AsSpan(), out char value, out int consumed));
        Assert.Equal(expected, value);
        Assert.Equal(expectedConsumed, consumed);
    }

    [Theory]
    [InlineData("&amp;")]
    [InlineData("&lt;")]
    [InlineData("&gt;")]
    [InlineData("&nosuch;")]
    [InlineData("&nbsp")]
    public void TryDecode_ОтклоняетНетипографскиеИНезавершённые(string source)
    {
        Assert.False(EntityTable.TryDecode(source.AsSpan(), out _, out _));
    }

    [Fact]
    public void NameOf_ВозвращаетИмяДляТипографскогоСимвола()
    {
        Assert.Equal("nbsp", EntityTable.NameOf(' '));
        Assert.Equal("laquo", EntityTable.NameOf('«'));
        Assert.Null(EntityTable.NameOf('а'));
    }

    [Fact]
    public void IsInvisible_ТолькоПробельныеСимволы()
    {
        Assert.True(EntityTable.IsInvisible(' '));
        Assert.True(EntityTable.IsInvisible(' '));
        Assert.False(EntityTable.IsInvisible('«'));
    }
}
```

Важно: `&amp;`, `&lt;`, `&gt;` намеренно не декодируются — они несут смысл разметки, а не типографики, и должны дойти до выхода нетронутыми (спецификация, раздел 5.1).

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter EntityTableTests`
Ожидается: ошибка компиляции «EntityTable не найден».

- [ ] **Step 3: Реализовать таблицу**

Создать `src/Typographer/Internal/Chars.cs`:

```csharp
namespace Typographer.Internal;

/// <summary>Символы, которыми оперирует типограф.</summary>
internal static class Chars
{
    public const char Nbsp = ' ';
    public const char NarrowNbsp = ' ';
    public const char ThinSpace = ' ';
    public const char Laquo = '«';
    public const char Raquo = '»';
    public const char Bdquo = '„';
    public const char Ldquo = '“';
    public const char Lsquo = '‘';
    public const char Rsquo = '’';
    public const char MDash = '—';
    public const char NDash = '–';
    public const char Hellip = '…';
    public const char Numero = '№';
    public const char Minus = '−';
    public const char Times = '×';
    public const char Degree = '°';
}
```

Создать `src/Typographer/Internal/EntityTable.cs`:

```csharp
using System.Globalization;

namespace Typographer.Internal;

/// <summary>Декодирование и кодирование типографских HTML-сущностей.</summary>
/// <remarks>
/// Сущности разметки (&amp;amp;, &amp;lt;, &amp;gt;, &amp;quot;) сюда не входят:
/// они несут смысл разметки и проходят насквозь.
/// </remarks>
internal static class EntityTable
{
    private static readonly (string Name, char Value)[] Entries =
    [
        ("nbsp", Chars.Nbsp),
        ("thinsp", Chars.ThinSpace),
        ("laquo", Chars.Laquo),
        ("raquo", Chars.Raquo),
        ("bdquo", Chars.Bdquo),
        ("ldquo", Chars.Ldquo),
        ("lsquo", Chars.Lsquo),
        ("rsquo", Chars.Rsquo),
        ("mdash", Chars.MDash),
        ("ndash", Chars.NDash),
        ("hellip", Chars.Hellip),
        ("numero", Chars.Numero),
        ("times", Chars.Times),
        ("deg", Chars.Degree),
        ("copy", '©'),
        ("reg", '®'),
        ("trade", '™'),
        ("sect", '§'),
        ("para", '¶'),
        ("plusmn", '±'),
        ("ne", '≠'),
        ("le", '≤'),
        ("ge", '≥'),
        ("larr", '←'),
        ("rarr", '→'),
        ("sup2", '²'),
        ("sup3", '³'),
        ("frac12", '½'),
        ("frac14", '¼'),
        ("frac34", '¾'),
    ];

    public static bool TryDecode(ReadOnlySpan<char> source, out char value, out int consumed)
    {
        value = default;
        consumed = 0;

        if (source.Length < 4 || source[0] != '&')
        {
            return false;
        }

        int end = source.Slice(0, Math.Min(source.Length, 12)).IndexOf(';');
        if (end < 0)
        {
            return false;
        }

        ReadOnlySpan<char> body = source.Slice(1, end - 1);

        if (body.Length > 1 && body[0] == '#')
        {
            ReadOnlySpan<char> digits = body.Slice(1);
            bool hex = digits[0] is 'x' or 'X';
            if (hex)
            {
                digits = digits.Slice(1);
            }

            if (digits.IsEmpty || !TryParseCode(digits, hex, out int code) || code is < 0 or > 0xFFFF)
            {
                return false;
            }

            char decoded = (char)code;
            if (NameOf(decoded) is null)
            {
                return false;
            }

            value = decoded;
            consumed = end + 1;
            return true;
        }

        foreach ((string name, char entityValue) in Entries)
        {
            if (body.SequenceEqual(name.AsSpan()))
            {
                value = entityValue;
                consumed = end + 1;
                return true;
            }
        }

        return false;
    }

    public static string? NameOf(char value)
    {
        foreach ((string name, char entityValue) in Entries)
        {
            if (entityValue == value)
            {
                return name;
            }
        }

        return null;
    }

    public static bool IsInvisible(char value)
        => value is Chars.Nbsp or Chars.NarrowNbsp or Chars.ThinSpace;

    private static bool TryParseCode(ReadOnlySpan<char> digits, bool hex, out int code)
    {
        NumberStyles styles = hex ? NumberStyles.HexNumber : NumberStyles.Integer;
#if NET8_0_OR_GREATER
        return int.TryParse(digits, styles, CultureInfo.InvariantCulture, out code);
#else
        return int.TryParse(digits.ToString(), styles, CultureInfo.InvariantCulture, out code);
#endif
    }
}
```

Линейный перебор по 30 элементам допустим: `TryDecode` вызывается только на символе `&`, а `NameOf` — только на фазе `Emit` для символов, уже опознанных как типографские. Если бенчмарк Task 13 покажет, что это заметно, заменить на `switch` по длине и первому символу.

- [ ] **Step 4: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Tests --filter EntityTableTests`
Ожидается: PASS, 11 тестов.

- [ ] **Step 5: Коммит**

```bash
git add src/Typographer/Internal/Chars.cs src/Typographer/Internal/EntityTable.cs tests/Typographer.Tests/Internal/EntityTableTests.cs
git commit -m "feat: таблица типографских сущностей"
```

---

### Task 3: Сегментация разметки (фаза Protect)

**Files:**
- Create: `src/Typographer/Internal/MarkupScanner.cs`
- Test: `tests/Typographer.Tests/Internal/MarkupScannerTests.cs`

**Interfaces:**
- Consumes: ничего.
- Produces: `internal enum SegmentKind { Text, Markup, Protected }`; `internal readonly record struct Segment(SegmentKind Kind, int Start, int Length)`; `internal ref struct MarkupScanner` с конструктором `MarkupScanner(ReadOnlySpan<char> source)` и методом `bool TryRead(out Segment segment)`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/Internal/MarkupScannerTests.cs`:

```csharp
using Typographer.Internal;

namespace Typographer.Tests.Internal;

public class MarkupScannerTests
{
    private static List<(SegmentKind Kind, string Text)> Scan(string source)
    {
        var result = new List<(SegmentKind, string)>();
        var scanner = new MarkupScanner(source.AsSpan());
        while (scanner.TryRead(out Segment segment))
        {
            result.Add((segment.Kind, source.Substring(segment.Start, segment.Length)));
        }

        return result;
    }

    [Fact]
    public void РазделяетТегиИТекст()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<p>"), (SegmentKind.Text, "Он - человек"), (SegmentKind.Markup, "</p>")],
            Scan("<p>Он - человек</p>"));
    }

    [Fact]
    public void СодержимоеCodeЗащищено()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<code>"), (SegmentKind.Protected, "a - b"), (SegmentKind.Markup, "</code>")],
            Scan("<code>a - b</code>"));
    }

    [Fact]
    public void УгловаяСкобкаВЗначенииАтрибутаНеЗавершаетТег()
    {
        Assert.Equal(
            [(SegmentKind.Markup, "<a title=\"a>b\">"), (SegmentKind.Text, "x"), (SegmentKind.Markup, "</a>")],
            Scan("<a title=\"a>b\">x</a>"));
    }

    [Fact]
    public void ОдинокаяУгловаяСкобкаОстаётсяТекстом()
    {
        Assert.Equal([(SegmentKind.Text, "если a < b, то")], Scan("если a < b, то"));
    }

    [Fact]
    public void КомментарийЗащищён()
    {
        Assert.Equal(
            [(SegmentKind.Protected, "<!--[if IE]>только тут<![endif]-->")],
            Scan("<!--[if IE]>только тут<![endif]-->"));
    }

    [Fact]
    public void НезакрытыйТегДоходитДоКонцаВвода()
    {
        Assert.Equal([(SegmentKind.Text, "текст "), (SegmentKind.Markup, "<b")], Scan("текст <b"));
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter MarkupScannerTests`
Ожидается: ошибка компиляции «MarkupScanner не найден».

- [ ] **Step 3: Реализовать сканер**

Создать `src/Typographer/Internal/MarkupScanner.cs`:

```csharp
namespace Typographer.Internal;

/// <summary>Вид сегмента входного текста.</summary>
internal enum SegmentKind
{
    /// <summary>Текстовый узел: к нему применяются правила типографики.</summary>
    Text,

    /// <summary>Тег целиком, включая значения атрибутов: копируется без изменений.</summary>
    Markup,

    /// <summary>Защищённая зона (code, pre, script, комментарий): копируется без изменений.</summary>
    Protected,
}

/// <summary>Сегмент входного текста.</summary>
internal readonly record struct Segment(SegmentKind Kind, int Start, int Length);

/// <summary>
/// Фаза Protect: делит вход на текстовые узлы, разметку и защищённые зоны.
/// Ломаный HTML ошибкой не считается и выводится как есть.
/// </summary>
internal ref struct MarkupScanner
{
    private static readonly string[] ProtectedTags =
        ["code", "pre", "script", "style", "textarea", "kbd", "samp"];

    private readonly ReadOnlySpan<char> _source;
    private int _position;
    private int _protectedUntil;

    public MarkupScanner(ReadOnlySpan<char> source)
    {
        _source = source;
        _position = 0;
        _protectedUntil = 0;
    }

    public bool TryRead(out Segment segment)
    {
        if (_position >= _source.Length)
        {
            segment = default;
            return false;
        }

        int start = _position;

        if (IsCommentStart(start))
        {
            int end = IndexOfSequence(start + 4, "-->");
            _position = end < 0 ? _source.Length : end + 3;
            segment = new Segment(SegmentKind.Protected, start, _position - start);
            return true;
        }

        if (IsTagStart(start))
        {
            _position = ReadTagEnd(start);
            segment = new Segment(SegmentKind.Markup, start, _position - start);
            return true;
        }

        if (_protectedUntil > start)
        {
            _position = Math.Min(_protectedUntil, _source.Length);
            segment = new Segment(SegmentKind.Protected, start, _position - start);
            return true;
        }

        while (_position < _source.Length && !IsTagStart(_position) && !IsCommentStart(_position))
        {
            _position++;
        }

        segment = new Segment(SegmentKind.Text, start, _position - start);
        return true;
    }

    private readonly bool IsCommentStart(int index)
        => index + 3 < _source.Length
           && _source[index] == '<' && _source[index + 1] == '!'
           && _source[index + 2] == '-' && _source[index + 3] == '-';

    private readonly bool IsTagStart(int index)
    {
        if (index + 1 >= _source.Length || _source[index] != '<')
        {
            return false;
        }

        char next = _source[index + 1];
        return char.IsLetter(next) || next is '/' or '!' or '?';
    }

    private int ReadTagEnd(int start)
    {
        int index = start + 1;
        char quote = '\0';

        while (index < _source.Length)
        {
            char c = _source[index];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                index++;
                MarkProtectedContent(start, index);
                return index;
            }

            index++;
        }

        return _source.Length;
    }

    private void MarkProtectedContent(int tagStart, int tagEnd)
    {
        if (_source[tagStart + 1] == '/')
        {
            return;
        }

        int nameStart = tagStart + 1;
        int nameEnd = nameStart;
        while (nameEnd < tagEnd && char.IsLetter(_source[nameEnd]))
        {
            nameEnd++;
        }

        ReadOnlySpan<char> name = _source.Slice(nameStart, nameEnd - nameStart);
        foreach (string tag in ProtectedTags)
        {
            if (name.Equals(tag.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                int closing = IndexOfClosingTag(tagEnd, tag);
                _protectedUntil = closing < 0 ? _source.Length : closing;
                return;
            }
        }
    }

    private readonly int IndexOfClosingTag(int from, string tag)
    {
        for (int i = from; i + tag.Length + 2 < _source.Length; i++)
        {
            if (_source[i] == '<' && _source[i + 1] == '/'
                && _source.Slice(i + 2, tag.Length).Equals(tag.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private readonly int IndexOfSequence(int from, string needle)
    {
        if (from >= _source.Length)
        {
            return -1;
        }

        int found = _source.Slice(from).IndexOf(needle.AsSpan(), StringComparison.Ordinal);
        return found < 0 ? -1 : from + found;
    }
}
```

- [ ] **Step 4: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Tests --filter MarkupScannerTests`
Ожидается: PASS, 6 тестов.

- [ ] **Step 5: Коммит**

```bash
git add src/Typographer/Internal/MarkupScanner.cs tests/Typographer.Tests/Internal/MarkupScannerTests.cs
git commit -m "feat: сегментация разметки и защищённых зон"
```

---

### Task 4: Идентификаторы и множества правил

**Files:**
- Create: `src/Typographer/Rules/RulePhase.cs`
- Create: `src/Typographer/Rules/RuleId.cs`
- Create: `src/Typographer/Rules/RuleSet.cs`
- Test: `tests/Typographer.Tests/Rules/RuleSetTests.cs`

**Interfaces:**
- Consumes: ничего.
- Produces: `public enum RulePhase { Prepare, Protect, Scan, Bind, Layout, Emit }`; `public readonly struct RuleId` с `string Name`, `RulePhase Phase`, `int Index`, `static bool TryParse(string, out RuleId)` и вложенными статическими классами-деревом; `public sealed class RuleSet` с `Contains`, `With`, `Without`, `Count` и статическими пресетами `Default`, `Lebedev`, `Typograf`, `Gost`, `All`, `Minimal`, `None`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/Rules/RuleSetTests.cs`:

```csharp
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class RuleSetTests
{
    [Fact]
    public void Default_ВключаетТиреИКавычки()
    {
        Assert.True(RuleSet.Default.Contains(RuleId.Ru.Dash.Main));
        Assert.True(RuleSet.Default.Contains(RuleId.Common.Punctuation.Quote));
    }

    [Fact]
    public void Default_ИсключаетРискованныеПравила()
    {
        Assert.False(RuleSet.Default.Contains(RuleId.Ru.Typo.SwitchingKeyboardLayout));
        Assert.False(RuleSet.Default.Contains(RuleId.Ru.Punctuation.Ano));
    }

    [Fact]
    public void Without_НеМеняетИсходноеМножество()
    {
        RuleSet reduced = RuleSet.Default.Without(RuleId.Ru.Dash.Main);

        Assert.False(reduced.Contains(RuleId.Ru.Dash.Main));
        Assert.True(RuleSet.Default.Contains(RuleId.Ru.Dash.Main));
    }

    [Fact]
    public void With_ДобавляетВыключенноеПравило()
    {
        RuleSet extended = RuleSet.Default.With(RuleId.Ru.Typo.SwitchingKeyboardLayout);

        Assert.True(extended.Contains(RuleId.Ru.Typo.SwitchingKeyboardLayout));
    }

    [Fact]
    public void None_Пусто_All_Полно()
    {
        Assert.Equal(0, RuleSet.None.Count);
        Assert.True(RuleSet.All.Count >= RuleSet.Default.Count);
    }

    [Theory]
    [InlineData("ru/dash/main")]
    [InlineData("common/punctuation/quote")]
    public void TryParse_РазбираетИмяКакВJsTypograf(string name)
    {
        Assert.True(RuleId.TryParse(name, out RuleId rule));
        Assert.Equal(name, rule.Name);
    }

    [Fact]
    public void TryParse_ОтклоняетНеизвестноеИмя()
    {
        Assert.False(RuleId.TryParse("ru/nbsp/abr", out _));
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter RuleSetTests`
Ожидается: ошибка компиляции «RuleSet не найден».

- [ ] **Step 3: Реализовать реестр правил**

Создать `src/Typographer/Rules/RulePhase.cs`:

```csharp
namespace Typographer.Rules;

/// <summary>Фаза конвейера, на которой применяется правило.</summary>
public enum RulePhase
{
    /// <summary>Подготовка: BOM, переводы строк, декодирование сущностей.</summary>
    Prepare,

    /// <summary>Защита: разметка и неприкосновенные зоны.</summary>
    Protect,

    /// <summary>Посимвольный проход: кавычки, тире, пунктуация, символы.</summary>
    Scan,

    /// <summary>Словарный проход: неразрывные пробелы, сокращения, единицы.</summary>
    Bind,

    /// <summary>Компоновка: nobr, висячая пунктуация, абзацы и переносы.</summary>
    Layout,

    /// <summary>Вывод: кодирование сущностей.</summary>
    Emit,
}
```

Создать `src/Typographer/Rules/RuleId.cs`. В этом плане регистрируются только правила, которые план реализует; план 2 дополняет реестр до 107.

```csharp
namespace Typographer.Rules;

/// <summary>Идентификатор правила типографики.</summary>
/// <remarks>Имена совпадают с именами правил JS-typograf, например «ru/nbsp/abbr».</remarks>
public readonly struct RuleId : IEquatable<RuleId>
{
    internal RuleId(int index, string name, RulePhase phase)
    {
        Index = index;
        Name = name;
        Phase = phase;
    }

    /// <summary>Порядковый номер правила в реестре. Используется как позиция бита в <see cref="RuleSet"/>.</summary>
    internal int Index { get; }

    /// <summary>Имя правила вида «ru/dash/main».</summary>
    public string Name { get; }

    /// <summary>Фаза конвейера, на которой правило применяется.</summary>
    public RulePhase Phase { get; }

    /// <summary>Разбирает имя правила. Возвращает false, если такого правила нет.</summary>
    public static bool TryParse(string name, out RuleId rule)
    {
        foreach (RuleId candidate in Registry.All)
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                rule = candidate;
                return true;
            }
        }

        rule = default;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(RuleId other) => Index == other.Index;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RuleId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Index;

    /// <inheritdoc />
    public override string ToString() => Name;

    /// <summary>Правила, общие для всех языков.</summary>
    public static class Common
    {
        /// <summary>Пунктуация.</summary>
        public static class Punctuation
        {
            /// <summary>Расстановка кавычек правильного вида.</summary>
            public static RuleId Quote => Registry.Quote;

            /// <summary>Замена трёх точек на многоточие.</summary>
            public static RuleId Hellip => Registry.Hellip;

            /// <summary>Расстановка правильного апострофа.</summary>
            public static RuleId Apostrophe => Registry.Apostrophe;
        }

        /// <summary>Пробелы.</summary>
        public static class Space
        {
            /// <summary>Удаление повторяющихся пробелов.</summary>
            public static RuleId DelRepeatSpace => Registry.DelRepeatSpace;

            /// <summary>Удаление пробелов перед знаками пунктуации.</summary>
            public static RuleId DelBeforePunctuation => Registry.DelBeforePunctuation;

            /// <summary>Пробел после запятой.</summary>
            public static RuleId AfterComma => Registry.AfterComma;
        }

        /// <summary>Неразрывные пробелы, общие для языков.</summary>
        public static class Nbsp
        {
            /// <summary>Неразрывный пробел после короткого слова.</summary>
            public static RuleId AfterShortWord => Registry.AfterShortWord;
        }
    }

    /// <summary>Правила русского языка.</summary>
    public static class Ru
    {
        /// <summary>Тире и дефисы.</summary>
        public static class Dash
        {
            /// <summary>Замена дефиса на тире.</summary>
            public static RuleId Main => Registry.DashMain;

            /// <summary>Тире в прямой речи.</summary>
            public static RuleId DirectSpeech => Registry.DashDirectSpeech;

            /// <summary>Замена дефиса на тире в годах.</summary>
            public static RuleId Years => Registry.DashYears;
        }

        /// <summary>Неразрывные пробелы русского языка.</summary>
        public static class Nbsp
        {
            /// <summary>Неразрывный пробел в сокращениях, например «т. д.».</summary>
            public static RuleId Abbr => Registry.NbspAbbr;

            /// <summary>Привязка инициалов к фамилии.</summary>
            public static RuleId Initials => Registry.NbspInitials;
        }

        /// <summary>Пунктуация русского языка.</summary>
        public static class Punctuation
        {
            /// <summary>Расстановка запятых перед «а» и «но». По умолчанию выключено.</summary>
            public static RuleId Ano => Registry.Ano;
        }

        /// <summary>Исправление опечаток.</summary>
        public static class Typo
        {
            /// <summary>Замена латинских букв на русские при ошибке раскладки. По умолчанию выключено.</summary>
            public static RuleId SwitchingKeyboardLayout => Registry.KeyboardLayout;
        }
    }

    internal static class Registry
    {
        public static readonly RuleId Quote = new(0, "common/punctuation/quote", RulePhase.Scan);
        public static readonly RuleId Hellip = new(1, "common/punctuation/hellip", RulePhase.Scan);
        public static readonly RuleId Apostrophe = new(2, "common/punctuation/apostrophe", RulePhase.Scan);
        public static readonly RuleId DelRepeatSpace = new(3, "common/space/delRepeatSpace", RulePhase.Scan);
        public static readonly RuleId DelBeforePunctuation = new(4, "common/space/delBeforePunctuation", RulePhase.Scan);
        public static readonly RuleId AfterComma = new(5, "common/space/afterComma", RulePhase.Scan);
        public static readonly RuleId AfterShortWord = new(6, "common/nbsp/afterShortWord", RulePhase.Bind);
        public static readonly RuleId DashMain = new(7, "ru/dash/main", RulePhase.Scan);
        public static readonly RuleId DashDirectSpeech = new(8, "ru/dash/directSpeech", RulePhase.Scan);
        public static readonly RuleId DashYears = new(9, "ru/dash/years", RulePhase.Scan);
        public static readonly RuleId NbspAbbr = new(10, "ru/nbsp/abbr", RulePhase.Bind);
        public static readonly RuleId NbspInitials = new(11, "ru/nbsp/initials", RulePhase.Bind);
        public static readonly RuleId Ano = new(12, "ru/punctuation/ano", RulePhase.Scan);
        public static readonly RuleId KeyboardLayout = new(13, "ru/typo/switchingKeyboardLayout", RulePhase.Bind);

        public static readonly RuleId[] All =
        [
            Quote, Hellip, Apostrophe, DelRepeatSpace, DelBeforePunctuation, AfterComma,
            AfterShortWord, DashMain, DashDirectSpeech, DashYears, NbspAbbr, NbspInitials,
            Ano, KeyboardLayout,
        ];

        /// <summary>Правила, выключенные в пресете Default: меняют смысл текста.</summary>
        public static readonly RuleId[] Unsafe = [Ano, KeyboardLayout];
    }
}
```

Создать `src/Typographer/Rules/RuleSet.cs`:

```csharp
using System.Collections;

namespace Typographer.Rules;

/// <summary>Иммутабельное множество включённых правил.</summary>
/// <remarks>Внутри — битовая маска, проверка включённости не аллоцирует.</remarks>
public sealed class RuleSet : IReadOnlyCollection<RuleId>
{
    private readonly ulong _low;
    private readonly ulong _high;

    private RuleSet(ulong low, ulong high)
    {
        _low = low;
        _high = high;
    }

    /// <summary>Пустое множество: типограф ничего не меняет.</summary>
    public static RuleSet None { get; } = new(0, 0);

    /// <summary>Все правила, включая рискованные и требующие внешнего CSS.</summary>
    public static RuleSet All { get; } = FromRules(RuleId.Registry.All);

    /// <summary>Безопасная типографика: всё, что меняет только оформление. Пресет по умолчанию.</summary>
    public static RuleSet Default { get; } = All.Without(RuleId.Registry.Unsafe);

    /// <summary>Кавычки, тире и многоточие — минимум.</summary>
    public static RuleSet Minimal { get; } = FromRules(
    [
        RuleId.Common.Punctuation.Quote,
        RuleId.Common.Punctuation.Hellip,
        RuleId.Ru.Dash.Main,
    ]);

    /// <summary>Поведение веб-сервиса Артемия Лебедева.</summary>
    public static RuleSet Lebedev { get; } = Default;

    /// <summary>Паритет дефолтов с JS-typograf.</summary>
    public static RuleSet Typograf { get; } = Default.With(RuleId.Ru.Punctuation.Ano, RuleId.Ru.Typo.SwitchingKeyboardLayout);

    /// <summary>Строго по ГОСТ Р 7.0.110-2025.</summary>
    public static RuleSet Gost { get; } = Default;

    /// <summary>Количество включённых правил.</summary>
    public int Count => BitCount(_low) + BitCount(_high);

    /// <summary>Проверяет, включено ли правило.</summary>
    public bool Contains(RuleId rule)
        => rule.Index < 64
            ? (_low & (1UL << rule.Index)) != 0
            : (_high & (1UL << (rule.Index - 64))) != 0;

    /// <summary>Возвращает новое множество с добавленными правилами.</summary>
    public RuleSet With(params RuleId[] rules)
    {
        ulong low = _low, high = _high;
        foreach (RuleId rule in rules)
        {
            if (rule.Index < 64)
            {
                low |= 1UL << rule.Index;
            }
            else
            {
                high |= 1UL << (rule.Index - 64);
            }
        }

        return new RuleSet(low, high);
    }

    /// <summary>Возвращает новое множество без указанных правил.</summary>
    public RuleSet Without(params RuleId[] rules)
    {
        ulong low = _low, high = _high;
        foreach (RuleId rule in rules)
        {
            if (rule.Index < 64)
            {
                low &= ~(1UL << rule.Index);
            }
            else
            {
                high &= ~(1UL << (rule.Index - 64));
            }
        }

        return new RuleSet(low, high);
    }

    /// <inheritdoc />
    public IEnumerator<RuleId> GetEnumerator()
    {
        foreach (RuleId rule in RuleId.Registry.All)
        {
            if (Contains(rule))
            {
                yield return rule;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static RuleSet FromRules(RuleId[] rules) => None.With(rules);

    private static int BitCount(ulong value)
    {
#if NET8_0_OR_GREATER
        return System.Numerics.BitOperations.PopCount(value);
#else
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
#endif
    }
}
```

- [ ] **Step 4: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Tests --filter RuleSetTests`
Ожидается: PASS, 8 тестов.

- [ ] **Step 5: Коммит**

```bash
git add src/Typographer/Rules tests/Typographer.Tests/Rules
git commit -m "feat: реестр правил и иммутабельные пресеты"
```

---

### Task 5: Сквозной конвейер без правил

Задача доказывает главную гарантию до того, как появится хоть одно правило: с `RuleSet.None` типограф возвращает вход байт-в-байт, а для строки — тот же экземпляр.

**Files:**
- Create: `src/Typographer/EntityMode.cs`
- Create: `src/Typographer/TextOptions.cs`
- Create: `src/Typographer/HtmlOptions.cs`
- Create: `src/Typographer/TextTypograf.cs`
- Create: `src/Typographer/HtmlTypograf.cs`
- Create: `src/Typographer/Typograf.cs`
- Delete: `src/Typographer/RussianTypographer.cs`
- Delete: `tests/Typographer.Tests/RussianTypographerTests.cs`
- Test: `tests/Typographer.Tests/PipelineTests.cs`

**Interfaces:**
- Consumes: `CharBuffer` (Task 1), `MarkupScanner` (Task 3), `RuleSet` (Task 4).
- Produces: `HtmlTypograf.Process(string)`, `HtmlTypograf.Process(ReadOnlySpan<char>, IBufferWriter<char>)`, те же два метода у `TextTypograf`, фасад `Typograf.Html(string)` и `Typograf.PlainText(string)`, типы `HtmlOptions`, `TextOptions`, `EntityMode`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/PipelineTests.cs`:

```csharp
using System.Buffers;
using Typographer.Rules;

namespace Typographer.Tests;

public class PipelineTests
{
    [Fact]
    public void БезПравилВозвращаетТотЖеЭкземплярСтроки()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None });
        string source = "<p>Он - человек</p>";

        Assert.Same(source, typograf.Process(source));
    }

    [Fact]
    public void БезПравилРазметкаКопируетсяБайтВБайт()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None });
        string source = "<a title=\"a>b\">x</a><code>a - b</code><!-- c -->";

        Assert.Equal(source, typograf.Process(source));
    }

    [Fact]
    public void ПишетВBufferWriter()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None });
        var writer = new ArrayBufferWriter<char>();

        typograf.Process("<p>текст</p>".AsSpan(), writer);

        Assert.Equal("<p>текст</p>", writer.WrittenSpan.ToString());
    }

    [Fact]
    public void ФасадРаботаетБезНастроек()
    {
        Assert.NotNull(Typograf.Html("текст"));
        Assert.NotNull(Typograf.PlainText("текст"));
    }

    [Fact]
    public void NullБросаетArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Typograf.Html(null!));
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter PipelineTests`
Ожидается: ошибка компиляции «HtmlTypograf не найден».

- [ ] **Step 3: Удалить заглушку каркаса**

```bash
git rm src/Typographer/RussianTypographer.cs tests/Typographer.Tests/RussianTypographerTests.cs
```

- [ ] **Step 4: Реализовать типы настроек**

Создать `src/Typographer/EntityMode.cs`:

```csharp
namespace Typographer;

/// <summary>Как выводить типографские символы в HTML.</summary>
/// <remarks>Значения соответствуют параметру entityType веб-сервиса «Типографа» Артемия Лебедева.</remarks>
public enum EntityMode
{
    /// <summary>Символами как есть. entityType = 0.</summary>
    Symbols = 0,

    /// <summary>Буквенными кодами сущностей. entityType = 1.</summary>
    Named = 1,

    /// <summary>Числовыми кодами сущностей. entityType = 2.</summary>
    Numeric = 2,

    /// <summary>Невидимые символы — кодами, видимые — символами. entityType = 3.</summary>
    Mixed = 3,
}
```

Создать `src/Typographer/TextOptions.cs`:

```csharp
using Typographer.Rules;

namespace Typographer;

/// <summary>Настройки типографирования обычного текста.</summary>
public sealed record TextOptions
{
    /// <summary>Набор включённых правил. По умолчанию — безопасная типографика.</summary>
    public RuleSet Rules { get; init; } = RuleSet.Default;

    /// <summary>Предел длины результата в символах. Ноль — без ограничения.</summary>
    public int MaxOutputLength { get; init; }

    /// <summary>Настройки по умолчанию.</summary>
    public static TextOptions Default { get; } = new();
}
```

Создать `src/Typographer/HtmlOptions.cs`:

```csharp
using Typographer.Rules;

namespace Typographer;

/// <summary>Настройки типографирования HTML-фрагмента.</summary>
public sealed record HtmlOptions
{
    /// <summary>Набор включённых правил. По умолчанию — безопасная типографика.</summary>
    public RuleSet Rules { get; init; } = RuleSet.Default;

    /// <summary>Как выводить типографские символы.</summary>
    public EntityMode Entities { get; init; } = EntityMode.Symbols;

    /// <summary>Заменять перевод строки тегом переноса.</summary>
    public bool UseBr { get; init; }

    /// <summary>Оборачивать абзацы в теги абзаца.</summary>
    public bool UseP { get; init; }

    /// <summary>Максимальное число слов, объединяемых в неразрывный блок. Ноль — не объединять.</summary>
    public int MaxNobr { get; init; }

    /// <summary>Предел длины результата в символах. Ноль — без ограничения.</summary>
    public int MaxOutputLength { get; init; }

    /// <summary>Настройки по умолчанию.</summary>
    public static HtmlOptions Default { get; } = new();
}
```

- [ ] **Step 5: Реализовать конвейер**

Создать `src/Typographer/HtmlTypograf.cs`:

```csharp
using System.Buffers;
using Typographer.Internal;

namespace Typographer;

/// <summary>Типограф для HTML-фрагментов. Иммутабелен и потокобезопасен.</summary>
public sealed class HtmlTypograf
{
    private readonly HtmlOptions _options;

    /// <summary>Создаёт типограф с указанными настройками.</summary>
    /// <param name="options">Настройки; null — настройки по умолчанию.</param>
    public HtmlTypograf(HtmlOptions? options = null) => _options = options ?? HtmlOptions.Default;

    /// <summary>Типограф с настройками по умолчанию.</summary>
    public static HtmlTypograf Default { get; } = new();

    /// <summary>Типографирует HTML-фрагмент.</summary>
    /// <param name="html">Исходный фрагмент.</param>
    /// <returns>Обработанный фрагмент. Если правок нет — тот же экземпляр строки.</returns>
    public string Process(string html)
    {
        Throw.IfNull(html, nameof(html));

        var buffer = new CharBuffer(html.Length + (html.Length >> 2), _options.MaxOutputLength);
        try
        {
            Run(html.AsSpan(), ref buffer);
            ReadOnlySpan<char> result = buffer.AsSpan();
            return result.SequenceEqual(html.AsSpan()) ? html : result.ToString();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Типографирует HTML-фрагмент, записывая результат в приёмник без промежуточной строки.</summary>
    /// <param name="html">Исходный фрагмент.</param>
    /// <param name="destination">Приёмник результата.</param>
    public void Process(ReadOnlySpan<char> html, IBufferWriter<char> destination)
    {
        Throw.IfNull(destination, nameof(destination));

        var buffer = new CharBuffer(html.Length + (html.Length >> 2), _options.MaxOutputLength);
        try
        {
            Run(html, ref buffer);
            ReadOnlySpan<char> result = buffer.AsSpan();
            result.CopyTo(destination.GetSpan(result.Length));
            destination.Advance(result.Length);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    private void Run(ReadOnlySpan<char> source, ref CharBuffer buffer)
    {
        var scanner = new MarkupScanner(source);
        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = source.Slice(segment.Start, segment.Length);
            if (segment.Kind == SegmentKind.Text)
            {
                TextScanner.Run(slice, _options.Rules, ref buffer);
            }
            else
            {
                buffer.Write(slice);
            }
        }
    }
}
```

Создать `src/Typographer/TextTypograf.cs` — тот же код, но без `MarkupScanner`: весь вход считается текстом.

```csharp
using System.Buffers;
using Typographer.Internal;

namespace Typographer;

/// <summary>Типограф для обычного текста. Иммутабелен и потокобезопасен.</summary>
public sealed class TextTypograf
{
    private readonly TextOptions _options;

    /// <summary>Создаёт типограф с указанными настройками.</summary>
    /// <param name="options">Настройки; null — настройки по умолчанию.</param>
    public TextTypograf(TextOptions? options = null) => _options = options ?? TextOptions.Default;

    /// <summary>Типограф с настройками по умолчанию.</summary>
    public static TextTypograf Default { get; } = new();

    /// <summary>Типографирует текст.</summary>
    /// <param name="text">Исходный текст.</param>
    /// <returns>Обработанный текст. Если правок нет — тот же экземпляр строки.</returns>
    public string Process(string text)
    {
        Throw.IfNull(text, nameof(text));

        var buffer = new CharBuffer(text.Length + (text.Length >> 2), _options.MaxOutputLength);
        try
        {
            TextScanner.Run(text.AsSpan(), _options.Rules, ref buffer);
            ReadOnlySpan<char> result = buffer.AsSpan();
            return result.SequenceEqual(text.AsSpan()) ? text : result.ToString();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Типографирует текст, записывая результат в приёмник без промежуточной строки.</summary>
    /// <param name="text">Исходный текст.</param>
    /// <param name="destination">Приёмник результата.</param>
    public void Process(ReadOnlySpan<char> text, IBufferWriter<char> destination)
    {
        Throw.IfNull(destination, nameof(destination));

        var buffer = new CharBuffer(text.Length + (text.Length >> 2), _options.MaxOutputLength);
        try
        {
            TextScanner.Run(text, _options.Rules, ref buffer);
            ReadOnlySpan<char> result = buffer.AsSpan();
            result.CopyTo(destination.GetSpan(result.Length));
            destination.Advance(result.Length);
        }
        finally
        {
            buffer.Dispose();
        }
    }
}
```

Создать `src/Typographer/Typograf.cs`:

```csharp
namespace Typographer;

/// <summary>Типографирование с настройками по умолчанию.</summary>
public static class Typograf
{
    /// <summary>Типографирует HTML-фрагмент настройками по умолчанию.</summary>
    /// <param name="html">Исходный фрагмент.</param>
    /// <returns>Обработанный фрагмент.</returns>
    public static string Html(string html) => HtmlTypograf.Default.Process(html);

    /// <summary>Типографирует обычный текст настройками по умолчанию.</summary>
    /// <param name="text">Исходный текст.</param>
    /// <returns>Обработанный текст.</returns>
    public static string PlainText(string text) => TextTypograf.Default.Process(text);
}
```

Создать заглушку `src/Typographer/Internal/TextScanner.cs`, которая пока просто копирует вход — правила добавит Task 7:

```csharp
using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>Фаза Scan: посимвольное применение правил к текстовому узлу.</summary>
internal static class TextScanner
{
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)
        => buffer.Write(source);
}
```

`ArgumentNullException.ThrowIfNull` появился в .NET 6 и на `netstandard2.0` недоступен, а `PolySharp` полифилит атрибуты и языковые фичи, но не методы BCL. Поэтому создать `src/Typographer/Internal/Throw.cs`:

```csharp
namespace Typographer.Internal;

/// <summary>Проверки аргументов, доступные на всех целевых платформах.</summary>
internal static class Throw
{
    public static void IfNull(object? value, string paramName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}
```

- [ ] **Step 6: Убедиться, что тесты проходят**

Выполнить: `dotnet test`
Ожидается: PASS, все тесты, включая ранее написанные. Сборка проходит на всех трёх TFM.

- [ ] **Step 7: Коммит**

```bash
git add -A
git commit -m "feat: сквозной конвейер и публичное API"
```

---

### Task 6: Кодирование сущностей (фаза Emit)

**Files:**
- Create: `src/Typographer/Internal/Emitter.cs`
- Modify: `src/Typographer/HtmlTypograf.cs` (пропустить результат через `Emitter`)
- Test: `tests/Typographer.Tests/Internal/EmitterTests.cs`

**Interfaces:**
- Consumes: `EntityTable` (Task 2), `CharBuffer` (Task 1), `EntityMode` (Task 5).
- Produces: `internal static class Emitter` с методом `void Encode(ReadOnlySpan<char> source, EntityMode mode, ref CharBuffer destination)`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/Internal/EmitterTests.cs`:

```csharp
using Typographer.Internal;

namespace Typographer.Tests.Internal;

public class EmitterTests
{
    private static string Encode(string source, EntityMode mode)
    {
        var buffer = new CharBuffer(source.Length);
        try
        {
            Emitter.Encode(source.AsSpan(), mode, ref buffer);
            return buffer.AsSpan().ToString();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Fact]
    public void Symbols_ОставляетСимволыКакЕсть()
        => Assert.Equal("«а б»", Encode("«а б»", EntityMode.Symbols));

    [Fact]
    public void Named_КодируетБуквеннымиИменами()
        => Assert.Equal("&laquo;а&nbsp;б&raquo;", Encode("«а б»", EntityMode.Named));

    [Fact]
    public void Numeric_КодируетЧисловымиКодами()
        => Assert.Equal("&#171;а&#160;б&#187;", Encode("«а б»", EntityMode.Numeric));

    [Fact]
    public void Mixed_КодируетТолькоНевидимые()
        => Assert.Equal("«а&nbsp;б»", Encode("«а б»", EntityMode.Mixed));

    [Fact]
    public void НеТрогаетОбычныеСимволы()
        => Assert.Equal("<b>текст</b> & ещё", Encode("<b>текст</b> & ещё", EntityMode.Named));
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter EmitterTests`
Ожидается: ошибка компиляции «Emitter не найден».

- [ ] **Step 3: Реализовать кодирование**

Создать `src/Typographer/Internal/Emitter.cs`:

```csharp
using System.Globalization;

namespace Typographer.Internal;

/// <summary>Фаза Emit: кодирование типографских символов по выбранному режиму.</summary>
internal static class Emitter
{
    public static void Encode(ReadOnlySpan<char> source, EntityMode mode, ref CharBuffer destination)
    {
        if (mode == EntityMode.Symbols)
        {
            destination.Write(source);
            return;
        }

        foreach (char c in source)
        {
            string? name = EntityTable.NameOf(c);
            bool encode = name is not null
                && (mode != EntityMode.Mixed || EntityTable.IsInvisible(c));

            if (!encode)
            {
                destination.Write(c);
                continue;
            }

            destination.Write('&');
            if (mode == EntityMode.Numeric)
            {
                destination.Write('#');
                destination.Write(((int)c).ToString(CultureInfo.InvariantCulture).AsSpan());
            }
            else
            {
                destination.Write(name.AsSpan());
            }

            destination.Write(';');
        }
    }
}
```

- [ ] **Step 4: Подключить фазу к HTML-типографу**

В `src/Typographer/HtmlTypograf.cs` заменить тело `Run` так, чтобы текстовые сегменты после `TextScanner` проходили через `Emitter`, а разметка — нет:

```csharp
    private void Run(ReadOnlySpan<char> source, ref CharBuffer buffer)
    {
        var scanner = new MarkupScanner(source);
        while (scanner.TryRead(out Segment segment))
        {
            ReadOnlySpan<char> slice = source.Slice(segment.Start, segment.Length);
            if (segment.Kind != SegmentKind.Text)
            {
                buffer.Write(slice);
                continue;
            }

            var scanned = new CharBuffer(slice.Length + 8, _options.MaxOutputLength);
            try
            {
                TextScanner.Run(slice, _options.Rules, ref scanned);
                Emitter.Encode(scanned.AsSpan(), _options.Entities, ref buffer);
            }
            finally
            {
                scanned.Dispose();
            }
        }
    }
```

Промежуточный буфер берётся из того же пула и живёт в пределах сегмента — аллокаций в куче не добавляет.

- [ ] **Step 5: Убедиться, что тесты проходят**

Выполнить: `dotnet test`
Ожидается: PASS, все тесты.

- [ ] **Step 6: Коммит**

```bash
git add src/Typographer/Internal/Emitter.cs src/Typographer/HtmlTypograf.cs tests/Typographer.Tests/Internal/EmitterTests.cs
git commit -m "feat: кодирование сущностей по EntityMode"
```

---

### Task 7: Правила пробелов и пунктуации

**Files:**
- Modify: `src/Typographer/Internal/TextScanner.cs`
- Test: `tests/Typographer.Tests/Rules/SpaceRulesTests.cs`

**Interfaces:**
- Consumes: `CharBuffer`, `RuleSet`, `RuleId`.
- Produces: рабочая реализация `TextScanner.Run`, применяющая `common/space/delRepeatSpace`, `common/space/delBeforePunctuation`, `common/space/afterComma`, `common/punctuation/hellip`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/Rules/SpaceRulesTests.cs`:

```csharp
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class SpaceRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Space.DelRepeatSpace)
            .With(RuleId.Common.Space.DelBeforePunctuation)
            .With(RuleId.Common.Space.AfterComma)
            .With(RuleId.Common.Punctuation.Hellip),
    }).Process(source);

    [Theory]
    [InlineData("два  пробела", "два пробела")]
    [InlineData("три   пробела", "три пробела")]
    public void УдаляетПовторяющиесяПробелы(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("слово , запятая", "слово, запятая")]
    [InlineData("слово !", "слово!")]
    [InlineData("слово ?", "слово?")]
    public void УдаляетПробелПередПунктуацией(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Fact]
    public void ДобавляетПробелПослеЗапятой()
        => Assert.Equal("раз, два, три", Run("раз,два,три"));

    [Fact]
    public void НеТрогаетЗапятуюВЧисле()
        => Assert.Equal("3,14", Run("3,14"));

    [Fact]
    public void ЗаменяетТриТочкиНаМноготочие()
        => Assert.Equal("вот…", Run("вот..."));

    [Fact]
    public void НеТрогаетЧетыреТочки()
        => Assert.Equal("вот....", Run("вот...."));
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter SpaceRulesTests`
Ожидается: FAIL — заглушка `TextScanner` копирует вход без изменений.

- [ ] **Step 3: Реализовать сканер пробелов и пунктуации**

Заменить содержимое `src/Typographer/Internal/TextScanner.cs`:

```csharp
using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>Фаза Scan: посимвольное применение правил к текстовому узлу.</summary>
internal static class TextScanner
{
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)
    {
        bool delRepeatSpace = rules.Contains(RuleId.Common.Space.DelRepeatSpace);
        bool delBeforePunctuation = rules.Contains(RuleId.Common.Space.DelBeforePunctuation);
        bool afterComma = rules.Contains(RuleId.Common.Space.AfterComma);
        bool hellip = rules.Contains(RuleId.Common.Punctuation.Hellip);

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];

            if (hellip && c == '.' && IsExactlyThreeDots(source, i))
            {
                buffer.Write(Chars.Hellip);
                i += 2;
                continue;
            }

            if (c == ' ')
            {
                if (delRepeatSpace && i + 1 < source.Length && source[i + 1] == ' ')
                {
                    continue;
                }

                if (delBeforePunctuation && i + 1 < source.Length && IsPunctuation(source[i + 1]))
                {
                    continue;
                }

                buffer.Write(c);
                continue;
            }

            buffer.Write(c);

            if (afterComma && c == ',' && NeedsSpaceAfterComma(source, i))
            {
                buffer.Write(' ');
            }
        }
    }

    private static bool IsPunctuation(char c) => c is ',' or '.' or ';' or ':' or '!' or '?';

    private static bool NeedsSpaceAfterComma(ReadOnlySpan<char> source, int index)
    {
        if (index + 1 >= source.Length || source[index + 1] == ' ')
        {
            return false;
        }

        // Запятая внутри числа — десятичный разделитель, пробел не нужен.
        bool digitBefore = index > 0 && char.IsDigit(source[index - 1]);
        bool digitAfter = char.IsDigit(source[index + 1]);
        return !(digitBefore && digitAfter);
    }

    private static bool IsExactlyThreeDots(ReadOnlySpan<char> source, int index)
    {
        if (index + 2 >= source.Length || source[index + 1] != '.' || source[index + 2] != '.')
        {
            return false;
        }

        bool dotBefore = index > 0 && source[index - 1] == '.';
        bool dotAfter = index + 3 < source.Length && source[index + 3] == '.';
        return !dotBefore && !dotAfter;
    }
}
```

- [ ] **Step 4: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Tests --filter SpaceRulesTests`
Ожидается: PASS, 9 тестов.

- [ ] **Step 5: Коммит**

```bash
git add src/Typographer/Internal/TextScanner.cs tests/Typographer.Tests/Rules/SpaceRulesTests.cs
git commit -m "feat: правила пробелов, пунктуации и многоточия"
```

---

### Task 8: Кавычки и апостроф

**Files:**
- Create: `src/Typographer/Internal/QuoteStack.cs`
- Modify: `src/Typographer/Internal/TextScanner.cs`
- Test: `tests/Typographer.Tests/Rules/QuoteRulesTests.cs`

**Interfaces:**
- Consumes: `CharBuffer`, `Chars`, `RuleSet`.
- Produces: `internal struct QuoteStack` с членами `int Depth`, `char Open()`, `char Close()`, `bool IsEmpty`; расширенный `TextScanner`, реализующий `common/punctuation/quote` и `common/punctuation/apostrophe`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/Rules/QuoteRulesTests.cs`:

```csharp
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class QuoteRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Punctuation.Quote)
            .With(RuleId.Common.Punctuation.Apostrophe),
    }).Process(source);

    [Fact]
    public void ПростыеКавычкиСтановятсяЁлочками()
        => Assert.Equal("Он сказал «привет»", Run("Он сказал \"привет\""));

    [Fact]
    public void ВложенныеКавычкиСтановятсяЛапками()
        => Assert.Equal(
            "Эксперт уточнил: «в перечень включен закон „О стандартизации“»",
            Run("Эксперт уточнил: \"в перечень включен закон \"О стандартизации\"\""));

    [Fact]
    public void ТретийУровеньОдинарныеЛапки()
        => Assert.Equal("«а „б ‘в’ б“ а»", Run("\"а \"б \"в\" б\" а\""));

    [Theory]
    [InlineData("17\"", "17\"")]
    [InlineData("3' 25\"", "3' 25\"")]
    [InlineData("диагональ 15,6\"", "диагональ 15,6\"")]
    public void ШтрихиПослеЦифрОстаютсяПрямыми(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("д'Артаньян", "д’Артаньян")]
    [InlineData("O'Neil", "O’Neil")]
    public void АпострофМеждуБуквами(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Fact]
    public void ТочкаПередЗакрывающейПослеСокращения()
        => Assert.Equal("Он сказал: «Это важно, и т. д.»", Run("Он сказал: \"Это важно, и т. д.\""));
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter QuoteRulesTests`
Ожидается: FAIL — кавычки остаются прямыми.

- [ ] **Step 3: Реализовать стек кавычек**

Создать `src/Typographer/Internal/QuoteStack.cs`:

```csharp
namespace Typographer.Internal;

/// <summary>
/// Уровни кавычек русского набора: ёлочки, затем двойные лапки, затем одинарные лапки.
/// Источник: ГОСТ Р 7.0.110-2025, 16.3; справочник кавычек type.today.
/// </summary>
internal struct QuoteStack
{
    private const int MaxDepth = 8;

    private int _depth;

    public readonly int Depth => _depth;

    public readonly bool IsEmpty => _depth == 0;

    /// <summary>Открывает следующий уровень и возвращает открывающую кавычку.</summary>
    public char Open()
    {
        char quote = _depth switch
        {
            0 => Chars.Laquo,
            1 => Chars.Bdquo,
            _ => Chars.Lsquo,
        };

        if (_depth < MaxDepth)
        {
            _depth++;
        }

        return quote;
    }

    /// <summary>Закрывает текущий уровень и возвращает закрывающую кавычку.</summary>
    public char Close()
    {
        if (_depth > 0)
        {
            _depth--;
        }

        return _depth switch
        {
            0 => Chars.Raquo,
            1 => Chars.Ldquo,
            _ => Chars.Rsquo,
        };
    }
}
```

Ограничение `MaxDepth` — часть гарантии безопасности: глубина не растёт бесконечно на враждебном вводе, лишние уровни схлопываются в одинарные лапки.

- [ ] **Step 4: Добавить обработку кавычек в сканер**

В `TextScanner.Run` объявить локальные переменные и добавить ветки до общей записи символа:

```csharp
        bool quotes = rules.Contains(RuleId.Common.Punctuation.Quote);
        bool apostrophe = rules.Contains(RuleId.Common.Punctuation.Apostrophe);
        var quoteStack = new QuoteStack();
```

и внутри цикла, до проверки на пробел:

```csharp
            if (quotes && c == '"')
            {
                char previous = i > 0 ? source[i - 1] : '\0';
                bool inch = char.IsDigit(previous) && quoteStack.IsEmpty;
                if (inch)
                {
                    buffer.Write(c);
                    continue;
                }

                bool opening = previous is '\0' or ' ' or '(' or '[' or '\n' or Chars.Nbsp
                    || (quoteStack.IsEmpty && previous == ':');
                buffer.Write(opening ? quoteStack.Open() : quoteStack.Close());
                continue;
            }

            if (apostrophe && c == '\'')
            {
                bool letterBefore = i > 0 && char.IsLetter(source[i - 1]);
                bool letterAfter = i + 1 < source.Length && char.IsLetter(source[i + 1]);
                buffer.Write(letterBefore && letterAfter ? Chars.Rsquo : c);
                continue;
            }
```

Открывающая кавычка после двоеточия распознаётся только на нулевом уровне: это случай прямой цитаты `Он сказал: "…"`. На вложенных уровнях двоеточие внутри цитаты не должно открывать новый уровень.

- [ ] **Step 5: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Tests --filter QuoteRulesTests`
Ожидается: PASS, 8 тестов. Если тест `ТретийУровеньОдинарныеЛапки` не проходит, причина в определении открывающей и закрывающей: проверить, что после закрывающей кавычки следующая кавычка перед пробелом трактуется как закрывающая, а не открывающая.

- [ ] **Step 6: Коммит**

```bash
git add src/Typographer/Internal/QuoteStack.cs src/Typographer/Internal/TextScanner.cs tests/Typographer.Tests/Rules/QuoteRulesTests.cs
git commit -m "feat: кавычки трёх уровней, штрихи и апостроф"
```

---

### Task 9: Тире, дефис и минус

**Files:**
- Modify: `src/Typographer/Internal/TextScanner.cs`
- Test: `tests/Typographer.Tests/Rules/DashRulesTests.cs`

**Interfaces:**
- Consumes: `CharBuffer`, `Chars`, `RuleSet`.
- Produces: реализация `ru/dash/main`, `ru/dash/directSpeech`, `ru/dash/years` в `TextScanner`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/Rules/DashRulesTests.cs`:

```csharp
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class DashRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Ru.Dash.Main)
            .With(RuleId.Ru.Dash.DirectSpeech)
            .With(RuleId.Ru.Dash.Years),
    }).Process(source);

    [Fact]
    public void ДефисМеждуСловамиСПробеламиСтановитсяТире()
        => Assert.Equal("Он — человек", Run("Он - человек"));

    [Theory]
    [InlineData("из-за")]
    [InlineData("по-русски")]
    [InlineData("кто-то")]
    [InlineData("из-под")]
    public void ДефисВнутриСловаНеТрогаем(string source)
        => Assert.Equal(source, Run(source));

    [Fact]
    public void ДиапазонГодовКороткимТиреБезОтбивки()
        => Assert.Equal("1941—1945", Run("1941-1945"));

    [Fact]
    public void ТиреПрямойРечиВНачалеСтроки()
        => Assert.Equal("— Привет, — сказал он.", Run("- Привет, - сказал он."));

    [Fact]
    public void МинусПередЧисломНеСтановитсяТире()
        => Assert.Equal("от -5 до +5", Run("от -5 до +5"));

    [Fact]
    public void НеТрогаетДефисВUrl()
        => Assert.Equal("http://example.com/a-b", Run("http://example.com/a-b"));
}
```

Тест `ДиапазонГодовКороткимТиреБезОтбивки` следует ГОСТ Р 7.0.110-2025, 14.3: диапазон дат тире, не отбитым пробелами. Символ в ожидании — длинное тире, потому что ГОСТ 14.3 разрешает «тире или короткое тире» единообразно; пресет `Default` использует длинное. Пресет `Lebedev` даст `1941 — 1945`; это поведение появится в плане 2 вместе с пресетами.

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter DashRulesTests`
Ожидается: FAIL — дефисы остаются дефисами.

- [ ] **Step 3: Реализовать правила тире**

Добавить в `TextScanner.Run` объявления:

```csharp
        bool dashMain = rules.Contains(RuleId.Ru.Dash.Main);
        bool directSpeech = rules.Contains(RuleId.Ru.Dash.DirectSpeech);
        bool dashYears = rules.Contains(RuleId.Ru.Dash.Years);
```

и ветку обработки дефиса внутри цикла, до общей записи символа:

```csharp
            if (c == '-')
            {
                char previous = i > 0 ? source[i - 1] : '\0';
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                // Тире прямой речи: дефис в начале текста или строки, за ним пробел.
                if (directSpeech && next == ' ' && previous is '\0' or '\n')
                {
                    buffer.Write(Chars.MDash);
                    continue;
                }

                // Диапазон чисел: цифра с обеих сторон, без пробелов.
                if (dashYears && char.IsDigit(previous) && char.IsDigit(next))
                {
                    buffer.Write(Chars.MDash);
                    continue;
                }

                // Тире между словами: пробел с обеих сторон, справа не число.
                if (dashMain && previous == ' ' && next == ' ' && !IsNumberAhead(source, i + 2))
                {
                    buffer.PatchAt(buffer.Length - 1, Chars.Nbsp);
                    buffer.Write(Chars.MDash);
                    continue;
                }

                buffer.Write(c);
                continue;
            }
```

и вспомогательный метод:

```csharp
    private static bool IsNumberAhead(ReadOnlySpan<char> source, int index)
        => index < source.Length && char.IsDigit(source[index]);
```

Патч предыдущего пробела на неразрывный — это ровно та техника, ради которой в `CharBuffer` есть `PatchAt`: пробел уже записан, решение о его неразрывности принимается на следующем символе.

Минус перед числом (`от -5 до +5`) обрабатывается сам собой: `previous == ' '`, но `next == '5'` — ни одна ветка не срабатывает, дефис пишется как есть. Дефис в URL тоже: слева и справа буквы.

- [ ] **Step 4: Убедиться, что тесты проходят**

Выполнить: `dotnet test tests/Typographer.Tests --filter DashRulesTests`
Ожидается: PASS, 9 тестов.

- [ ] **Step 5: Прогнать весь набор — правила не должны конфликтовать**

Выполнить: `dotnet test`
Ожидается: PASS. Если `SpaceRulesTests` сломались, причина в порядке веток внутри цикла: ветка дефиса обязана стоять до общей записи символа, но после ветки пробела.

- [ ] **Step 6: Коммит**

```bash
git add src/Typographer/Internal/TextScanner.cs tests/Typographer.Tests/Rules/DashRulesTests.cs
git commit -m "feat: тире, дефис и минус по ГОСТ"
```

---

### Task 10: Неразрывные пробелы (фаза Bind)

**Files:**
- Create: `src/Typographer/Internal/Dictionaries.cs`
- Create: `src/Typographer/Internal/WordBinder.cs`
- Modify: `src/Typographer/TextTypograf.cs`, `src/Typographer/HtmlTypograf.cs` (вставить фазу между `Scan` и `Emit`)
- Test: `tests/Typographer.Tests/Rules/NbspRulesTests.cs`

**Interfaces:**
- Consumes: `CharBuffer`, `Chars`, `RuleSet`.
- Produces: `internal static class Dictionaries` с `bool IsShortWord(ReadOnlySpan<char> word)` и `bool IsAbbreviationPart(ReadOnlySpan<char> word)`; `internal static class WordBinder` с `void Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/Rules/NbspRulesTests.cs`:

```csharp
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class NbspRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Nbsp.AfterShortWord)
            .With(RuleId.Ru.Nbsp.Abbr)
            .With(RuleId.Ru.Nbsp.Initials),
    }).Process(source);

    [Fact]
    public void ПослеКороткогоСловаНеразрывныйПробел()
        => Assert.Equal("в доме на горе", Run("в доме на горе"));

    [Fact]
    public void ДлинноеСловоНеТрогаем()
        => Assert.Equal("дерево стоит", Run("дерево стоит"));

    [Fact]
    public void СокращениеТДСклеивается()
        => Assert.Equal("и т. д.", Run("и т. д."));

    [Fact]
    public void ИнициалыПривязываютсяКФамилии()
        => Assert.Equal("А. С. Пушкин", Run("А. С. Пушкин"));

    [Fact]
    public void ФамилияПередИнициаламиТоже()
        => Assert.Equal("Пушкин А. С.", Run("Пушкин А. С."));
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter NbspRulesTests`
Ожидается: FAIL — обычные пробелы остаются обычными.

- [ ] **Step 3: Реализовать словари**

Создать `src/Typographer/Internal/Dictionaries.cs`:

```csharp
namespace Typographer.Internal;

/// <summary>Словари, по которым фаза Bind принимает решения.</summary>
internal static class Dictionaries
{
    /// <summary>
    /// Короткое слово — предлог, союз или частица длиной до трёх букв,
    /// после которого перенос строки нежелателен. ГОСТ Р 7.0.110-2025, 9.4.
    /// </summary>
    public static bool IsShortWord(ReadOnlySpan<char> word) => word.Length is > 0 and <= 3;

    /// <summary>
    /// Проверяет, что слово может быть частью устойчивого сокращения:
    /// «т. д.», «т. п.», «т. е.», «н. э.», «г.», «в.».
    /// </summary>
    public static bool IsAbbreviationPart(ReadOnlySpan<char> word)
        => word.Length == 1 && word[0] is 'т' or 'д' or 'п' or 'е' or 'н' or 'э' or 'г' or 'в';
}
```

Сравнение идёт по символу, без `ToString()` — аллокаций на слово нет. `FrozenSet` появится в плане 2 для многобуквенных словарей (месяцы, «млн», «ООО», единицы измерения); там поиск пойдёт через `AlternateLookup<ReadOnlySpan<char>>` на `net8.0` и выше и через сравнение по длине на `netstandard2.0`.

- [ ] **Step 4: Реализовать фазу Bind**

Создать `src/Typographer/Internal/WordBinder.cs`:

```csharp
using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>
/// Фаза Bind: словарный проход по словам, расстановка неразрывных пробелов.
/// Работает по уже отсканированному тексту и патчит пробелы на месте.
/// </summary>
internal static class WordBinder
{
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)
    {
        bool afterShortWord = rules.Contains(RuleId.Common.Nbsp.AfterShortWord);
        bool abbr = rules.Contains(RuleId.Ru.Nbsp.Abbr);
        bool initials = rules.Contains(RuleId.Ru.Nbsp.Initials);

        int wordStart = -1;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            buffer.Write(c);

            if (char.IsLetter(c))
            {
                if (wordStart < 0)
                {
                    wordStart = i;
                }

                continue;
            }

            // Точка не завершает слово: она может быть частью сокращения или инициала,
            // решение принимает следующий за ней пробел.
            if (c == '.')
            {
                continue;
            }

            if (c != ' ')
            {
                wordStart = -1;
                continue;
            }

            if (wordStart < 0)
            {
                continue;
            }

            ReadOnlySpan<char> token = source.Slice(wordStart, i - wordStart);
            bool hasDot = token[token.Length - 1] == '.';
            ReadOnlySpan<char> letters = hasDot ? token.Slice(0, token.Length - 1) : token;

            bool bind =
                (afterShortWord && !hasDot && Dictionaries.IsShortWord(letters))
                || (abbr && hasDot && Dictionaries.IsAbbreviationPart(letters))
                || (initials && hasDot && letters.Length == 1 && char.IsUpper(letters[0]));

            if (bind)
            {
                buffer.PatchAt(buffer.Length - 1, Chars.Nbsp);
            }

            wordStart = -1;
        }
    }
}
```

Разбор `token` на `letters` и признак `hasDot` — не украшение: «в» и «т.» требуют разных правил, а слово, завершённое пробелом, физически включает в себя точку. Без этого разделения `А. С. Пушкин` не связался бы, потому что «А.» имеет длину два, а не один.

- [ ] **Step 5: Вставить фазу в конвейер**

В `TextTypograf.Process` и в `HtmlTypograf.Run` заменить одиночный вызов `TextScanner.Run` на цепочку из двух буферов: результат `Scan` подаётся в `Bind`, результат `Bind` — в `Emit` (для HTML) или прямо в выходной буфер (для текста). Пример для `TextTypograf.Process`:

```csharp
        var scanned = new CharBuffer(text.Length + 16, _options.MaxOutputLength);
        var buffer = new CharBuffer(text.Length + 16, _options.MaxOutputLength);
        try
        {
            TextScanner.Run(text.AsSpan(), _options.Rules, ref scanned);
            WordBinder.Run(scanned.AsSpan(), _options.Rules, ref buffer);
            ReadOnlySpan<char> result = buffer.AsSpan();
            return result.SequenceEqual(text.AsSpan()) ? text : result.ToString();
        }
        finally
        {
            scanned.Dispose();
            buffer.Dispose();
        }
```

Два буфера из пула — не аллокация в куче. Объединение фаз `Scan` и `Bind` в один проход возможно, но требует окна look-ahead на слово; делать это только если бенчмарк Task 13 покажет проигрыш цели.

- [ ] **Step 6: Убедиться, что тесты проходят**

Выполнить: `dotnet test`
Ожидается: PASS, все тесты.

- [ ] **Step 7: Коммит**

```bash
git add src/Typographer/Internal/Dictionaries.cs src/Typographer/Internal/WordBinder.cs src/Typographer/TextTypograf.cs src/Typographer/HtmlTypograf.cs tests/Typographer.Tests/Rules/NbspRulesTests.cs
git commit -m "feat: неразрывные пробелы, сокращения и инициалы"
```

---

### Task 11: Компоновка — br, p и nobr

**Files:**
- Create: `src/Typographer/Internal/LayoutWriter.cs`
- Modify: `src/Typographer/HtmlTypograf.cs`
- Test: `tests/Typographer.Tests/Rules/LayoutTests.cs`

**Interfaces:**
- Consumes: `CharBuffer`, `HtmlOptions`.
- Produces: `internal static class LayoutWriter` с методом `void Run(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/Typographer.Tests/Rules/LayoutTests.cs`:

```csharp
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class LayoutTests
{
    private static string Run(string source, HtmlOptions options) => new HtmlTypograf(options).Process(source);

    [Fact]
    public void UseBr_ЗаменяетПереводСтроки()
        => Assert.Equal(
            "первая<br />\nвторая",
            Run("первая\nвторая", new HtmlOptions { Rules = RuleSet.None, UseBr = true }));

    [Fact]
    public void UseP_ОборачиваетАбзацы()
        => Assert.Equal(
            "<p>первый</p>\n<p>второй</p>",
            Run("первый\n\nвторой", new HtmlOptions { Rules = RuleSet.None, UseP = true }));

    [Fact]
    public void БезФлаговНичегоНеДобавляется()
        => Assert.Equal(
            "первая\nвторая",
            Run("первая\nвторая", new HtmlOptions { Rules = RuleSet.None }));

    [Fact]
    public void MaxNobr_ОборачиваетНеразрывныеГруппы()
        => Assert.Equal(
            "<nobr>в доме</nobr> на горе",
            Run("в доме на горе", new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 }));
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test tests/Typographer.Tests --filter LayoutTests`
Ожидается: FAIL — переводы строк остаются как есть.

- [ ] **Step 3: Реализовать компоновку**

Создать `src/Typographer/Internal/LayoutWriter.cs`:

```csharp
namespace Typographer.Internal;

/// <summary>Фаза Layout: неразрывные блоки, переносы строк и абзацы.</summary>
internal static class LayoutWriter
{
    public static void Run(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)
    {
        if (options.MaxNobr > 0)
        {
            WriteWithNobr(source, options, ref buffer);
            return;
        }

        WriteBreaks(source, options, ref buffer);
    }

    private static void WriteWithNobr(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)
    {
        int index = 0;
        while (index < source.Length)
        {
            int groupStart = FindNobrGroupStart(source, index);
            if (groupStart < 0)
            {
                WriteBreaks(source.Slice(index), options, ref buffer);
                return;
            }

            WriteBreaks(source.Slice(index, groupStart - index), options, ref buffer);

            int groupEnd = FindNobrGroupEnd(source, groupStart, options.MaxNobr);
            buffer.Write("<nobr>");
            buffer.Write(source.Slice(groupStart, groupEnd - groupStart));
            buffer.Write("</nobr>");
            index = groupEnd;
        }
    }

    /// <summary>Начало неразрывной группы — начало слова, за которым идёт неразрывный пробел.</summary>
    private static int FindNobrGroupStart(ReadOnlySpan<char> source, int from)
    {
        int nbsp = source.Slice(from).IndexOf(Chars.Nbsp);
        if (nbsp < 0)
        {
            return -1;
        }

        int position = from + nbsp;
        while (position > from && !IsBoundary(source[position - 1]))
        {
            position--;
        }

        return position;
    }

    /// <summary>Конец группы — после указанного числа слов или на первом обычном пробеле.</summary>
    private static int FindNobrGroupEnd(ReadOnlySpan<char> source, int start, int maxWords)
    {
        int words = 1;
        int position = start;
        while (position < source.Length)
        {
            char c = source[position];
            if (c == Chars.Nbsp)
            {
                if (++words > maxWords)
                {
                    return position;
                }
            }
            else if (IsBoundary(c))
            {
                return position;
            }

            position++;
        }

        return source.Length;
    }

    private static bool IsBoundary(char c) => c is ' ' or '\n' or '\t';

    private static void WriteBreaks(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)
    {
        if (!options.UseBr && !options.UseP)
        {
            buffer.Write(source);
            return;
        }

        if (options.UseP)
        {
            WriteParagraphs(source, options, ref buffer);
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                buffer.Write("<br />");
            }

            buffer.Write(source[i]);
        }
    }

    private static void WriteParagraphs(ReadOnlySpan<char> source, HtmlOptions options, ref CharBuffer buffer)
    {
        int start = 0;
        bool first = true;
        while (start < source.Length)
        {
            int separator = source.Slice(start).IndexOf("\n\n".AsSpan(), StringComparison.Ordinal);
            ReadOnlySpan<char> paragraph = separator < 0
                ? source.Slice(start)
                : source.Slice(start, separator);

            if (!first)
            {
                buffer.Write('\n');
            }

            buffer.Write("<p>");
            if (options.UseBr)
            {
                for (int i = 0; i < paragraph.Length; i++)
                {
                    if (paragraph[i] == '\n')
                    {
                        buffer.Write("<br />");
                    }

                    buffer.Write(paragraph[i]);
                }
            }
            else
            {
                buffer.Write(paragraph);
            }

            buffer.Write("</p>");

            first = false;
            start = separator < 0 ? source.Length : start + separator + 2;
        }
    }
}
```

- [ ] **Step 4: Подключить фазу**

В `HtmlTypograf.Run` вставить `LayoutWriter.Run` между `WordBinder` и `Emitter`: результат `Bind` подаётся в `Layout`, результат `Layout` — в `Emit`. Фаза применяется только к текстовым сегментам.

- [ ] **Step 5: Убедиться, что тесты проходят**

Выполнить: `dotnet test`
Ожидается: PASS, все тесты.

- [ ] **Step 6: Коммит**

```bash
git add src/Typographer/Internal/LayoutWriter.cs src/Typographer/HtmlTypograf.cs tests/Typographer.Tests/Rules/LayoutTests.cs
git commit -m "feat: компоновка nobr, br и абзацев"
```

---

### Task 12: Гарантии как исполняемые тесты

Здесь гарантии из раздела 8 спецификации превращаются в тесты. Задача обязана быть последней содержательной: она проверяет весь конвейер целиком.

**Files:**
- Create: `tests/Typographer.Tests/Guarantees/IdempotencyTests.cs`
- Create: `tests/Typographer.Tests/Guarantees/MarkupIntegrityTests.cs`
- Create: `tests/Typographer.Tests/Guarantees/RobustnessTests.cs`
- Create: `tests/Typographer.Tests/Corpus/HardCases.cs`

**Interfaces:**
- Consumes: весь публичный API.
- Produces: ничего для кода; корпус `HardCases.All` доступен последующим планам как источник входных данных.

- [ ] **Step 1: Создать корпус сложных входов**

Создать `tests/Typographer.Tests/Corpus/HardCases.cs`:

```csharp
namespace Typographer.Tests.Corpus;

/// <summary>Входы, на которых типографы обычно ломаются. Источник — раздел 9 спецификации.</summary>
public static class HardCases
{
    public static TheoryData<string> All =>
    [
        "Эксперт уточнил: \"в перечень включен закон \"О стандартизации\"\"",
        "17\" и 3' 25\" и диагональ 15,6\"",
        "д'Артаньян, O'Neil, Кот-д'Ивуар",
        "из-за, по-русски, -5 °C, от -5 до +5",
        "- Привет, - сказал он. 1941-1945",
        "3.14 и 1.2.3 и 192.168.0.1 и 01.01.2020",
        "А.С. Пушкин, в 3 г. IV в. до н. э., ул. Ленина, д. 5, кв. 7",
        "10 км/ч, 100 %, 25 °C, № 5, § 3",
        "вот?.. и вот!.. и смайл :-)",
        "<code>a - b</code> и <a title=\"Не трогать - тире\">x</a>",
        "<b>сло</b>во и \"<a href=\"#\">Ссылка</a>\"",
        "<!--[if IE]>только тут<![endif]-->",
        "http://example.com/a-b?x=1&y=2 и user@example.com",
        "$<b>100</b> и 5<sup>2</sup> м",
        "Windows 10 - \"лучшая\" ОС",
        "﻿текст с BOM",
        "текст с уже стоящим nbsp",
        "",
        " ",
        "\n\n\n",
    ];
}
```

- [ ] **Step 2: Написать тесты идемпотентности**

Создать `tests/Typographer.Tests/Guarantees/IdempotencyTests.cs`:

```csharp
using Typographer.Rules;
using Typographer.Tests.Corpus;

namespace Typographer.Tests.Guarantees;

public class IdempotencyTests
{
    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void ПовторныйПрогонНичегоНеМеняет_Html(string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        string once = typograf.Process(source);

        Assert.Equal(once, typograf.Process(once));
    }

    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void ПовторныйПрогонНичегоНеМеняет_PlainText(string source)
    {
        var typograf = new TextTypograf(new TextOptions { Rules = RuleSet.Default });
        string once = typograf.Process(source);

        Assert.Equal(once, typograf.Process(once));
    }

    [Theory]
    [InlineData(EntityMode.Named)]
    [InlineData(EntityMode.Numeric)]
    [InlineData(EntityMode.Mixed)]
    public void ИдемпотентностьСохраняетсяПриКодированииСущностей(EntityMode mode)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default, Entities = mode });
        string once = typograf.Process("Он сказал: \"да\" - и ушёл");

        Assert.Equal(once, typograf.Process(once));
    }
}
```

Тест с кодированием сущностей — самый ценный в наборе: он поймает ситуацию, когда `&laquo;` на втором прогоне не распознаётся как кавычка и обрастает второй кавычкой. Если он падает, недостаёт фазы `Prepare` с декодированием сущностей; добавить её вызовом `EntityTable.TryDecode` в начале `HtmlTypograf.Run` для текстовых сегментов.

- [ ] **Step 3: Написать тесты целостности разметки**

Создать `tests/Typographer.Tests/Guarantees/MarkupIntegrityTests.cs`:

```csharp
using Typographer.Rules;
using Typographer.Tests.Corpus;

namespace Typographer.Tests.Guarantees;

public class MarkupIntegrityTests
{
    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void КоличествоТеговНеМеняется(string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        string result = typograf.Process(source);

        Assert.Equal(CountTags(source), CountTags(result));
    }

    [Fact]
    public void СодержимоеCodeНеТрогается()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });

        Assert.Contains("<code>a - b \"x\"</code>", typograf.Process("<code>a - b \"x\"</code>"));
    }

    [Fact]
    public void ЗначенияАтрибутовНеТрогаются()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });

        Assert.Contains("title=\"Не трогать - тире\"", typograf.Process("<a title=\"Не трогать - тире\">x</a>"));
    }

    private static int CountTags(string value)
    {
        int count = 0;
        for (int i = 0; i < value.Length - 1; i++)
        {
            if (value[i] == '<' && (char.IsLetter(value[i + 1]) || value[i + 1] == '/'))
            {
                count++;
            }
        }

        return count;
    }
}
```

`КоличествоТеговНеМеняется` заведомо не пройдёт при `UseBr` или `UseP` — поэтому тест гоняется на настройках без них. Это и есть граница гарантии: типограф не создаёт разметку из текста, кроме случаев, когда его об этом явно попросили флагом.

- [ ] **Step 4: Написать тесты устойчивости**

Создать `tests/Typographer.Tests/Guarantees/RobustnessTests.cs`:

```csharp
using Typographer.Rules;

namespace Typographer.Tests.Guarantees;

public class RobustnessTests
{
    [Theory]
    [InlineData("\ud800")]
    [InlineData("текст\udc00текст")]
    [InlineData("<<<<<<")]
    [InlineData("<a href=\"")]
    [InlineData("&&&&&")]
    [InlineData("\"\"\"\"\"\"\"\"\"\"")]
    public void НеБросаетНаБитомВводе(string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });

        Assert.NotNull(typograf.Process(source));
    }

    [Fact]
    public void СлучайныеДанныеНеЛомаютТипограф()
    {
        var random = new Random(20260908);
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        var chars = new char[256];

        for (int iteration = 0; iteration < 500; iteration++)
        {
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)random.Next(0x20, 0x4FF);
            }

            Assert.NotNull(typograf.Process(new string(chars)));
        }
    }

    [Fact]
    public void ПревышениеПределаДаётПонятноеИсключение()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default, MaxOutputLength = 10 });

        Assert.Throws<OutputTooLargeException>(() => typograf.Process(new string('а', 100)));
    }

    [Fact]
    public void БезПравокВозвращаетТотЖеЭкземпляр()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        string source = "простой текст без правок";

        Assert.Same(source, typograf.Process(source));
    }
}
```

- [ ] **Step 5: Убедиться, что тесты проходят**

Выполнить: `dotnet test`
Ожидается: PASS. Каждое падение здесь — настоящий баг ядра, а не проблема теста: чинить реализацию, а не ожидание.

- [ ] **Step 6: Коммит**

```bash
git add tests/Typographer.Tests/Guarantees tests/Typographer.Tests/Corpus
git commit -m "test: гарантии идемпотентности, целостности и устойчивости"
```

---

### Task 13: Бенчмарк и проверка цели по производительности

**Files:**
- Create: `bench/Typographer.Bench/Typographer.Bench.csproj`
- Create: `bench/Typographer.Bench/Program.cs`
- Create: `bench/Typographer.Bench/TypografBenchmarks.cs`
- Modify: `Typographer.slnx`
- Modify: `README.md` (раздел с результатами)

**Interfaces:**
- Consumes: публичный API ядра.
- Produces: воспроизводимый бенчмарк, фиксирующий цель: не медленнее двукратного `string.Replace` и ноль аллокаций на пути `IBufferWriter<char>`.

- [ ] **Step 1: Создать проект бенчмарков**

```bash
dotnet new console -o bench/Typographer.Bench -n Typographer.Bench -f net10.0
dotnet add bench/Typographer.Bench package BenchmarkDotNet
dotnet add bench/Typographer.Bench reference src/Typographer/Typographer.csproj
dotnet sln add bench/Typographer.Bench/Typographer.Bench.csproj
```

В `bench/Typographer.Bench/Typographer.Bench.csproj` добавить `<IsPackable>false</IsPackable>`.

- [ ] **Step 2: Написать бенчмарк**

Создать `bench/Typographer.Bench/TypografBenchmarks.cs`:

```csharp
using System.Buffers;
using BenchmarkDotNet.Attributes;
using Typographer;

namespace Typographer.Bench;

[MemoryDiagnoser]
public class TypografBenchmarks
{
    private const string Fragment =
        "<p>Он сказал: \"Это важно, и т. д.\" - и ушёл в 1941-1945 гг. " +
        "А.С. Пушкин писал про 10 км/ч и 100 % на 25 °C.</p>";

    private string _text = string.Empty;
    private HtmlTypograf _typograf = null!;
    private ArrayBufferWriter<char> _writer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _text = string.Concat(Enumerable.Repeat(Fragment, 200));
        _typograf = new HtmlTypograf();
        _writer = new ArrayBufferWriter<char>(_text.Length * 2);
    }

    [Benchmark(Baseline = true)]
    public string StringReplace() => _text.Replace(" - ", " — ").Replace("\"", "«");

    [Benchmark]
    public string Html() => _typograf.Process(_text);

    [Benchmark]
    public int HtmlToBufferWriter()
    {
        _writer.Clear();
        _typograf.Process(_text.AsSpan(), _writer);
        return _writer.WrittenCount;
    }
}
```

Создать `bench/Typographer.Bench/Program.cs`:

```csharp
using BenchmarkDotNet.Running;
using Typographer.Bench;

BenchmarkRunner.Run<TypografBenchmarks>();
```

- [ ] **Step 3: Прогнать бенчмарк**

Выполнить: `dotnet run -c Release --project bench/Typographer.Bench`
Ожидается: отчёт BenchmarkDotNet. Критерии приёмки:
- `Html` не более чем вдвое медленнее `StringReplace` по среднему времени;
- `HtmlToBufferWriter` показывает `Allocated` не более 0 байт на операцию после прогрева (колонка `Allocated` в отчёте `MemoryDiagnoser`).

Если критерий по аллокациям не выполнен, искать боксинг структуры `CharBuffer` или захват её в лямбду; если не выполнен критерий по времени — объединить фазы `Scan` и `Bind` в один проход с окном на слово.

- [ ] **Step 4: Записать результат в README**

Добавить в `README.md` раздел «Производительность» с таблицей из отчёта и датой прогона, машиной и версией среды выполнения. Цифры без указания машины бессмысленны.

- [ ] **Step 5: Коммит**

```bash
git add bench Typographer.slnx README.md
git commit -m "perf: бенчмарки ядра и фиксация цели"
```

---

## Self-Review

**Покрытие спецификации планом 1:**

| Раздел спецификации | Задача |
|---|---|
| 4.1 Точки входа | Task 5 |
| 4.2 Настройки | Task 5 |
| 4.3 Правила, `RuleSet`, пресеты | Task 4 |
| 5 Фаза Prepare | Task 12, шаг 2 (добавляется, если падает тест идемпотентности с сущностями) |
| 5.1 Фаза Protect | Task 3 |
| 5.2 Кавычки | Task 8 |
| 5.3 Тире | Task 9 |
| Фаза Bind | Task 10 |
| Фаза Layout | Task 11 |
| Фаза Emit | Task 6 |
| 8 Гарантии | Task 12 |
| 9 Тестирование, корпус | Task 12 |
| Цель по производительности | Task 13 |

**Не покрыто планом 1 и вынесено в следующие планы** (см. ниже): полный набор 107 правил, генерация справочника правил, оракул Лебедева, фаззинг, пакеты интеграций, CLI, сайт документации.

---

## Очередь планов

Что план 1 передаёт дальше — инварианты, на которые можно опираться, незакрытые
места и отложенные мелочи — собрано в `2026-09-08-input-for-plan-2.md`. Прочитать
до начала плана 2.

| План | Содержание | Зависит от |
|---|---|---|
| 1. Ядро | Настоящий документ | — |
| 2. Полный набор правил | Остальные 93 правила по группам, пресеты `Lebedev`, `Typograf`, `Gost` с их расхождениями, словари месяцев, единиц, адресных сокращений | 1 |
| 3. Качество | Golden-корпус в файлах, оракул `typograf.artlebedev.ru`, фаззинг SharpFuzz, тест на перестановку правил внутри фазы | 2 |
| 4. Интеграции | `Typographer.DependencyInjection`, `Typographer.AspNetCore`, `Typographer.Markdig`, `dotnet-typograf` | 2 |
| 5. Документация и релиз | Генератор `docs/rules.md` из метаданных, сайт DocFX на GitHub Pages, публикация в NuGet по тегу | 2, 4 |
