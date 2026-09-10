using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;

namespace Typographer.Markdig;

/// <summary>Расширение Markdig: типографика применяется к дереву документа после разбора.</summary>
/// <remarks>
/// После разбора, а не после рендеринга: рендерер кодирует прямую кавычку в
/// <c>&amp;quot;</c>, а типограф сущности разметки не декодирует, и кавычки-ёлочки
/// в готовом HTML уже не появились бы. Правка дерева работает с любым рендерером.
/// Разметка внутри абзаца не трогается: правкам подлежат только текстовые узлы, а
/// содержимое кода, адреса ссылок и встроенный HTML отдельными узлами и остаются.
/// </remarks>
public sealed class TypographerExtension : IMarkdownExtension
{
    private readonly TextTypographer _typographer;

    /// <summary>Создаёт расширение с указанным типографом.</summary>
    /// <param name="typographer">Типограф обычного текста; <c>null</c> — с настройками по умолчанию.</param>
    public TypographerExtension(TextTypographer? typographer = null) => _typographer = typographer ?? TextTypographer.Default;

    /// <summary>Подписывается на завершение разбора документа.</summary>
    /// <param name="pipeline">Строитель конвейера.</param>
    /// <exception cref="ArgumentNullException">Строитель равен <c>null</c>.</exception>
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (pipeline is null)
        {
            throw new ArgumentNullException(nameof(pipeline));
        }

        pipeline.DocumentProcessed += Apply;
    }

    /// <summary>Ничего не делает: правки уже в дереве, и рендерер увидит их сам.</summary>
    /// <param name="pipeline">Готовый конвейер.</param>
    /// <param name="renderer">Рендерер.</param>
    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }

    private void Apply(MarkdownDocument document)
    {
        foreach (LeafBlock block in document.Descendants<LeafBlock>())
        {
            // Inline есть у абзацев и заголовков; у блока кода и блока HTML его нет,
            // и именно поэтому они не типографируются — отдельной проверки не нужно.
            if (block.Inline is not null)
            {
                BlockTypographer.Apply(block.Inline, _typographer);
            }
        }
    }
}
