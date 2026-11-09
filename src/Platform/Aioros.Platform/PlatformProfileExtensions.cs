using Avalonia;

namespace Aioros.Platform;

public static class PlatformProfileExtensions
{
    /// <summary>
    /// Configures the app's platform for where it runs, in place of <c>UsePlatformDetect()</c>: Avalonia's own platform
    /// detection plus the rules of <see cref="PlatformProfile.Current"/>.
    /// </summary>
    public static AppBuilder UsePlatformProfile(this AppBuilder builder) => builder.UsePlatformProfile(PlatformProfile.Current);

    public static AppBuilder UsePlatformProfile(this AppBuilder builder, PlatformProfile profile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(profile);
        builder = builder.UsePlatformDetect();
        if (profile.Rules.Contains(PlatformRules.WaylandBackend))
        {
            builder = builder.UseWaylandWithFallback();
        }

        if (profile.Rules.Contains(PlatformRules.DispatcherClock))
        {
            builder = builder.AfterPlatformServicesSetup(_ => DispatcherClock.Align());
        }

        return builder;
    }
}
