using System.Buffers;
using BenchmarkDotNet.Attributes;

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
