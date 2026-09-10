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

        // Границы адреса те же, что у фазы Scan (см. Url); внутри схемы границ нет.
        int length = scheme;
        while (length < rest.Length && !Url.Ends(rest, length, rest[length - 1]))
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
    /// <remarks>
    /// Экранируется только амперсанд, который сущности НЕ начинает. Иначе уже записанная
    /// автором «&amp;amp;» превращалась бы в «&amp;amp;amp;», и параметр «b» после
    /// разбора браузером назывался бы «amp;b» — адрес в href переставал бы совпадать с
    /// адресом в тексте ссылки.
    /// </remarks>
    private static void WriteLink(ReadOnlySpan<char> target, ReadOnlySpan<char> scheme, ref CharBuffer buffer)
    {
        buffer.Write("<a href=\"");
        buffer.Write(scheme);
        for (int i = 0; i < target.Length; i++)
        {
            char c = target[i];
            switch (c)
            {
                case '&' when !StartsEntity(target, i):
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

    /// <summary>
    /// Амперсанд на позиции <paramref name="index"/> начинает ссылку на символ:
    /// «&amp;amp;», «&amp;#34;», «&amp;#x22;». Амперсанд без имени или без точки с запятой
    /// сущности не начинает и подлежит экранированию.
    /// </summary>
    /// <remarks>
    /// Взгляд вперёд гарантии 1 не нарушает, хотя и зовётся на каждом амперсанде: перебор
    /// идёт по буквам, цифрам и решётке, а следующий амперсанд перебор останавливает.
    /// Отрезки, просмотренные разными вызовами, поэтому не накладываются друг на друга, и
    /// суммарная работа линейна от длины адреса.
    /// </remarks>
    private static bool StartsEntity(ReadOnlySpan<char> source, int index)
    {
        int end = index + 1;
        while (end < source.Length && IsEntityChar(source[end]))
        {
            end++;
        }

        return end < source.Length && source[end] == ';' && IsEntity(source.Slice(index, end - index + 1));
    }

    /// <summary>
    /// Точка с запятой на позиции <paramref name="index"/> закрывает ссылку на символ.
    /// Признак тот же, что у <see cref="StartsEntity"/>: иначе «&amp;#;» одна проверка
    /// считала сущностью, а другая — нет, и точка с запятой оставалась в адресе при
    /// экранированном амперсанде.
    /// </summary>
    private static bool ClosesEntity(ReadOnlySpan<char> source, int index)
    {
        int start = index - 1;
        while (start >= 0 && IsEntityChar(source[start]))
        {
            start--;
        }

        return start >= 0 && source[start] == '&' && IsEntity(source.Slice(start, index - start + 1));
    }

    /// <summary>
    /// Запись от амперсанда до точки с запятой — ссылка на символ: имя («amp») или код
    /// («#34», «#x22») хотя бы из одного знака. «&amp;;», «&amp;#;» и «&amp;#x;» сущностями
    /// не являются.
    /// </summary>
    private static bool IsEntity(ReadOnlySpan<char> entity)
    {
        int last = entity.Length - 1;
        int i = 1;
        if (i < last && entity[i] == '#')
        {
            i++;
            if (i < last && entity[i] is 'x' or 'X')
            {
                i++;
            }
        }

        if (i >= last)
        {
            return false;
        }

        for (; i < last; i++)
        {
            if (!char.IsLetterOrDigit(entity[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsEntityChar(char c) => char.IsLetterOrDigit(c) || c == '#';

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

    private static bool IsLocalChar(char c)
        => char.IsLetterOrDigit(c) || c is '.' or '_' or '%' or '+' or '-';

    /// <summary>
    /// Хвостовые знаки препинания к адресу не относятся: «Сайт http://a.ru.», «'http://a.ru'».
    /// Прямой апостроф внутри адреса законен (RFC 3986) и границей адреса не является —
    /// «?q='x y'» рвать нельзя, — поэтому отрезается только с хвоста.
    /// </summary>
    private static int TrimTrailingPunctuation(ReadOnlySpan<char> source, int length)
    {
        int apostrophes = 0;
        foreach (char c in source.Slice(0, length))
        {
            apostrophes += c == '\'' ? 1 : 0;
        }

        while (length > 0 && source[length - 1] is '.' or ',' or ';' or ':' or '!' or '?' or ')' or '\'')
        {
            // Точка с запятой, закрывающая сущность, знаком препинания не является: она
            // часть адреса. Без этого «?q=&quot;x&quot;» теряло хвост записи, и адрес в
            // ссылке обрывался внутри сущности.
            if (source[length - 1] == ';' && ClosesEntity(source, length - 1))
            {
                break;
            }

            // Нечётное число апострофов слева означает, что последний закрывает строку
            // внутри самого адреса: ?q='hello', $filter=Name%20eq%20'Alice'. Такой апостроф
            // законен в URI и удалять его нельзя. При чётном числе это внешняя кавычка:
            // 'http://a.ru' или 'http://a.ru/?q='x''.
            if (source[length - 1] == '\'' && --apostrophes % 2 != 0)
            {
                break;
            }

            length--;
        }

        return length;
    }

}
