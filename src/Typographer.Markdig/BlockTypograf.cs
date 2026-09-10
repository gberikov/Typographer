using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Typographer.Markdig;

/// <summary>Типографика текста одного блока с сохранением разметки внутри него.</summary>
/// <remarks>
/// Директивы using стоят ДО объявления пространства имён намеренно: внутри
/// <c>Typographer.Markdig</c> имя <c>Markdig</c> разрешается в это же пространство,
/// и обратиться к типам библиотеки через <c>Markdig.Syntax.X</c> было бы нельзя.
/// </remarks>
internal static class BlockTypograf
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
    /// <param name="typograf">Типограф обычного текста.</param>
    public static void Apply(ContainerInline root, TextTypograf typograf)
    {
        List<LiteralInline> literals = [];
        foreach (LiteralInline literal in root.Descendants<LiteralInline>())
        {
            literals.Add(literal);
        }

        if (literals.Count == 0)
        {
            return;
        }

        var parts = new string[literals.Count];
        for (int i = 0; i < literals.Count; i++)
        {
            parts[i] = literals[i].Content.ToString();

            // Чужой U+FFFC во входе сделал бы раскладку обратно неоднозначной.
            // Такой блок остаётся нетронутым: испортить текст хуже, чем не улучшить.
            if (parts[i].IndexOf(Object) >= 0)
            {
                return;
            }
        }

        string[] result = typograf.Process(string.Join(Separator, parts)).Split(Object);

        // Правило могло съесть разделитель вместе с соседним пробелом или размножить его.
        // Раскладывать нечего — блок остаётся как был.
        if (result.Length != literals.Count)
        {
            return;
        }

        for (int i = 0; i < literals.Count; i++)
        {
            if (!string.Equals(result[i], parts[i], StringComparison.Ordinal))
            {
                literals[i].Content = new StringSlice(result[i]);
            }
        }
    }
}
