using System.Collections;
using Typographer.Internal;

namespace Typographer.Rules;

/// <summary>Иммутабельное множество включённых правил.</summary>
/// <remarks>Внутри — битовая маска, проверка включённости не аллоцирует.</remarks>
public sealed class RuleSet : IReadOnlyCollection<RuleId>
{
    private readonly ulong _low;
    private readonly ulong _high;

    private RuleSet(ulong low, ulong high)
    {
        _low = low;
        _high = high;
    }

    /// <summary>Пустое множество: типограф ничего не меняет.</summary>
    public static RuleSet None { get; } = new(0, 0);

    /// <summary>Все правила, включая рискованные и требующие внешнего CSS.</summary>
    public static RuleSet All { get; } = FromRules(RuleId.Registry.All);

    /// <summary>Безопасная типографика: всё, что меняет только оформление. Пресет по умолчанию.</summary>
    public static RuleSet Default { get; } = All
        .Without(RuleId.Registry.Unsafe)
        .Without(RuleId.Registry.Normalization)
        .Without(RuleId.Registry.OptIn);

    /// <summary>Кавычки, тире и многоточие — минимум.</summary>
    public static RuleSet Minimal { get; } = FromRules(
    [
        RuleId.Common.Punctuation.Quote,
        RuleId.Common.Punctuation.Hellip,
        RuleId.Ru.Dash.Main,
    ]);

    /// <summary>
    /// Поведение веб-сервиса Артемия Лебедева. Сейчас буквально совпадает с <see cref="Default"/>:
    /// правило, которое должно отличать этот пресет от <see cref="Gost"/> (диапазон дат с
    /// неразрывным пробелом, «1941 — 1945» вместо «1941—1945»), в текущей версии не реализовано.
    /// Разойдётся в следующей версии.
    /// </summary>
    public static RuleSet Lebedev { get; } = Default;

    /// <summary>
    /// Паритет дефолтов с JS-typograf. Включает <see cref="RuleId.Ru.Punctuation.Ano"/> и
    /// <see cref="RuleId.Ru.Typo.SwitchingKeyboardLayout"/> по имени, но оба правила пока не
    /// реализованы (см. их XML-комментарии) — до тех пор вывод этого пресета буквально совпадает
    /// с <see cref="Default"/>.
    /// </summary>
    public static RuleSet Typograf { get; } = Default.With(RuleId.Ru.Punctuation.Ano, RuleId.Ru.Typo.SwitchingKeyboardLayout);

    /// <summary>
    /// Строго по ГОСТ Р 7.0.110-2025. Сейчас буквально совпадает с <see cref="Default"/>:
    /// правило, которое должно отличать этот пресет от <see cref="Lebedev"/> (диапазон дат без
    /// отбивки, «1941—1945» вместо «1941 — 1945»), в текущей версии не реализовано.
    /// Разойдётся в следующей версии.
    /// </summary>
    public static RuleSet Gost { get; } = Default;

    /// <summary>Количество включённых правил.</summary>
    public int Count => BitCount(_low) + BitCount(_high);

    /// <summary>Проверяет, включено ли правило.</summary>
    public bool Contains(RuleId rule)
        => rule.Index < 64
            ? (_low & (1UL << rule.Index)) != 0
            : (_high & (1UL << (rule.Index - 64))) != 0;

    /// <summary>Возвращает новое множество с добавленными правилами.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> — null.</exception>
    public RuleSet With(params RuleId[] rules)
    {
        Throw.IfNull(rules, nameof(rules));

        ulong low = _low, high = _high;
        foreach (RuleId rule in rules)
        {
            if (rule.Index < 64)
            {
                low |= 1UL << rule.Index;
            }
            else
            {
                high |= 1UL << (rule.Index - 64);
            }
        }

        return new RuleSet(low, high);
    }

    /// <summary>Возвращает новое множество без указанных правил.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> — null.</exception>
    public RuleSet Without(params RuleId[] rules)
    {
        Throw.IfNull(rules, nameof(rules));

        ulong low = _low, high = _high;
        foreach (RuleId rule in rules)
        {
            if (rule.Index < 64)
            {
                low &= ~(1UL << rule.Index);
            }
            else
            {
                high &= ~(1UL << (rule.Index - 64));
            }
        }

        return new RuleSet(low, high);
    }

    /// <inheritdoc />
    public IEnumerator<RuleId> GetEnumerator()
    {
        foreach (RuleId rule in RuleId.Registry.All)
        {
            if (Contains(rule))
            {
                yield return rule;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static RuleSet FromRules(RuleId[] rules) => None.With(rules);

    private static int BitCount(ulong value)
    {
#if NET8_0_OR_GREATER
        return System.Numerics.BitOperations.PopCount(value);
#else
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
#endif
    }
}
