using Aioros.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Aioros.Targets.Desktop.Tests.Hosting;

public sealed class CommandLineTests
{
    [Fact]
    [Trait("AC", "DESKTOP-54")]
    public void StandardSettingsReachTheHostWithOrWithoutTheDesktopSwitch()
    {
        var root = Path.GetTempPath();
        string[] settings = ["--urls", "http://127.0.0.1:5231", "--environment", "Staging", "--contentRoot", root, "Shop:Name=games", "--Shop:Owner", "ana"];

        var web = AiorosHost.CreateBuilder(settings, App());
        var desktop = AiorosHost.CreateBuilder([AiorosMode.DesktopFlag, "shop://open?path=%2F", .. settings], App());

        foreach (var builder in (WebApplicationBuilder[])[web, desktop])
        {
            Assert.Equal("http://127.0.0.1:5231", builder.Configuration[WebHostDefaults.ServerUrlsKey]);
            Assert.Equal("Staging", builder.Environment.EnvironmentName);
            Assert.Equal(Path.TrimEndingDirectorySeparator(root), Path.TrimEndingDirectorySeparator(builder.Environment.ContentRootPath));
            Assert.Equal("games", builder.Configuration["Shop:Name"]);
            Assert.Equal("ana", builder.Configuration["Shop:Owner"]);
            Assert.DoesNotContain(builder.Configuration.AsEnumerable(), setting => setting.Key.StartsWith("shop:", StringComparison.Ordinal) || setting.Value?.StartsWith("shop:", StringComparison.Ordinal) == true);
        }

        Assert.Equal(AiorosRole.Server, web.Role);
        Assert.Equal(AiorosRole.InProcess, desktop.Role);
    }

    private static AiorosApp App() => new([], static () => typeof(TestApp), typeof(DesktopFixture).Assembly, AppContext.BaseDirectory);
}
