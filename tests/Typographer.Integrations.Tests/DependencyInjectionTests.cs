using Microsoft.Extensions.DependencyInjection;

namespace Typographer.Integrations.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddTypograf_РегистрируетОбаТипографа()
    {
        ServiceProvider provider = new ServiceCollection().AddTypograf().BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<HtmlTypograf>());
        Assert.NotNull(provider.GetRequiredService<TextTypograf>());
    }

    [Fact]
    public void AddTypograf_РегистрируетОдиночками()
    {
        ServiceProvider provider = new ServiceCollection().AddTypograf().BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<HtmlTypograf>(), provider.GetRequiredService<HtmlTypograf>());
    }

    [Fact]
    public void AddTypograf_ПередаётНастройкиТипографу()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddTypograf(new HtmlOptions { Entities = EntityMode.Named })
            .BuildServiceProvider();

        Assert.Contains("&laquo;", provider.GetRequiredService<HtmlTypograf>().Process("\"цитата\""));
    }

    // TryAdd, а не Add: своя регистрация типографа должна побеждать. Иначе AddTypograf,
    // вызванный библиотекой, молча затирал бы настройку приложения.
    [Fact]
    public void AddTypograf_НеЗатираетЧужуюРегистрацию()
    {
        var mine = new HtmlTypograf(new HtmlOptions { Entities = EntityMode.Numeric });
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(mine)
            .AddTypograf()
            .BuildServiceProvider();

        Assert.Same(mine, provider.GetRequiredService<HtmlTypograf>());
    }

    [Fact]
    public void AddTypograf_NullБросаетArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddTypograf());
    }
}
