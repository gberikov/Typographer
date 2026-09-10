using Microsoft.Extensions.DependencyInjection;

namespace Typographer.Integrations.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddTypographer_RegistersBothTypographers()
    {
        ServiceProvider provider = new ServiceCollection().AddTypographer().BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<HtmlTypographer>());
        Assert.NotNull(provider.GetRequiredService<TextTypographer>());
    }

    [Fact]
    public void AddTypographer_RegistersAsSingletons()
    {
        ServiceProvider provider = new ServiceCollection().AddTypographer().BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<HtmlTypographer>(), provider.GetRequiredService<HtmlTypographer>());
    }

    [Fact]
    public void AddTypographer_PassesOptionsToTypographer()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddTypographer(new HtmlOptions { Entities = EntityMode.Named })
            .BuildServiceProvider();

        Assert.Contains("&laquo;", provider.GetRequiredService<HtmlTypographer>().Process("\"цитата\""));
    }

    // TryAdd, а не Add: своя регистрация типографа должна побеждать. Иначе AddTypographer,
    // вызванный библиотекой, молча затирал бы настройку приложения.
    [Fact]
    public void AddTypographer_DoesNotOverrideExistingRegistration()
    {
        var mine = new HtmlTypographer(new HtmlOptions { Entities = EntityMode.Numeric });
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(mine)
            .AddTypographer()
            .BuildServiceProvider();

        Assert.Same(mine, provider.GetRequiredService<HtmlTypographer>());
    }

    [Fact]
    public void AddTypographer_NullThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddTypographer());
    }
}
