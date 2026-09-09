using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>Правила пробелов фазы Scan.</summary>
internal static class SpaceRules
{
    // floor этим правилам не нужен — они не патчят буфер задним числом, параметр в сигнатуре ради единообразия.
    public static bool TryApply(
        ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        char c = source[index];

        if (rules.Contains(RuleId.Common.Space.DelRepeatSpace)
            && index + 1 < source.Length && source[index + 1] == ' ')
        {
            return true;
        }

        // previous != '<' — единственный барьер во всём конвейере против «текст становится
        // разметкой» (гарантия 4). Без него «< ?» и «< !--» превращались бы в «<?» и «<!--»:
        // удалённый здесь пробел — единственный, что мешало «<» слипнуться со знаком
        // препинания. После перехода фаз на документные проходы (Bind, Layout, Emit)
        // пересканируют уже этот буфер через MarkupScanner и примут псевдотег за настоящий.
        if (rules.Contains(RuleId.Common.Space.DelBeforePunctuation)
            && index + 1 < source.Length && IsPunctuation(source[index + 1])
            && previous != '<')
        {
            return true;
        }

        buffer.Write(c);
        return true;
    }

    /// <summary>
    /// Дописывает пробел после запятой, если он там нужен. Вызывается диспетчером ПОСЛЕ
    /// того, как обычный символ уже записан в буфер — это не самостоятельное правило со
    /// своим символом-триггером, а довесок к записи запятой.
    /// </summary>
    public static void WriteSpaceAfterComma(
        ReadOnlySpan<char> source, int index, char previous, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        if (rules.Contains(RuleId.Common.Space.AfterComma) && source[index] == ','
            && NeedsSpaceAfterComma(source, index, previous, !state.Quotes.IsEmpty))
        {
            buffer.Write(' ');
        }
    }

    // Знак препинания — класс, а не только сырая форма во входном тексте: многоточие входит
    // сюда как готовый символ (Chars.Hellip), потому что правило hellip схлопывает "..." в
    // него ДО того, как другие правила решают, что рядом со знаком препинания.
    private static bool IsPunctuation(char c) => c is ',' or '.' or ';' or ':' or '!' or '?' or Chars.Hellip;

    /// <summary>Символы, слева от которых пробел не ставится: закрывающие скобки и кавычки.</summary>
    private static bool IsClosing(char c)
        => c is ')' or ']' or '}' or Chars.Raquo or Chars.Ldquo or Chars.Rsquo;

    private static bool NeedsSpaceAfterComma(
        ReadOnlySpan<char> source, int index, char previous, bool insideQuotes)
    {
        if (index + 1 >= source.Length)
        {
            return false;
        }

        char next = source[index + 1];

        // Пробел уже есть. Проверяется КЛАСС символа, а не один только обычный пробел:
        // неразрывный пробел мог быть поставлен другим правилом на прошлом прогоне, и без
        // этого правило дописывало бы рядом ещё один — нарушая идемпотентность.
        if (char.IsWhiteSpace(next))
        {
            return false;
        }

        // Между двумя соседними знаками препинания пробела не бывает: «текст,,ещё» — за
        // первой запятой сразу вторая, вставлять пробел некуда.
        if (IsPunctuation(next))
        {
            return false;
        }

        // Слева от закрывающей скобки или кавычки пробела тоже не бывает. Простая кавычка
        // закрывающая ровно тогда, когда открыт хотя бы один уровень: вставленный перед ней
        // пробел сделал бы её открывающей, и уровни кавычек разъезжались бы до конца
        // документа — «Да,» превращалось в «Да, „».
        if (IsClosing(next) || (next is '"' or '\'' && insideQuotes))
        {
            return false;
        }

        // Запятая внутри числа — десятичный разделитель, пробел не нужен.
        return !(char.IsDigit(previous) && char.IsDigit(next));
    }
}
