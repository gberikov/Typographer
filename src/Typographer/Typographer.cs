namespace Typographer;

/// <summary>Типографирование с настройками по умолчанию.</summary>
public static class Typographer
{
    /// <summary>Типографирует HTML-фрагмент настройками по умолчанию.</summary>
    /// <param name="html">Исходный фрагмент.</param>
    /// <returns>Обработанный фрагмент.</returns>
    public static string Html(string html) => HtmlTypographer.Default.Process(html);

    /// <summary>Типографирует обычный текст настройками по умолчанию.</summary>
    /// <param name="text">Исходный текст.</param>
    /// <returns>Обработанный текст.</returns>
    public static string PlainText(string text) => TextTypographer.Default.Process(text);
}
