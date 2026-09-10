using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Typographer.Internal;

namespace Typographer.Markdig;

/// <summary>Типографика текста одного блока с сохранением разметки внутри него.</summary>
/// <remarks>
/// Директивы using стоят ДО объявления пространства имён намеренно: внутри
/// <c>Typographer.Markdig</c> имя <c>Markdig</c> разрешается в это же пространство,
/// и обратиться к типам библиотеки через <c>Markdig.Syntax.X</c> было бы нельзя.
/// </remarks>
internal static class BlockTypographer
{
    /// <summary>Разделитель, которым в собранной строке представлена вырезанная разметка.</summary>
    /// <remarks>
    /// U+FFFC — OBJECT REPLACEMENT CHARACTER, «здесь был объект». Он не пробельный,
    /// не буква и не скобка, поэтому для правила кавычек это обычное содержимое —
    /// ровно то, чем и была вырезанная разметка.
    /// </remarks>
    private const char Object = '\uFFFC';

    private static readonly string Separator = Object.ToString();

    /// <summary>Типографирует текстовые куски блока целиком, как один связный текст.</summary>
    /// <param name="root">Корень строчных элементов блока.</param>
    /// <param name="typographer">Типограф обычного текста.</param>
    public static void Apply(ContainerInline root, TextTypographer typographer)
    {
        List<LiteralInline?> literals = [];
        List<bool> editable = [];
        CollectLiterals(root, literals, editable, []);

        if (!editable.Contains(true))
        {
            return;
        }

        var parts = new string[literals.Count];
        for (int i = 0; i < literals.Count; i++)
        {
            parts[i] = editable[i] ? literals[i]!.Content.ToString() : string.Empty;

            // Чужой U+FFFC во входе сделал бы раскладку обратно неоднозначной.
            // Такой блок остаётся нетронутым: испортить текст хуже, чем не улучшить.
            if (parts[i].IndexOf(Object) >= 0)
            {
                return;
            }
        }

        string[] result = typographer.Process(string.Join(Separator, parts)).Split(Object);

        // Правило могло съесть разделитель вместе с соседним пробелом или размножить его.
        // Раскладывать нечего — блок остаётся как был.
        if (result.Length != literals.Count)
        {
            return;
        }

        for (int i = 0; i < literals.Count; i++)
        {
            if (editable[i] && !string.Equals(result[i], parts[i], StringComparison.Ordinal))
            {
                literals[i]!.Content = new StringSlice(result[i]);
            }
        }
    }

    /// <summary>
    /// Собирает текстовые узлы в порядке вывода. Защищённые узлы сохраняются как пустые
    /// части между разделителями: их текст типограф не видит, но сам элемент остаётся
    /// контекстом и не позволяет обрезать пробел у своего края.
    /// </summary>
    private static void CollectLiterals(
        ContainerInline container, List<LiteralInline?> literals, List<bool> editable,
        List<string> protectedTags)
    {
        for (Inline? inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            if (inline is HtmlInline html)
            {
                bool wasProtected = protectedTags.Count > 0;
                UpdateProtectedZone(html.Tag, protectedTags);
                if (!wasProtected && protectedTags.Count > 0)
                {
                    // Маркер нужен и пустому элементу: у <code></code> нет LiteralInline,
                    // но пробел рядом с ним всё равно не является краем блока.
                    literals.Add(null);
                    editable.Add(false);
                }
            }
            else if (inline is LiteralInline literal)
            {
                literals.Add(literal);
                editable.Add(protectedTags.Count == 0);
            }
            else if (inline is ContainerInline nested)
            {
                // HTML внутри подписи изображения или другого вложенного узла не является
                // соседом текста после контейнера и не вправе менять его защищённость.
                CollectLiterals(nested, literals, editable, [.. protectedTags]);
            }
        }
    }

    /// <summary>
    /// Пересчитывает стек защищённых зон по встреченному тегу. В сыром содержимом script,
    /// style и textarea распознаётся только закрывающий тег текущей зоны: похожая на тег
    /// строка внутри кода границы не меняет.
    /// Разбор тега и список элементов — из ядра: свой список разъехался бы при первой правке.
    /// </summary>
    /// <param name="tag">Тег целиком, как его записал автор: «&lt;code&gt;», «&lt;/kbd&gt;».</param>
    /// <param name="protectedTags">Стек открытых защищённых элементов.</param>
    private static void UpdateProtectedZone(string tag, List<string> protectedTags)
    {
        if (!Tags.TryReadName(tag, out ReadOnlySpan<char> name, out bool closing))
        {
            return;
        }

        if (protectedTags.Count > 0)
        {
            string current = protectedTags[^1];
            if (MarkupScanner.IsRawTextTag(current))
            {
                if (closing && Tags.NameIs(name, current))
                {
                    protectedTags.RemoveAt(protectedTags.Count - 1);
                }

                return;
            }

            if (closing)
            {
                if (Tags.NameIs(name, current))
                {
                    protectedTags.RemoveAt(protectedTags.Count - 1);
                }

                return;
            }
        }

        if (closing || Tags.IsSelfClosing(tag))
        {
            return;
        }

        foreach (string candidate in MarkupScanner.ProtectedTags)
        {
            if (Tags.NameIs(name, candidate))
            {
                protectedTags.Add(candidate);
                return;
            }
        }
    }
}
