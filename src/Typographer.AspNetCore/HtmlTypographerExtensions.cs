using Microsoft.AspNetCore.Html;

namespace Typographer.AspNetCore;

/// <summary>Типографирование для представлений Razor.</summary>
public static class HtmlTypographerExtensions
{
    /// <summary>Типографирует фрагмент и возвращает его как готовую разметку.</summary>
    /// <param name="typographer">Типограф HTML.</param>
    /// <param name="html">Исходный фрагмент; <c>null</c> даёт пустую разметку.</param>
    /// <returns>Разметка, готовая к выводу в представлении.</returns>
    /// <exception cref="ArgumentNullException">Типограф равен <c>null</c>.</exception>
    /// <remarks>
    /// Возвращается <see cref="IHtmlContent"/>, а не строка: результат уже содержит
    /// разметку, и повторное кодирование шаблонизатором превратило бы её в текст.
    /// В представлении:
    /// <code>
    /// @inject HtmlTypographer Typographer
    /// @Typographer.ToHtmlContent(Model.Text)
    /// </code>
    /// </remarks>
    public static IHtmlContent ToHtmlContent(this HtmlTypographer typographer, string? html)
    {
        ArgumentNullException.ThrowIfNull(typographer);

        return html is null ? HtmlString.Empty : new HtmlString(typographer.Process(html));
    }
}
