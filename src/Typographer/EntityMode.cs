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
