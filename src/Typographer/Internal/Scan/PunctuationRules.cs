using Typographer.Rules;

namespace Typographer.Internal.Scan;

/// <summary>
/// Правила знаков препинания фазы Scan.
/// </summary>
/// <remarks>
/// Сигнатура <see cref="TryApply"/> — образец для всех правил фазы Scan (в том числе для
/// правил планов 2b и 2c): <c>true</c> означает «символ обработан и записан в буфер»,
/// <c>false</c> — «правило не применилось, символ пишет диспетчер». Параметр
/// <c>floor</c> — позиция в буфере, с которой начинаются записи ТЕКУЩЕГО текстового узла:
/// буфер общий на весь документ, и левее floor лежат чужие символы, в том числе разметка.
/// Правило не имеет права патчить или усекать буфер левее floor — иначе оно переписало бы
/// символ внутри тега и сломало бы гарантию «разметка байт в байт».
/// Левый и правый контекст читаются НЕСИММЕТРИЧНО. Левый — через <c>previous</c> и
/// <c>state.Last</c> — живёт сквозь весь документ и пересекает границу тега: «а &lt;b&gt;б»
/// видит «а» контекстом для «б». Правый читается из <c>source</c> — текущего текстового
/// узла — и обрывается на границе тега: правило, которому нужен правый контекст за тегом,
/// его не получит.
/// Из этой асимметрии следует, что правило может сработать НАПОЛОВИНУ: вход
/// «текст &lt;b&gt;- слово&lt;/b&gt;» — тире ставится, потому что левый контекст дотянулся
/// через тег, но отбивка пробела слева не происходит, потому что пробел лежит левее floor
/// предыдущего узла и патчить его нельзя. Это не ошибка, а следствие двух правил выше —
/// автор нового правила должен знать, что такой исход возможен.
/// </remarks>
internal static class PunctuationRules
{
    public static bool TryApply(
        ReadOnlySpan<char> source, int index, char previous, int floor, RuleSet rules,
        ref ScanState state, ref CharBuffer buffer)
    {
        char c = source[index];

        // Запятая перед «а» и «но». Правило само расставляет знаки, то есть меняет текст,
        // а не оформление, — поэтому вне Default.
        if (c == ' ' && rules.Contains(RuleId.Ru.Punctuation.Ano))
        {
            return TryCommaBeforeConjunction(source, index, previous, ref buffer);
        }

        // Удвоенный восклицательный знак сводится к одному.
        if (c == '!' && previous == '!' && buffer.Length > floor
            && rules.Contains(RuleId.Ru.Punctuation.Exclamation))
        {
            return true;
        }

        // «!?» пишется как «?!»: знак вопроса первым. Восклицательный уже в буфере — он
        // патчится на месте, длина не меняется.
        if (c == '?' && previous == '!' && buffer.Length > floor
            && rules.Contains(RuleId.Ru.Punctuation.ExclamationQuestion))
        {
            buffer.PatchAt(buffer.Length - 1, '?');
            buffer.Write('!');
            return true;
        }

        // Многоточие после знака конца предложения короче на точку: «?..», а не «?…».
        if (c == Chars.Hellip && previous is '?' or '!'
            && rules.Contains(RuleId.Ru.Punctuation.HellipQuestion))
        {
            buffer.Write('.');
            buffer.Write('.');
            return true;
        }

        // Запятая после многоточия не нужна: многоточие само по себе разделяет.
        if (c == ',' && previous == Chars.Hellip && buffer.Length > floor
            && rules.Contains(RuleId.Ru.Punctuation.HellipQuestion))
        {
            return true;
        }

        // Знак препинания, повторённый подряд, — опечатка. Точка сюда НЕ входит: три точки
        // законно собираются в многоточие правилом ниже, и схлопывание пары убило бы его.
        // Восклицательный и вопросительный тоже не входят: удвоение там осмысленно в
        // разговорной речи, и решение о нём принимает правило русского языка, а не общее.
        if (c is ',' or ';' or ':'
            && previous == c
            && buffer.Length > floor
            && rules.Contains(RuleId.Common.Punctuation.DelDoublePunctuation))
        {
            // Решение о пробеле после знака принимается ЗДЕСЬ, а не при записи первого из
            // пары. В тот момент справа стоял второй такой же знак, и правило пробела
            // отказывалось: между двумя знаками препинания пробела не бывает. Съев дубль,
            // мы открыли за знаком букву — и если не спросить правило пробела сейчас, оно
            // сработает только на ВТОРОМ прогоне, а это разрыв идемпотентности (гарантия 5).
            SpaceRules.WriteSpaceAfterPunctuation(
                source, index, previous, floor, rules, ref state, ref buffer);
            return true;
        }

        // Многоточие схлопывается ЗАДНИМ ЧИСЛОМ: третья подряд точка в буфере забирает
        // две предыдущие. По исходной строке это не решается — точки становятся соседями
        // уже в буфере, после того как удалён пробел между ними («текст. ..»).
        if (rules.Contains(RuleId.Common.Punctuation.Hellip) && c == '.'
            && (index + 1 >= source.Length || source[index + 1] != '.')
            && ClosesEllipsis(ref buffer, floor, state.Last))
        {
            buffer.Truncate(buffer.Length - 2);

            // После знака конца предложения многоточие пишется двумя точками: «?..».
            char beforeDots = buffer.Length > floor ? buffer.CharAt(buffer.Length - 1) : state.Last;
            if (beforeDots is '?' or '!' && rules.Contains(RuleId.Ru.Punctuation.HellipQuestion))
            {
                buffer.Write('.');
                buffer.Write('.');
                return true;
            }

            buffer.Write(Chars.Hellip);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Буфер оканчивается ровно двумя точками, то есть текущая точка — третья и последняя.
    /// Третий с конца символ проверяется по КЛАССУ: готовое многоточие — те же точки,
    /// иначе «……» получалось бы из шести точек за два прогона.
    /// </summary>
    /// <param name="buffer">Буфер документа.</param>
    /// <param name="floor">Позиция в буфере, с которой начинается текущий сегмент.</param>
    /// <param name="beforeBuffer">Символ слева от сегмента — последний символ прошлого сегмента.</param>
    private static bool ClosesEllipsis(ref CharBuffer buffer, int floor, char beforeBuffer)
    {
        int written = buffer.Length - floor;
        if (written < 2 || buffer.CharAt(buffer.Length - 1) != '.' || buffer.CharAt(buffer.Length - 2) != '.')
        {
            return false;
        }

        char third = written >= 3 ? buffer.CharAt(buffer.Length - 3) : beforeBuffer;
        return third is not ('.' or Chars.Hellip);
    }

    /// <summary>
    /// Запятая перед союзом «а» или «но», если её там нет. Признак — буква слева и союз
    /// целым словом справа.
    /// </summary>
    private static bool TryCommaBeforeConjunction(
        ReadOnlySpan<char> source, int index, char previous, ref CharBuffer buffer)
    {
        if (!char.IsLetter(previous))
        {
            return false;
        }

        int start = index + 1;
        int length = 0;
        while (start + length < source.Length && char.IsLetter(source[start + length]))
        {
            length++;
        }

        bool conjunction = length switch
        {
            1 => char.ToLowerInvariant(source[start]) == 'а',
            2 => char.ToLowerInvariant(source[start]) == 'н' && char.ToLowerInvariant(source[start + 1]) == 'о',
            _ => false,
        };

        if (!conjunction)
        {
            return false;
        }

        buffer.Write(',');
        buffer.Write(' ');
        return true;
    }
}
