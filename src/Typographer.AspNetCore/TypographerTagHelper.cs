using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Typographer.AspNetCore;

/// <summary>Тег-хелпер <c>&lt;typographer&gt;</c>: типографирует содержимое элемента.</summary>
/// <remarks>
/// Содержимое к этому моменту уже отрендерено и закодировано шаблонизатором, то есть
/// является готовым HTML, — потому обрабатывается <see cref="HtmlTypographer"/>, а не
/// текстовым типографом, и возвращается как разметка, а не как текст.
/// Требует регистрации типографа в контейнере: <c>services.AddTypographer()</c>.
/// </remarks>
[HtmlTargetElement("typographer")]
public sealed class TypographerTagHelper : TagHelper
{
    private readonly HtmlTypographer _typographer;

    /// <summary>Создаёт тег-хелпер с типографом из контейнера.</summary>
    /// <param name="typographer">Типограф HTML.</param>
    /// <exception cref="ArgumentNullException">Типограф равен <c>null</c>.</exception>
    public TypographerTagHelper(HtmlTypographer typographer)
    {
        ArgumentNullException.ThrowIfNull(typographer);
        _typographer = typographer;
    }

    /// <summary>Заменяет содержимое элемента типографированным и убирает сам элемент.</summary>
    /// <param name="context">Контекст тег-хелпера.</param>
    /// <param name="output">Вывод тег-хелпера.</param>
    /// <exception cref="ArgumentNullException">Вывод равен <c>null</c>.</exception>
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        TagHelperContent content = await output.GetChildContentAsync().ConfigureAwait(false);

        output.TagName = null;
        output.Content.SetHtmlContent(_typographer.Process(content.GetContent()));
    }
}
