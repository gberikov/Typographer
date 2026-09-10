using Typographer.Rules;

namespace Typographer.Internal.Layout;

/// <summary>Автоссылки: веб-адрес и электронная почта становятся элементом ссылки.</summary>
/// <remarks>
/// Оба правила вне <see cref="RuleSet.Default"/>: они делают тег из текста, а гарантия 4
/// обещает, что ни одно правило <see cref="RuleSet.Default"/> так не поступает.
/// Внутрь уже открытого элемента ссылки правила не суются — вложенная ссылка невалидна, и на
/// этом же держится их идемпотентность: собственный вывод правило второй раз не размечает.
/// </remarks>
internal static class LinkRules
{
    /// <summary>Схемы, которые правило считает началом веб-адреса.</summary>
    private const string Http = "http://";

    /// <summary>Защищённая схема; проверяется первой, потому что длиннее.</summary>
    private const string Https = "https://";

    /// <summary>Пишет ссылку, если она начинается на позиции <paramref name="index"/>.</summary>
    /// <param name="node">Текстовый узел.</param>
    /// <param name="index">Позиция разбираемого символа.</param>
    /// <param name="rules">Набор включённых правил.</param>
    /// <param name="state">Состояние прохода.</param>
    /// <param name="buffer">Приёмник.</param>
    /// <returns>Символ записан правилом; диспетчеру писать нечего.</returns>
    public static bool TryApply(
        ReadOnlySpan<char> node, int index, RuleSet rules, ref InlineState state, ref CharBuffer buffer)
    {
        if (state.InsideLink)
        {
            return false;
        }

        if (rules.Contains(RuleId.Common.Html.Url) && TryUrl(node, index, ref state, ref buffer))
        {
            return true;
        }

        return rules.Contains(RuleId.Common.Html.EMail) && TryEmail(node, index, ref state, ref buffer);
    }

    private static bool TryUrl(
        ReadOnlySpan<char> node, int index, ref InlineState state, ref CharBuffer buffer)
    {
        ReadOnlySpan<char> rest = node.Slice(index);
        int scheme = StartsWithScheme(rest, Https) ? Https.Length
            : StartsWithScheme(rest, Http) ? Http.Length
            : 0;

        if (scheme == 0)
        {
            return false;
        }

        int length = 0;
        while (length < rest.Length && !IsUrlBoundary(rest[length]))
        {
            length++;
        }

        length = TrimTrailingPunctuation(rest, length);
        if (length <= scheme)
        {
            return false;
        }

        WriteLink(rest.Slice(0, length), default, ref buffer);
        state.Skip = length - 1;
        return true;
    }

    private static bool TryEmail(
        ReadOnlySpan<char> node, int index, ref InlineState state, ref CharBuffer buffer)
    {
        // Правило спрашивается на КАЖДОМ символе, поэтому первым делом отсекается всё, что
        // адресом заведомо не начинается: середина слова и небуквенный символ. Разбирать
        // адрес по «собаке» нельзя — локальная часть к тому времени уже записана в буфер.
        if (!IsLocalChar(node[index]) || (index > 0 && IsLocalChar(node[index - 1])))
        {
            return false;
        }

        ReadOnlySpan<char> rest = node.Slice(index);
        int length = 0;
        while (length < rest.Length && (IsLocalChar(rest[length]) || rest[length] == '@'))
        {
            length++;
        }

        length = TrimTrailingPunctuation(rest, length);
        ReadOnlySpan<char> address = rest.Slice(0, length);
        if (!IsEmail(address))
        {
            return false;
        }

        WriteLink(address, "mailto:".AsSpan(), ref buffer);
        state.Skip = length - 1;
        return true;
    }

    /// <summary>Адрес: одна «собака», непустая локальная часть и домен с точкой и зоной.</summary>
    private static bool IsEmail(ReadOnlySpan<char> address)
    {
        int at = address.IndexOf('@');
        if (at <= 0 || at == address.Length - 1)
        {
            return false;
        }

        ReadOnlySpan<char> domain = address.Slice(at + 1);
        if (domain.IndexOf('@') >= 0)
        {
            return false;
        }

        int dot = domain.LastIndexOf('.');
        if (dot <= 0 || domain.Length - dot - 1 < 2)
        {
            return false;
        }

        for (int i = dot + 1; i < domain.Length; i++)
        {
            if (!char.IsLetter(domain[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Пишет элемент ссылки. Значение атрибута экранируется, текст ссылки — нет: это два
    /// разных контекста, и амперсанд в них означает разное.
    /// </summary>
    private static void WriteLink(ReadOnlySpan<char> target, ReadOnlySpan<char> scheme, ref CharBuffer buffer)
    {
        buffer.Write("<a href=\"");
        buffer.Write(scheme);
        foreach (char c in target)
        {
            switch (c)
            {
                case '&':
                    buffer.Write("&amp;");
                    break;
                case '"':
                    buffer.Write("&quot;");
                    break;
                default:
                    buffer.Write(c);
                    break;
            }
        }

        buffer.Write("\">");
        buffer.Write(target);
        buffer.Write("</a>");
    }

    /// <summary>Сравнение со схемой без учёта регистра и без аллокаций.</summary>
    private static bool StartsWithScheme(ReadOnlySpan<char> source, string scheme)
    {
        if (source.Length < scheme.Length)
        {
            return false;
        }

        for (int i = 0; i < scheme.Length; i++)
        {
            if (char.ToLowerInvariant(source[i]) != scheme[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUrlBoundary(char c) => char.IsWhiteSpace(c) || c is '<' or '>' or '"';

    private static bool IsLocalChar(char c)
        => char.IsLetterOrDigit(c) || c is '.' or '_' or '%' or '+' or '-';

    /// <summary>Хвостовые знаки препинания к адресу не относятся: «Сайт http://a.ru.».</summary>
    private static int TrimTrailingPunctuation(ReadOnlySpan<char> source, int length)
    {
        while (length > 0 && source[length - 1] is '.' or ',' or ';' or ':' or '!' or '?' or ')')
        {
            length--;
        }

        return length;
    }
}
