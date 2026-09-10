using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Typographer.AspNetCore;

/// <summary>Тег-хелпер <c>&lt;typograf&gt;</c>: типографирует содержимое элемента.</summary>
/// <remarks>
/// Содержимое к этому моменту уже отрендерено и закодировано шаблонизатором, то есть
/// является готовым HTML, — потому обрабатывается <see cref="HtmlTypograf"/>, а не
/// текстовым типографом, и возвращается как разметка, а не как текст.
/// Требует регистрации типографа в контейнере: <c>services.AddTypograf()</c>.
/// </remarks>
[HtmlTargetElement("typograf")]
public sealed class TypografTagHelper : TagHelper
{
    private readonly HtmlTypograf _typograf;

    /// <summary>Создаёт тег-хелпер с типографом из контейнера.</summary>
    /// <param name="typograf">Типограф HTML.</param>
    /// <exception cref="ArgumentNullException">Типограф равен <c>null</c>.</exception>
    public TypografTagHelper(HtmlTypograf typograf)
    {
        ArgumentNullException.ThrowIfNull(typograf);
        _typograf = typograf;
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
        output.Content.SetHtmlContent(_typograf.Process(content.GetContent()));
    }
}
