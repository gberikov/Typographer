using Microsoft.Extensions.DependencyInjection.Extensions;
using Typographer;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Регистрация типографа в контейнере служб.</summary>
/// <remarks>
/// Пространство имён взято контейнерное, а не <c>Typographer.*</c>: так метод виден там,
/// где уже написано <c>using Microsoft.Extensions.DependencyInjection</c>, без второго using.
/// Обёртки над <c>IOptions</c> здесь нет намеренно: типографы и их настройки иммутабельны,
/// перенастраивать их в рантайме нечего, а <c>IOptionsMonitor</c> добавил бы зависимость
/// ради значения, которое никогда не меняется.
/// </remarks>
public static class TypografServiceCollectionExtensions
{
    /// <summary>Регистрирует <see cref="HtmlTypograf"/> и <see cref="TextTypograf"/> одиночками.</summary>
    /// <param name="services">Коллекция служб.</param>
    /// <param name="html">Настройки типографа HTML; <c>null</c> — настройки по умолчанию.</param>
    /// <param name="text">Настройки типографа текста; <c>null</c> — настройки по умолчанию.</param>
    /// <returns>Та же коллекция служб, чтобы вызовы выстраивались в цепочку.</returns>
    /// <exception cref="ArgumentNullException">Коллекция служб равна <c>null</c>.</exception>
    public static IServiceCollection AddTypograf(
        this IServiceCollection services,
        HtmlOptions? html = null,
        TextOptions? text = null)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton(new HtmlTypograf(html));
        services.TryAddSingleton(new TextTypograf(text));
        return services;
    }
}
