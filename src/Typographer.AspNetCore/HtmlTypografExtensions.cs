using Microsoft.AspNetCore.Html;

namespace Typographer.AspNetCore;

/// <summary>Типографирование для представлений Razor.</summary>
public static class HtmlTypografExtensions
{
    /// <summary>Типографирует фрагмент и возвращает его как готовую разметку.</summary>
    /// <param name="typograf">Типограф HTML.</param>
    /// <param name="html">Исходный фрагмент; <c>null</c> даёт пустую разметку.</param>
    /// <returns>Разметка, готовая к выводу в представлении.</returns>
    /// <exception cref="ArgumentNullException">Типограф равен <c>null</c>.</exception>
    /// <remarks>
    /// Возвращается <see cref="IHtmlContent"/>, а не строка: результат уже содержит
    /// разметку, и повторное кодирование шаблонизатором превратило бы её в текст.
    /// В представлении:
    /// <code>
    /// @inject HtmlTypograf Typograf
    /// @Typograf.ToHtmlContent(Model.Text)
    /// </code>
    /// </remarks>
    public static IHtmlContent ToHtmlContent(this HtmlTypograf typograf, string? html)
    {
        ArgumentNullException.ThrowIfNull(typograf);

        return html is null ? HtmlString.Empty : new HtmlString(typograf.Process(html));
    }
}
