using Aioros.Hosting;
using Microsoft.Extensions.Configuration;

namespace Aioros.Targets.Desktop.Tests.Handoff;

public sealed class InstalledModeTests
{
    [Fact]
    [Trait("AC", "INSTALL-13")]
    public void TheInstalledAppRunsOnTheDesktopAndDeepLinksAreNoSettings()
    {
        string[] args = [AiorosMode.DesktopFlag, "shop://open?path=%2Forders%2F42", "--Aioros:Server=http://server.test"];
        var settings = new ConfigurationBuilder().AddCommandLine(AiorosMode.Settings(args)).Build();
        var none = new ConfigurationBuilder().Build();

        var linked = AiorosMode.From(args, settings);
        var installed = AiorosMode.From([], none, installed: true);
        var web = AiorosMode.From([], none);

        Assert.Equal(["--Aioros:Server=http://server.test"], AiorosMode.Settings(args));
        Assert.DoesNotContain(settings.AsEnumerable(), setting => setting.Key.StartsWith("shop", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(AiorosRole.Client, linked.Role);
        Assert.True(installed.Desktop);
        Assert.Equal(AiorosRole.InProcess, installed.Role);
        Assert.False(web.Desktop);
    }

    [Fact]
    [Trait("AC", "INSTALL-20")]
    public void TheUpdateFeedIsTheServersUnlessConfigured()
    {
        var server = new Uri("https://shop.example");

        Assert.Equal("/srv/feed", AiorosMode.UpdateFeed("/srv/feed", server));
        Assert.Equal("https://shop.example/updates/", AiorosMode.UpdateFeed(null, server));
        Assert.Equal("https://shop.example/updates/", AiorosMode.UpdateFeed(string.Empty, server));
        Assert.Null(AiorosMode.UpdateFeed(null, null));
    }
}
