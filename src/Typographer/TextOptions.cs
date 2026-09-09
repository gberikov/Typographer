using Typographer.Rules;

namespace Typographer;

/// <summary>Настройки типографирования обычного текста.</summary>
public sealed record TextOptions
{
    /// <summary>Набор включённых правил. По умолчанию — безопасная типографика.</summary>
    public RuleSet Rules { get; init; } = RuleSet.Default;

    /// <summary>Предел длины результата в символах. Ноль — без ограничения.</summary>
    public int MaxOutputLength { get; init; }

    /// <summary>Настройки по умолчанию.</summary>
    public static TextOptions Default { get; } = new();
}
