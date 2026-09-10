using System.Buffers;
using BenchmarkDotNet.Attributes;
using Typographer.Rules;

namespace Typographer.Bench;

[MemoryDiagnoser]
public class TypografBenchmarks
{
    private const string Fragment =
        "<p>Он сказал: \"Это важно, и т. д.\" - и ушёл в 1941-1945 гг. " +
        "А.С. Пушкин писал про 10 км/ч и 100 % на 25 °C.</p>";

    private string _text = string.Empty;
    private HtmlTypograf _typograf = null!;
    private HtmlTypograf _typografNoRules = null!;
    private HtmlTypograf _typografNumeric = null!;
    private TextTypograf _textTypograf = null!;
    private HtmlTypograf _scanOnly = null!;
    private HtmlTypograf _bindOnly = null!;
    private ArrayBufferWriter<char> _writer = null!;

    /// <summary>Число символов во входном тексте — знаменатель для пропускной способности.</summary>
    public int TextLength { get; private set; }

    /// <summary>Входной текст: его же берёт грубый профиль по группам правил.</summary>
    public string Text_ => _text;

    [GlobalSetup]
    public void Setup()
    {
        _text = string.Concat(Enumerable.Repeat(Fragment, 200));
        TextLength = _text.Length;
        _typograf = new HtmlTypograf();
        _typografNoRules = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None });
        _typografNumeric = new HtmlTypograf(new HtmlOptions { Entities = EntityMode.Numeric });
        _textTypograf = new TextTypograf();

        // Профиль по фазам через наборы правил: публичного способа включить фазу целиком
        // нет, но фаза правила известна, и набор собирается из Default по ней.
        _scanOnly = new HtmlTypograf(new HtmlOptions { Rules = OnlyPhase(RulePhase.Scan) });
        _bindOnly = new HtmlTypograf(new HtmlOptions { Rules = OnlyPhase(RulePhase.Bind) });
        _writer = new ArrayBufferWriter<char>(_text.Length * 2);
    }

    private static RuleSet OnlyPhase(RulePhase phase)
        => RuleSet.None.With([.. RuleSet.Default.Where(rule => rule.Phase == phase)]);

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

    /// <summary>Диагностика аллокаций: числовой режим кодирования на пути записи в приёмник — обязан показывать ноль.</summary>
    [Benchmark]
    public int HtmlNumericToBufferWriter()
    {
        _writer.Clear();
        _typografNumeric.Process(_text.AsSpan(), _writer);
        return _writer.WrittenCount;
    }

    /// <summary>Диагностика: стоимость чистого плумбинга конвейера — сегментация разметки, три буфера, копирование — без единого правила.</summary>
    [Benchmark]
    public string HtmlNoRules() => _typografNoRules.Process(_text);

    /// <summary>Диагностика: только правила фазы Scan — посимвольный проход.</summary>
    [Benchmark]
    public string HtmlScanOnly() => _scanOnly.Process(_text);

    /// <summary>Диагностика: только правила фазы Bind — словарный проход по токенам.</summary>
    [Benchmark]
    public string HtmlBindOnly() => _bindOnly.Process(_text);

    /// <summary>Диагностика: обычный текст — без сегментации разметки и без фазы Emit.</summary>
    [Benchmark]
    public string Text() => _textTypograf.Process(_text);
}
