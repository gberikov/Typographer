using Typographer.Rules;

namespace Typographer.Internal.Layout;

/// <summary>Висячая пунктуация: открывающий знак выносится за край набора.</summary>
/// <remarks>
/// Правило само по себе ничего не выравнивает — оно только размечает знак, а выносит его
/// CSS на стороне сайта. Поэтому имена классов взяты у JS-typograf дословно: чужие стили
/// обязаны подходить к нашему выводу без переписывания.
/// Все три правила вне <see cref="RuleSet.Default"/>: они делают тег из текста (гарантия 4),
/// и без стилей вывод ничем не отличается от обычного, зато содержит лишнюю разметку.
/// Закрывающая кавычка и закрывающая скобка не размечаются: висит ЛЕВЫЙ край строки.
/// Идемпотентность держится на признаке <see cref="InlineState.InsideOptAlign"/>: символ,
/// обёрнутый на прошлом прогоне, второй раз не оборачивается.
/// </remarks>
internal static class OptAlignRules
{
    /// <summary>Префикс классов правила. По нему же распознаётся собственный вывод.</summary>
    private const string ClassPrefix = "typograf-oa-";

    /// <summary>Оборачивает знак, если на позиции <paramref name="index"/> он и стоит.</summary>
    /// <param name="node">Текстовый узел.</param>
    /// <param name="index">Позиция разбираемого символа.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние прохода.</param>
    /// <param name="buffer">Приёмник.</param>
    /// <returns>Символ записан правилом; диспетчеру писать нечего.</returns>
    public static bool TryApply(
        ReadOnlySpan<char> node, int index, RuleSet rules, ref InlineState state, ref CharBuffer buffer)
    {
        if (state.InsideOptAlign)
        {
            return false;
        }

        char c = node[index];

        if (c is Chars.Laquo or Chars.Bdquo && rules.Contains(RuleId.Ru.OptAlign.Quote))
        {
            return TryHang(c, "lquote", ref state, ref buffer);
        }

        if (c == '(' && rules.Contains(RuleId.Ru.OptAlign.Bracket))
        {
            return TryHang(c, "lbracket", ref state, ref buffer);
        }

        if (c == ',' && rules.Contains(RuleId.Ru.OptAlign.Comma))
        {
            return TryComma(node, index, ref state, ref buffer);
        }

        return false;
    }

    /// <summary>Тег поставлен правилом висячей пунктуации: в нём есть класс с нашим префиксом.</summary>
    /// <param name="tag">Срез сегмента разметки.</param>
    public static bool IsOptAlignTag(ReadOnlySpan<char> tag)
        => tag.IndexOf(ClassPrefix.AsSpan(), StringComparison.Ordinal) >= 0;

    /// <summary>
    /// Открывающий знак у левого края. В начале строки он размечается один; в середине —
    /// вместе с пробелом слева, который забирается из буфера и переписывается своим тегом.
    /// </summary>
    private static bool TryHang(char sign, string kind, ref InlineState state, ref CharBuffer buffer)
    {
        if (state.AtLineStart)
        {
            WriteWrapped("n-", kind, sign, ref buffer);
            return true;
        }

        // Пробел слева уже записан. Забрать его можно, только если записал его этот же
        // проход: левее SafeFrom лежит скопированная разметка, и усечение съело бы её байты.
        if (buffer.Length <= state.SafeFrom)
        {
            return false;
        }

        char space = buffer.CharAt(buffer.Length - 1);
        if (space is not (' ' or Chars.Nbsp))
        {
            return false;
        }

        buffer.Truncate(buffer.Length - 1);
        WriteWrapped("sp-", kind, space, ref buffer);
        WriteWrapped(string.Empty, kind, sign, ref buffer);
        return true;
    }

    /// <summary>
    /// Запятая у правого края: размечается сама и пробел за ней. Слева от неё обязана быть
    /// буква или цифра, справа — пробел: «раз,два» вешать не за что.
    /// </summary>
    private static bool TryComma(
        ReadOnlySpan<char> node, int index, ref InlineState state, ref CharBuffer buffer)
    {
        if (index + 1 >= node.Length || node[index + 1] is not (' ' or Chars.Nbsp))
        {
            return false;
        }

        if (buffer.Length <= state.SafeFrom || !char.IsLetterOrDigit(buffer.CharAt(buffer.Length - 1)))
        {
            return false;
        }

        WriteWrapped(string.Empty, "comma", ',', ref buffer);
        WriteWrapped(string.Empty, "comma-sp", node[index + 1], ref buffer);
        state.Skip = 1;
        return true;
    }

    /// <summary>Пишет символ, обёрнутый в тег с классом правила.</summary>
    private static void WriteWrapped(string prefix, string kind, char value, ref CharBuffer buffer)
    {
        buffer.Write("<span class=\"");
        buffer.Write(ClassPrefix.AsSpan());
        buffer.Write(prefix.AsSpan());
        buffer.Write(kind.AsSpan());
        buffer.Write("\">");
        buffer.Write(value);
        buffer.Write("</span>");
    }
}
