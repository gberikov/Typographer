using Markdig;

namespace Typographer.Markdig;

/// <summary>Подключение типографа к конвейеру Markdig.</summary>
public static class MarkdownPipelineBuilderExtensions
{
    /// <summary>Включает типографику текста в конвейере.</summary>
    /// <param name="pipeline">Строитель конвейера.</param>
    /// <param name="typographer">Типограф обычного текста; <c>null</c> — с настройками по умолчанию.</param>
    /// <returns>Тот же строитель, чтобы вызовы выстраивались в цепочку.</returns>
    /// <exception cref="ArgumentNullException">Строитель равен <c>null</c>.</exception>
    /// <remarks>Повторный вызов ничего не добавляет: расширение регистрируется однократно.</remarks>
    public static MarkdownPipelineBuilder UseTypographer(
        this MarkdownPipelineBuilder pipeline,
        TextTypographer? typographer = null)
    {
        if (pipeline is null)
        {
            throw new ArgumentNullException(nameof(pipeline));
        }

        pipeline.Extensions.AddIfNotAlready(new TypographerExtension(typographer));
        return pipeline;
    }
}
