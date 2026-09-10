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

    /// <summary>Предел длины результата не может быть отрицательным.</summary>
    /// <remarks>
    /// Отрицательное значение раньше молча означало «без ограничения»: условие проверки
    /// смотрит только на положительные. Тихий сюрприз хуже отказа — тот, кто вычислил
    /// предел арифметикой и получил минус, узнаёт об этом сразу.
    /// </remarks>
    public static void IfNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Предел длины результата не может быть отрицательным.");
        }
    }

    /// <summary>
    /// Проверяет предел длины РЕЗУЛЬТАТА. Ноль — без ограничения.
    /// </summary>
    /// <remarks>
    /// Проверка стоит именно на готовом результате, а не на записи в промежуточные буферы.
    /// Фазы конвейера временно пишут больше, чем окажется в выводе: сканер пишет две точки,
    /// прежде чем свернуть их в многоточие вместе с третьей. Предел, применённый к такой
    /// записи, срабатывал на входе, который в него укладывается.
    /// </remarks>
    public static void IfTooLong(int length, int limit)
    {
        if (limit > 0 && length > limit)
        {
            throw new OutputTooLargeException(limit);
        }
    }
}
