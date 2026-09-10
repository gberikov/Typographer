namespace Typographer.Internal;

/// <summary>Разбор тега на имя и вид. Общий помощник фаз Bind и Layout.</summary>
internal static class Tags
{
    /// <summary>
    /// Читает имя элемента из среза разметки. Комментарии, инструкции обработки и
    /// незакрытые обрывки именем не обладают — для них возвращается <c>false</c>.
    /// </summary>
    /// <param name="tag">Срез сегмента разметки, начинающийся с угловой скобки.</param>
    /// <param name="name">Имя элемента без скобок и атрибутов.</param>
    /// <param name="closing">Тег закрывающий.</param>
    public static bool TryReadName(ReadOnlySpan<char> tag, out ReadOnlySpan<char> name, out bool closing)
    {
        name = default;
        closing = false;

        if (tag.Length < 3 || tag[0] != '<' || tag[1] is '!' or '?')
        {
            return false;
        }

        closing = tag[1] == '/';
        int start = closing ? 2 : 1;
        int end = start;
        while (end < tag.Length && !char.IsWhiteSpace(tag[end]) && tag[end] is not ('/' or '>'))
        {
            end++;
        }

        if (end == start)
        {
            return false;
        }

        name = tag.Slice(start, end - start);
        return true;
    }

    /// <summary>Тег закрыт сам собой: «&lt;br /&gt;».</summary>
    public static bool IsSelfClosing(ReadOnlySpan<char> tag)
    {
        int index = tag.Length - 2;
        while (index >= 0 && char.IsWhiteSpace(tag[index]))
        {
            index--;
        }

        return index >= 0 && tag[index] == '/';
    }

    /// <summary>Имя элемента совпадает с образцом без учёта регистра.</summary>
    public static bool NameIs(ReadOnlySpan<char> name, string sample)
        => name.Equals(sample.AsSpan(), StringComparison.OrdinalIgnoreCase);
}
