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
    private TextTypograf _textTypograf = null!;
    private ArrayBufferWriter<char> _writer = null!;

    /// <summary>Число символов во входном тексте — знаменатель для пропускной способности.</summary>
    public int TextLength { get; private set; }

    [GlobalSetup]
    public void Setup()
    {
        _text = string.Concat(Enumerable.Repeat(Fragment, 200));
        TextLength = _text.Length;
        _typograf = new HtmlTypograf();
        _typografNoRules = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None });
        _textTypograf = new TextTypograf();
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

    /// <summary>Диагностика: стоимость чистого плумбинга конвейера — сегментация разметки, три буфера, копирование — без единого правила.</summary>
    [Benchmark]
    public string HtmlNoRules() => _typografNoRules.Process(_text);

    /// <summary>Диагностика: обычный текст — без сегментации разметки и без фазы Emit.</summary>
    [Benchmark]
    public string Text() => _textTypograf.Process(_text);
}
