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
