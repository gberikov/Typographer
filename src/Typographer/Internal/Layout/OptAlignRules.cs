using Typographer.Rules;

namespace Typographer.Internal.Layout;

/// <summary>Висячая пунктуация: открывающий знак выносится за край набора.</summary>
/// <remarks>
/// Правила добавляет задача 5 плана 2d. Пока правило только соблюдает соглашение прохода:
/// возвращает false и отдаёт символ диспетчеру.
/// </remarks>
internal static class OptAlignRules
{
    /// <summary>Оборачивает знак, если на позиции <paramref name="index"/> он и стоит.</summary>
    /// <param name="node">Текстовый узел.</param>
    /// <param name="index">Позиция разбираемого символа.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние прохода.</param>
    /// <param name="buffer">Приёмник.</param>
    /// <returns>Символ записан правилом; диспетчеру писать нечего.</returns>
    public static bool TryApply(
        ReadOnlySpan<char> node, int index, RuleSet rules, ref InlineState state, ref CharBuffer buffer)
        => false;

    /// <summary>Тег поставлен правилом висячей пунктуации: класс начинается с его префикса.</summary>
    /// <param name="tag">Срез сегмента разметки.</param>
    public static bool IsOptAlignTag(ReadOnlySpan<char> tag) => false;
}
