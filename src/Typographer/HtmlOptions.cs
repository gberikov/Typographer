using Typographer.Rules;

namespace Typographer;

/// <summary>Настройки типографирования HTML-фрагмента.</summary>
public sealed record HtmlOptions
{
    /// <summary>Набор включённых правил. По умолчанию — безопасная типографика.</summary>
    public RuleSet Rules { get; init; } = RuleSet.Default;

    /// <summary>Как выводить типографские символы.</summary>
    public EntityMode Entities { get; init; } = EntityMode.Symbols;

    /// <summary>Заменять перевод строки тегом переноса.</summary>
    public bool UseBr { get; init; }

    /// <summary>Оборачивать абзацы в теги абзаца.</summary>
    public bool UseP { get; init; }

    /// <summary>Максимальное число слов, объединяемых в неразрывный блок. Ноль — не объединять.</summary>
    public int MaxNobr { get; init; }

    /// <summary>Предел длины результата в символах. Ноль — без ограничения.</summary>
    public int MaxOutputLength { get; init; }

    /// <summary>Настройки по умолчанию.</summary>
    public static HtmlOptions Default { get; } = new();
}
