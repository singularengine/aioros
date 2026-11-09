using Aioros.Pages;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopPlatformTests(DesktopFixture fixture)
{
    private static readonly PageDefinition[] Pages =
    [
        new("/platforms", "Pages/platforms/Page.axaml", _ => null, []),
        new("/native", "Pages/native/Page.axaml", _ => null, []) { Platforms = PagePlatforms.Desktop, Head = new PageHead("Files") },
    ];

    [Fact]
    [Trait("AC", "NATIVE-03")]
    public async Task TheRunningSystemsValueIsShownOnTheDesktopAsync()
    {
        var texts = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: Pages);
            await navigator.NavigateAsync("/platforms");
            var shown = DesktopFixture.Texts(window).ToList();
            window.Close();
            return shown;
        });

        var native = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux" : "default";
        Assert.Equal([native, "fallback"], texts);
    }

    [Fact]
    [Trait("AC", "NATIVE-12")]
    public async Task NativeOnlyPagesShowTheRealPageOnTheDesktopAsync()
    {
        var (texts, title) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: Pages);
            await navigator.NavigateAsync("/native");
            var shown = (DesktopFixture.Texts(window).ToList(), window.Title);
            window.Close();
            return shown;
        });

        Assert.Equal(["Native files page"], texts);
        Assert.Equal("Files", title);
    }
}
