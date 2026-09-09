using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>
/// Фаза Bind: словарный проход по словам, расстановка неразрывных пробелов.
/// </summary>
/// <remarks>
/// Фаза ничего не добавляет и не удаляет — она только заменяет отдельные пробелы на
/// неразрывные, поэтому правит буфер фазы Scan НА МЕСТЕ, а не переписывает его в новый.
/// Длина при этом не меняется, и индексы уже пройденных позиций остаются валидными: на этом
/// построена привязка фамилии к идущему следом инициалу.
/// </remarks>
internal static class WordBinder
{
    public static void Run(ref CharBuffer buffer, RuleSet rules)
    {
        ReadOnlySpan<char> source = buffer.AsSpan();

        bool afterShortWord = rules.Contains(RuleId.Common.Nbsp.AfterShortWord);
        bool abbr = rules.Contains(RuleId.Ru.Nbsp.Abbr);
        bool initials = rules.Contains(RuleId.Ru.Nbsp.Initials);

        int wordStart = -1;

        // Индекс пробела перед текущим словом, -1 — если слова не разделены пробелом
        // (начало строки или после другой пунктуации). Нужен, чтобы связать фамилию с инициалом,
        // который идёт СЛЕДОМ: «Пушкин А.» — на момент обработки «Пушкин» ещё неизвестно, что
        // дальше инициал, поэтому решение принимается при разборе «А.» задним числом.
        int prevSpaceIndex = -1;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];

            if (char.IsLetter(c))
            {
                if (wordStart < 0)
                {
                    wordStart = i;
                }

                continue;
            }

            // Точка не завершает слово: она может быть частью сокращения или инициала,
            // решение принимает следующий за ней пробел.
            if (c == '.')
            {
                continue;
            }

            if (c != ' ')
            {
                // Токен закончился не пробелом, а знаком препинания: «Пушкин А., автор».
                // Вперёд связывать нечего — пробела справа нет, — но связь НАЗАД, с
                // фамилией, инициалу по-прежнему нужна.
                BindInitialToPreviousWord(ref buffer, source, wordStart, i, initials, prevSpaceIndex);
                wordStart = -1;
                prevSpaceIndex = -1;
                continue;
            }

            if (wordStart < 0)
            {
                prevSpaceIndex = -1;
                continue;
            }

            ReadOnlySpan<char> token = source.Slice(wordStart, i - wordStart);
            bool hasDot = token[token.Length - 1] == '.';
            ReadOnlySpan<char> letters = hasDot ? token.Slice(0, token.Length - 1) : token;
            bool isInitial = initials && IsInitial(token);

            bool bind =
                (afterShortWord && !hasDot && Dictionaries.IsShortWord(letters))
                || (abbr && hasDot && Dictionaries.IsAbbreviationPart(letters))
                || isInitial;

            // Инициал связывает себя не только со следующим словом, но и с предыдущим —
            // «Пушкин А.» нуждается в неразрывном пробеле по обе стороны от «А.».
            if (isInitial && prevSpaceIndex >= 0)
            {
                buffer.PatchAt(prevSpaceIndex, Chars.Nbsp);
            }

            if (bind)
            {
                buffer.PatchAt(i, Chars.Nbsp);
            }

            wordStart = -1;
            prevSpaceIndex = i;
        }

        // Конец ввода тоже завершает токен: «Пушкин А.» кончается инициалом, и без этого
        // разбор последнего слова не запускался вовсе.
        BindInitialToPreviousWord(ref buffer, source, wordStart, source.Length, initials, prevSpaceIndex);
    }

    /// <summary>
    /// Ставит неразрывный пробел ПЕРЕД инициалом, завершившимся на границе, за которой
    /// пробела нет: конец ввода или знак препинания.
    /// </summary>
    private static void BindInitialToPreviousWord(
        ref CharBuffer buffer,
        ReadOnlySpan<char> source,
        int wordStart,
        int wordEnd,
        bool initials,
        int prevSpaceIndex)
    {
        if (!initials || wordStart < 0 || prevSpaceIndex < 0)
        {
            return;
        }

        if (IsInitial(source.Slice(wordStart, wordEnd - wordStart)))
        {
            buffer.PatchAt(prevSpaceIndex, Chars.Nbsp);
        }
    }

    /// <summary>Инициал — одна прописная буква с точкой: «А.».</summary>
    private static bool IsInitial(ReadOnlySpan<char> token)
        => token.Length == 2 && token[1] == '.' && char.IsUpper(token[0]);
}
