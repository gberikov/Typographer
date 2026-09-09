namespace Typographer.Internal;

/// <summary>
/// Уровни кавычек русского набора: ёлочки, затем двойные лапки, затем одинарные лапки.
/// Источник: ГОСТ Р 7.0.110-2025, 16.3; справочник кавычек type.today.
/// </summary>
internal struct QuoteStack
{
    private const int MaxDepth = 8;

    private int _depth;

    public readonly int Depth => _depth;

    public readonly bool IsEmpty => _depth == 0;

    /// <summary>Открывает следующий уровень и возвращает открывающую кавычку.</summary>
    public char Open()
    {
        char quote = _depth switch
        {
            0 => Chars.Laquo,
            1 => Chars.Bdquo,
            _ => Chars.Lsquo,
        };

        if (_depth < MaxDepth)
        {
            _depth++;
        }

        return quote;
    }

    /// <summary>Закрывает текущий уровень и возвращает закрывающую кавычку.</summary>
    public char Close()
    {
        if (_depth > 0)
        {
            _depth--;
        }

        return _depth switch
        {
            0 => Chars.Raquo,
            1 => Chars.Ldquo,
            _ => Chars.Rsquo,
        };
    }
}
