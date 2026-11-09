using Aioros.Avalonia.Tests.Controls;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class PlatformValueTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "NATIVE-01")]
    public async Task TheWebValueIsWrittenOnTheWebAsync()
    {
        var page = await fixture.CompileAsync("""
            <TextBlock Text="{a:OnPlatform Default=default, Windows=windows, macOS=macos, Linux=linux, Android=android, iOS=ios, Web=web}" />
            """);

        var html = EngineFixture.Body(page, (object?)null);
        Assert.Contains(">web<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("linux", html, StringComparison.Ordinal);
        Assert.Null(page.Failure);
        Assert.True(page.Reusable);
    }

    [Fact]
    [Trait("AC", "NATIVE-02")]
    public async Task WithoutAWebValueTheDefaultIsWrittenAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TextBlock Text="{a:OnPlatform Default=default, Windows=windows, macOS=macos, Linux=linux}" />
              <TextBlock Text="{a:OnPlatform Windows=windows, macOS=macos, Linux=linux}" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page, (object?)null);
        Assert.Contains(">default<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("linux", html, StringComparison.Ordinal);
        Assert.True(page.Reusable);
    }

    [Fact]
    [Trait("AC", "NATIVE-04")]
    public async Task SectionsAndVariantsFollowThePlatformAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border IsVisible="{a:OnPlatform Default=True, Web=False}"><TextBlock Text="native section" /></Border>
              <Border IsVisible="{a:OnPlatform Default=False, Web=True}"><TextBlock Text="web section" /></Border>
              <Button Content="{a:OnPlatform Default=Pick a folder, Web=Get the app}" />
              <TextBlock Text="{a:OnPlatform Default=Saved on this computer, Web=Saved in your account}" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page, (object?)null);
        Assert.DoesNotContain("native section", html, StringComparison.Ordinal);
        Assert.Contains("web section", html, StringComparison.Ordinal);
        Assert.Contains("Get the app", html, StringComparison.Ordinal);
        Assert.Contains("Saved in your account", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Pick a folder", html, StringComparison.Ordinal);
        Assert.Null(page.Failure);
        Assert.True(page.Reusable);
    }

    [Fact]
    [Trait("AC", "NATIVE-05")]
    public async Task FormFactorsAreDesktopOnTheWebAsync()
    {
        var page = await fixture.CompileAsync("""
            <TextBlock Text="{OnFormFactor Default=other, Desktop=wide, Mobile=narrow}" />
            """);

        var html = EngineFixture.Body(page, (object?)null);
        Assert.Contains(">wide<", html, StringComparison.Ordinal);
        Assert.True(page.Reusable);
    }
}
