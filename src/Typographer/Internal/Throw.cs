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
