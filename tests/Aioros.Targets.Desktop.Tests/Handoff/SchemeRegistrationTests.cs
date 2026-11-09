using System.Diagnostics;
using Aioros.Targets.Desktop.Handoff;

namespace Aioros.Targets.Desktop.Tests.Handoff;

public sealed class SchemeRegistrationTests
{
    [Fact(Timeout = 30_000)]
    [Trait("AC", "INSTALL-06")]
    public async Task LinuxRegistersTheSchemeInTheGivenXdgDirectoriesAsync()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The XDG registration is Linux's.");
        var bound = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("aioros-xdg-");
        try
        {
            var data = Path.Combine(root.FullName, "data");
            var config = Path.Combine(root.FullName, "config");
            Directory.CreateDirectory(config);
            var list = Path.Combine(config, "mimeapps.list");
            await File.WriteAllTextAsync(list, "[Default Applications]\ntext/html=firefox.desktop\n\n[Added Associations]\ntext/plain=gedit.desktop;\n", bound);
            var places = new XdgPlaces(data, config);

            var first = await LinuxSchemeRegistration.RegisterAsync("shop", "Shop", "/opt/Shop/Shop.AppImage", places, bound);
            var entry = await File.ReadAllLinesAsync(Path.Combine(data, "applications", "shop-handler.desktop"), bound);
            var defaults = await File.ReadAllTextAsync(list, bound);
            var stamps = (File.GetLastWriteTimeUtc(list), File.GetLastWriteTimeUtc(Path.Combine(data, "applications", "shop-handler.desktop")));
            var query = await QueryAsync(places, bound);
            var again = await LinuxSchemeRegistration.RegisterAsync("shop", "Shop", "/opt/Shop/Shop.AppImage", places, bound);
            var unchanged = (File.GetLastWriteTimeUtc(list), File.GetLastWriteTimeUtc(Path.Combine(data, "applications", "shop-handler.desktop")));
            var moved = await LinuxSchemeRegistration.RegisterAsync("shop", "Shop", "/home/ana/Apps/Shop $1.AppImage", places, bound);
            var movedEntry = await File.ReadAllLinesAsync(Path.Combine(data, "applications", "shop-handler.desktop"), bound);

            Assert.True(first);
            Assert.Contains("Exec=\"/opt/Shop/Shop.AppImage\" %u", entry);
            Assert.Contains("MimeType=x-scheme-handler/shop;", entry);
            Assert.Contains("NoDisplay=true", entry);
            Assert.Contains("Type=Application", entry);
            Assert.Equal("[Desktop Entry]", entry[0]);
            Assert.Equal("[Default Applications]\ntext/html=firefox.desktop\nx-scheme-handler/shop=shop-handler.desktop\n\n[Added Associations]\ntext/plain=gedit.desktop;\n", defaults);
            Assert.True(query is null || query == "shop-handler.desktop", query);
            Assert.False(again);
            Assert.Equal(stamps, unchanged);
            Assert.True(moved);
            Assert.Contains("Exec=\"/home/ana/Apps/Shop \\\\$1.AppImage\" %u", movedEntry);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("AC", "INSTALL-07")]
    public void WindowsRegistersTheSchemeUnderTheCurrentUsersClasses()
    {
        var entries = WindowsSchemeRegistration.Entries("shop", "Shop", @"C:\Apps\Shop\Shop.exe");

        Assert.Equal(
        [
            new RegistryValue(@"Software\Classes\shop", string.Empty, "URL:Shop"),
            new RegistryValue(@"Software\Classes\shop", "URL Protocol", string.Empty),
            new RegistryValue(@"Software\Classes\shop\shell\open\command", string.Empty, "\"C:\\Apps\\Shop\\Shop.exe\" \"%1\""),
        ], entries);
        Assert.Equal([entries[2]], WindowsSchemeRegistration.Changes(entries, value => value == entries[2] ? "\"C:\\Old\\Shop.exe\" \"%1\"" : value.Value));
        Assert.Empty(WindowsSchemeRegistration.Changes(entries, value => value.Value));
    }

    private static async Task<string?> QueryAsync(XdgPlaces places, CancellationToken cancellationToken)
    {
        if (!(Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':').Any(folder => File.Exists(Path.Combine(folder, "xdg-mime"))))
        {
            return null;
        }

        var start = new ProcessStartInfo("xdg-mime", ["query", "default", "x-scheme-handler/shop"]) { RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["XDG_DATA_HOME"] = places.DataHome;
        start.Environment["XDG_CONFIG_HOME"] = places.ConfigHome;
        start.Environment["XDG_DATA_DIRS"] = places.DataHome;
        start.Environment["XDG_CONFIG_DIRS"] = places.ConfigHome;
        start.Environment["XDG_CURRENT_DESKTOP"] = "X-Generic";
        start.Environment.Remove("KDE_FULL_SESSION");
        start.Environment.Remove("GNOME_DESKTOP_SESSION_ID");
        start.Environment.Remove("DESKTOP_SESSION");
        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output.Trim();
    }
}
