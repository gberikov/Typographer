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

        var buffer = new CharBuffer(text.Length + (text.Length >> 2));
        try
        {
            Run(text.AsSpan(), ref buffer);
            ReadOnlySpan<char> result = buffer.AsSpan();
            Throw.IfTooLong(result.Length, _options.MaxOutputLength);
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

        var buffer = new CharBuffer(text.Length + (text.Length >> 2));
        try
        {
            Run(text, ref buffer);
            ReadOnlySpan<char> result = buffer.AsSpan();

            // Предел проверяется ДО записи в приёмник: приёмник не должен получить
            // половину результата, за которой следует исключение.
            Throw.IfTooLong(result.Length, _options.MaxOutputLength);
            result.CopyTo(destination.GetSpan(result.Length));
            destination.Advance(result.Length);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    private void Run(ReadOnlySpan<char> text, ref CharBuffer buffer)
    {
        // Сущности в обычном тексте не декодируются: «&nbsp;» здесь — просто шесть символов,
        // а не неразрывный пробел. Фаза Prepare нужна ради метки порядка байт, которая
        // иначе встаёт слева от первого слова и лишает его контекста «начало документа».
        var prepared = new CharBuffer(text.Length + 8);
        try
        {
            Preparer.Run(text, decodeEntities: false, _options.Rules, ref prepared, isDocumentStart: true);

            var state = new ScanState();
            TextScanner.Run(prepared.AsSpan(), _options.Rules, ref state, ref buffer);
            WordBinder.Run(ref buffer, _options.Rules);
        }
        finally
        {
            prepared.Dispose();
        }
    }
}
