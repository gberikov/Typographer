using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>
/// Фаза Bind: словарный проход по словам, расстановка неразрывных пробелов.
/// Работает по уже отсканированному тексту и патчит пробелы на месте.
/// </summary>
internal static class WordBinder
{
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)
    {
        bool afterShortWord = rules.Contains(RuleId.Common.Nbsp.AfterShortWord);
        bool abbr = rules.Contains(RuleId.Ru.Nbsp.Abbr);
        bool initials = rules.Contains(RuleId.Ru.Nbsp.Initials);

        int wordStart = -1;

        // Индекс пробела перед текущим словом (в buffer), -1 — если слова не разделены пробелом
        // (начало строки или после другой пунктуации). Нужен, чтобы связать фамилию с инициалом,
        // который идёт СЛЕДОМ: «Пушкин А.» — на момент обработки «Пушкин» ещё неизвестно, что
        // дальше инициал, поэтому решение принимается при разборе «А.» задним числом.
        int prevSpaceIndex = -1;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            buffer.Write(c);

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
            bool isInitial = initials && hasDot && letters.Length == 1 && char.IsUpper(letters[0]);

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

            int currentSpaceIndex = buffer.Length - 1;
            if (bind)
            {
                buffer.PatchAt(currentSpaceIndex, Chars.Nbsp);
            }

            wordStart = -1;
            prevSpaceIndex = currentSpaceIndex;
        }
    }
}
