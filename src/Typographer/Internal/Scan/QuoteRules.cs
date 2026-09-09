using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>Правила кавычек и апострофа фазы Scan.</summary>
internal static class QuoteRules
{
    // floor этим правилам не нужен — они не патчят буфер задним числом, параметр в сигнатуре ради единообразия.
    public static bool TryApply(
        ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        char c = source[index];

        if (rules.Contains(RuleId.Common.Punctuation.Quote) && c == '"')
        {
            // Штрих после цифры прямой кавычкой и остаётся: 5" — пять дюймов.
            bool inch = char.IsDigit(previous) && state.Quotes.IsEmpty;
            if (inch)
            {
                buffer.Write(c);
                return true;
            }

            bool opening = IsOpeningContext(previous)
                || (state.Quotes.IsEmpty && previous == ':');
            buffer.Write(opening ? state.Quotes.Open() : state.Quotes.Close());
            return true;
        }

        // Готовая типографская кавычка обязана двигать тот же счётчик уровней. Без
        // этого простая кавычка внутри неё открывает уровень 0 повторно.
        if (rules.Contains(RuleId.Common.Punctuation.Quote) && IsReadyOpeningQuote(c))
        {
            state.Quotes.Open();
            buffer.Write(c);
            return true;
        }

        if (rules.Contains(RuleId.Common.Punctuation.Quote) && IsReadyClosingQuote(c))
        {
            // Правая одинарная кавычка одновременно служит апострофом. Между буквами
            // она не закрывает уровень: «д’Артаньян и "цитата"» должен сохранить
            // внешний уровень для вложенных двойных кавычек.
            bool apostropheBetweenLetters = c == Chars.Rsquo
                && char.IsLetter(previous)
                && index + 1 < source.Length
                && char.IsLetter(source[index + 1]);
            if (!apostropheBetweenLetters)
            {
                state.Quotes.Close();
            }

            buffer.Write(c);
            return true;
        }

        if (rules.Contains(RuleId.Common.Punctuation.Apostrophe) && c == '\'')
        {
            bool letterAfter = index + 1 < source.Length && char.IsLetter(source[index + 1]);
            buffer.Write(char.IsLetter(previous) && letterAfter ? Chars.Rsquo : c);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Кавычка открывающая, если слева начало документа, пробел любого вида (обычный,
    /// неразрывный, табуляция, перевод строки, возврат каретки) или открывающая скобка
    /// либо открывающая кавычка внешнего уровня.
    /// </summary>
    private static bool IsOpeningContext(char previous)
        => previous is '\0' or '(' or '[' or '{' or Chars.Laquo or Chars.Bdquo or Chars.Lsquo
           || char.IsWhiteSpace(previous);

    private static bool IsReadyOpeningQuote(char c)
        => c is Chars.Laquo or Chars.Bdquo or Chars.Lsquo;

    private static bool IsReadyClosingQuote(char c)
        => c is Chars.Raquo or Chars.Ldquo or Chars.Rsquo;
}
