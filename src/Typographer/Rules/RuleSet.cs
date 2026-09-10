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
    /// Поведение веб-сервиса Артемия Лебедева. Отличия от <see cref="Default"/> сняты
    /// снимком сервиса (<c>docs/oracle/lebedev.md</c>): диапазон лет он оставляет с дефисом,
    /// удвоенный восклицательный знак не трогает, длинные числа разбивает по разрядам,
    /// адресное сокращение от следующего слова не отбивает («ул. Ленина», «г. Москва»)
    /// и пробел перед знаком процента не съедает, а делает неразрывным.
    /// </summary>
    public static RuleSet Lebedev { get; } = Default
        .Without(
            RuleId.Ru.Dash.Years,
            RuleId.Ru.Punctuation.Exclamation,
            RuleId.Ru.Nbsp.Addr,
            RuleId.Common.Space.DelBeforePercent)
        .With(RuleId.Common.Number.DigitGrouping);

    /// <summary>
    /// Паритет дефолтов с JS-typograf: к <see cref="Default"/> добавлены правила, которые
    /// там включены по умолчанию, а у нас признаны меняющими смысл, — расстановка запятых
    /// перед «а» и «но», исправление раскладки, дефис в омонимичных частицах и разбиение
    /// разрядов. В обратную сторону расхождение одно: неразрывный пробел между числом и
    /// следующим словом в JS-typograf по умолчанию выключен, а у нас включён по ГОСТ 9.4.
    /// </summary>
    public static RuleSet Typograf { get; } = Default
        .With(
            RuleId.Ru.Punctuation.Ano,
            RuleId.Ru.Typo.SwitchingKeyboardLayout,
            RuleId.Ru.Dash.To,
            RuleId.Ru.Dash.KakTo,
            RuleId.Common.Number.DigitGrouping)
        .Without(RuleId.Common.Nbsp.AfterNumber);

    /// <summary>
    /// Строго по ГОСТ Р 7.0.110-2025. Отличие от <see cref="Default"/> одно: пробел перед
    /// знаком процента не удаляется. ГОСТ 9.6 требует там неразрывный пробел, а правило
    /// <see cref="RuleId.Common.Space.DelBeforePercent"/> пришло из JS-typograf и пробел
    /// съедает; при конфликте источников побеждает ГОСТ.
    /// </summary>
    public static RuleSet Gost { get; } = Default.Without(RuleId.Common.Space.DelBeforePercent);

    /// <summary>Все правила фазы Bind. Нужна самой фазе: без пересечения проход не запускается.</summary>
    internal static RuleSet BindPhase { get; } = FromPhase(RulePhase.Bind);

    /// <summary>Все правила фазы Layout. Нужна самой фазе: без пересечения проход не запускается.</summary>
    internal static RuleSet LayoutPhase { get; } = FromPhase(RulePhase.Layout);

    /// <summary>Количество включённых правил.</summary>
    public int Count => BitCount(_low) + BitCount(_high);

    /// <summary>Проверяет, включено ли правило.</summary>
    public bool Contains(RuleId rule)
        => rule.Index < 64
            ? (_low & (1UL << rule.Index)) != 0
            : (_high & (1UL << (rule.Index - 64))) != 0;

    /// <summary>Проверяет, есть ли правило, включённое и здесь, и в переданном множестве.</summary>
    /// <param name="other">Множество, с которым сравнивается это.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> — null.</exception>
    public bool Overlaps(RuleSet other)
    {
        Throw.IfNull(other, nameof(other));

        return (_low & other._low) != 0 || (_high & other._high) != 0;
    }

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

    private static RuleSet FromPhase(RulePhase phase)
    {
        RuleSet set = None;
        foreach (RuleId rule in RuleId.Registry.All)
        {
            if (rule.Phase == phase)
            {
                set = set.With(rule);
            }
        }

        return set;
    }

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
