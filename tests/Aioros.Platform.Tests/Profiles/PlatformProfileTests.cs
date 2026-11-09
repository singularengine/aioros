using System.Reflection;
using Aioros.Platform;
using Avalonia.Threading;

namespace Aioros.Platform.Tests.Profiles;

public sealed class PlatformProfileTests
{
    private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
        name => variables.FirstOrDefault(variable => variable.Name == name).Value;

    [Fact]
    [Trait("AC", "PLATFORM-01")]
    public void TheSessionDecidesTheRules()
    {
        var wayland = PlatformProfile.Detect(Environment(("WAYLAND_DISPLAY", "wayland-1"), ("XDG_CURRENT_DESKTOP", "Hyprland")), PlatformOs.Linux);
        var x11 = PlatformProfile.Detect(Environment(("DISPLAY", ":0")), PlatformOs.Linux);
        var forced = PlatformProfile.Detect(Environment(("WAYLAND_DISPLAY", "wayland-1"), ("AIOROS_DESKTOP_X11", "1")), PlatformOs.Linux);
        var windows = PlatformProfile.Detect(Environment(("WAYLAND_DISPLAY", "wayland-1")), PlatformOs.Windows);

        Assert.Equal(PlatformWindowing.Wayland, wayland.Windowing);
        Assert.Equal("Hyprland", wayland.Desktop);
        Assert.Equal([PlatformRules.WaylandBackend, PlatformRules.DispatcherClock], wayland.Rules);
        Assert.Equal(PlatformWindowing.X11, x11.Windowing);
        Assert.Empty(x11.Rules);
        Assert.Equal(PlatformWindowing.X11, forced.Windowing);
        Assert.Empty(forced.Rules);
        Assert.Equal(PlatformWindowing.Native, windows.Windowing);
        Assert.Empty(windows.Rules);
    }

    [Fact]
    [Trait("AC", "PLATFORM-02")]
    public void TheProfileDescribesWhatItDecided()
    {
        var wayland = PlatformProfile.Detect(Environment(("WAYLAND_DISPLAY", "wayland-1"), ("XDG_CURRENT_DESKTOP", "Hyprland")), PlatformOs.Linux);
        var macos = PlatformProfile.Detect(Environment(), PlatformOs.MacOS);

        Assert.Equal("platform: linux wayland (Hyprland) · wayland backend · dispatcher clock (Avalonia #22064)", wayland.Describe());
        Assert.Equal("platform: macos native", macos.Describe());
    }

    [Fact]
    [Trait("AC", "PLATFORM-03")]
    public void TheDispatcherClockFollowsThePlatform()
    {
        var dispatcher = Dispatcher.UIThread;
        var fields = typeof(Dispatcher).GetRuntimeFields().Where(field => !field.IsStatic).ToList();
        var platform = fields.First(field => field.Name == "_impl").GetValue(dispatcher)!;
        var now = platform.GetType().GetInterfaces().Select(contract => contract.GetProperty("Now")).First(property => property is not null)!;
        var provider = fields.First(field => field.Name == "_timeProvider");
        provider.SetValue(dispatcher, new Func<long>(() => (long)now.GetValue(platform)! + 100_000));

        var aligned = DispatcherClock.Align();
        var before = (long)now.GetValue(platform)!;
        var followed = ((Func<long>)provider.GetValue(dispatcher)!)();
        var after = (long)now.GetValue(platform)!;

        Assert.True(aligned);
        Assert.InRange(followed, before, after);
    }
}
