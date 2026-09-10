using Typographer.Rules;

namespace Typographer.Internal.Bind;

/// <summary>Правила фазы Bind, которые заменяют записанный токен другим.</summary>
/// <remarks>
/// Соглашение о правиле-перезаписи: правило усекает буфер до <see cref="BindState.TokenStart"/>
/// (или до другой позиции, которую проход записал сам) и пишет замену, а ТАКЖЕ приводит в
/// соответствие сам <c>token</c> и <see cref="BindState.TokenLength"/> — правила-склейки
/// спрашиваются после и обязаны видеть новый токен.
/// Переписывать разрешено только правее <see cref="BindState.SafeFrom"/>: левее лежит уже
/// скопированная разметка, и усечение съело бы её байты вопреки гарантии 3.
/// Правил здесь пока нет — их добавляют задачи 10, 12, 13 и 14 плана 2c; вызов из
/// диспетчера уже стоит в нужном месте и в нужном порядке.
/// </remarks>
internal static class RewriteRules
{
    /// <summary>Заменяет записанный токен, если какое-нибудь правило этого требует.</summary>
    /// <param name="token">Текущий токен; правило обязано обновить его вместе с буфером.</param>
    /// <param name="previous">Предыдущий токен.</param>
    /// <param name="boundary">Символ, закрывший токен; ноль — конец сегмента или документа.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние фазы.</param>
    /// <param name="buffer">Буфер вывода.</param>
    public static void TryRewrite(
        Span<char> token, ReadOnlySpan<char> previous, char boundary,
        RuleSet rules, ref BindState state, ref CharBuffer buffer)
    {
    }
}
