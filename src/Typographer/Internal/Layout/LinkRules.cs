using Typographer.Rules;

namespace Typographer.Internal.Layout;

/// <summary>Автоссылки: веб-адрес и электронная почта становятся элементом ссылки.</summary>
/// <remarks>
/// Правила добавляет задача 4 плана 2d. Пока правило только соблюдает соглашение прохода:
/// возвращает false и отдаёт символ диспетчеру.
/// </remarks>
internal static class LinkRules
{
    /// <summary>Пишет ссылку, если она начинается на позиции <paramref name="index"/>.</summary>
    /// <param name="node">Текстовый узел.</param>
    /// <param name="index">Позиция разбираемого символа.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние прохода.</param>
    /// <param name="buffer">Приёмник.</param>
    /// <returns>Символ записан правилом; диспетчеру писать нечего.</returns>
    public static bool TryApply(
        ReadOnlySpan<char> node, int index, RuleSet rules, ref InlineState state, ref CharBuffer buffer)
        => false;
}
