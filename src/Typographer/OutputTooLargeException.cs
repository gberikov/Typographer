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
