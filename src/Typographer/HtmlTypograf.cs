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
