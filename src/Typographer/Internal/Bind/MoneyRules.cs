using Typographer.Rules;

namespace Typographer.Internal.Bind;

/// <summary>Правила денежной записи фазы Bind. Оба вне <see cref="RuleSet.Default"/>.</summary>
/// <remarks>
/// Символ валюты перед числом ПЕРЕНОСИТСЯ за число — это перемещение текста, а не замена
/// символа, поэтому правило символьное: оно читает число из документа вперёд и пишет его само
/// (соглашение см. <see cref="MarkRules"/>). Знак рубля, наоборот, обычная перезапись токена.
/// </remarks>
internal static class MoneyRules
{
    /// <summary>Символы валют, которые правило переносит за число.</summary>
    private static bool IsCurrency(char c) => c is '$' or '€' or '¥' or '£' or '₤' or 'Ұ';

    /// <summary>Переносит символ валюты за число, если на позиции стоит он.</summary>
    /// <param name="document">Документ после фазы Scan.</param>
    /// <param name="index">Позиция разбираемого символа.</param>
    /// <param name="end">Граница текущего текстового сегмента.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние фазы.</param>
    /// <param name="buffer">Буфер вывода.</param>
    /// <returns>
    /// Символ записан правилом; диспетчеру писать нечего. Когда символ УЖЕ стоит за числом,
    /// правило только приклеивает его к числу неразрывным пробелом и возвращает <c>false</c>:
    /// сам символ в этом случае пишет диспетчер.
    /// </returns>
    public static bool TryApply(
        ReadOnlySpan<char> document, int index, int end,
        RuleSet rules, ref BindState state, ref CharBuffer buffer)
    {
        char currency = document[index];
        if (!rules.Contains(RuleId.Ru.Money.Currency) || !IsCurrency(currency))
        {
            return false;
        }

        int length = ReadNumber(document, index + 1, end);
        if (length == 0)
        {
            // Порядок уже правильный: «100 $». Остаётся сделать пробел неразрывным, чтобы
            // сумма не разрывалась переносом, — и это же держит идемпотентность правила.
            if (state.SpaceIndex >= 0 && state.PrevKind == TokenKind.Number && state.PrevLength > 0)
            {
                buffer.PatchAt(state.SpaceIndex, Chars.Nbsp);
            }

            return false;
        }

        buffer.Write(document.Slice(index + 1, length));
        buffer.Write(Chars.Nbsp);
        buffer.Write(currency);
        state.Skip = length;
        return true;
    }

    /// <summary>
    /// Длина числа, стоящего сразу за символом валюты. Дробная часть отделяется запятой или
    /// точкой и учитывается: «£5,50» — одна сумма, а не число и обрывок.
    /// </summary>
    private static int ReadNumber(ReadOnlySpan<char> document, int start, int end)
    {
        int length = 0;
        while (start + length < end && char.IsDigit(document[start + length]))
        {
            length++;
        }

        if (length == 0)
        {
            return 0;
        }

        int separator = start + length;
        if (separator + 1 < end && document[separator] is ',' or '.' && char.IsDigit(document[separator + 1]))
        {
            length++;
            while (start + length < end && char.IsDigit(document[start + length]))
            {
                length++;
            }
        }

        return length;
    }

    /// <summary>Заменяет «руб.» знаком рубля, если этого требует правило.</summary>
    /// <param name="token">Текущий токен; правило обязано обновить его вместе с буфером.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние фазы.</param>
    /// <param name="buffer">Буфер вывода.</param>
    public static void TryRewrite(
        Span<char> token, RuleSet rules, ref BindState state, ref CharBuffer buffer)
    {
        if (state.TokenOverflow || !rules.Contains(RuleId.Ru.Money.Ruble))
        {
            return;
        }

        // Сумма обязана стоять слева: «руб. за штуку» — начало предложения, а не сумма.
        if (state.PrevLength == 0 || state.PrevKind != TokenKind.Number)
        {
            return;
        }

        ReadOnlySpan<char> current = token.Slice(0, state.TokenLength);
        if (!Same(current, "руб.") && !Same(current, "руб"))
        {
            return;
        }

        if (RewriteRules.Replace(Ruble, token, state.TokenStart, ref state, ref buffer)
            && state.SpaceIndex >= 0)
        {
            buffer.PatchAt(state.SpaceIndex, Chars.Nbsp);
        }
    }

    /// <summary>Знак рубля строкой: замена токена принимает спан, а не символ.</summary>
    private const string Ruble = "₽";

    private static bool Same(ReadOnlySpan<char> token, string sample)
    {
        if (token.Length != sample.Length)
        {
            return false;
        }

        for (int i = 0; i < sample.Length; i++)
        {
            if (char.ToLowerInvariant(token[i]) != sample[i])
            {
                return false;
            }
        }

        return true;
    }
}
