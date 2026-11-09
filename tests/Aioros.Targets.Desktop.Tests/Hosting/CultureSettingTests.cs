using System.Globalization;
using Aioros.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aioros.Targets.Desktop.Tests.Hosting;

public sealed class CultureSettingTests
{
    [Fact]
    [Trait("AC", "SSR-222")]
    public void TheCulturesSettingLocalizesTheWebsiteAndNotTheDesktopApp()
    {
        var culture = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        string[] setting = ["--" + AiorosHost.CulturesSetting + "=en-US, fr-FR"];

        var web = AiorosHost.CreateBuilder(setting, App());
        var desktop = AiorosHost.CreateBuilder([AiorosMode.DesktopFlag, .. setting], App());
        using var services = web.Services.BuildServiceProvider();
        var options = services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;

        Assert.Equal(["en-US", "fr-FR"], options.SupportedCultures!.Select(supported => supported.Name));
        Assert.Equal(["en-US", "fr-FR"], options.SupportedUICultures!.Select(supported => supported.Name));
        Assert.Equal("en-US", options.DefaultRequestCulture.Culture.Name);
        Assert.DoesNotContain(desktop.Services, service => service.ServiceType == typeof(IConfigureOptions<RequestLocalizationOptions>));
        Assert.Equal(culture, (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture));
    }

    private static AiorosApp App() => new([], static () => typeof(TestApp), typeof(DesktopFixture).Assembly, AppContext.BaseDirectory);
}
