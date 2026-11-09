using System.Collections.Immutable;
using System.Text;

namespace Aioros.Platform;

/// <summary>The operating system an app runs on, as far as the profile needs to know it.</summary>
public enum PlatformOs
{
    Linux,
    Windows,
    MacOS,
    Other,
}

/// <summary>How the app's windows are drawn: Avalonia's Wayland or X11 backend, or the operating system's own.</summary>
public enum PlatformWindowing
{
    Wayland,
    X11,
    Native,
}

/// <summary>A setting the profile applies, with the problem it answers.</summary>
public sealed record PlatformRule(string Name, string Cause)
{
    public override string ToString() => Cause.Length == 0 ? Name : $"{Name} ({Cause})";
}

/// <summary>The rules the profile knows. Each one answers a problem reproduced and measured, and goes when its cause is fixed.</summary>
public static class PlatformRules
{
    /// <summary>Avalonia's Wayland backend: windows drawn at the screen's own scale, sharp at fractional scales, where the compositor stretches an X11 window.</summary>
    public static PlatformRule WaylandBackend { get; } = new("wayland backend", string.Empty);

    /// <summary>The dispatcher's clock aligned with the platform's (<see cref="DispatcherClock"/>).</summary>
    public static PlatformRule DispatcherClock { get; } = new("dispatcher clock", "Avalonia #22064");
}

/// <summary>Where an Avalonia app runs and the rules applied there.</summary>
public sealed record PlatformProfile(PlatformOs Os, PlatformWindowing Windowing, string? Desktop, ImmutableArray<PlatformRule> Rules)
{
    /// <summary>Set to 1 to draw through X11 even where a Wayland compositor runs.</summary>
    public const string X11Variable = "AIOROS_DESKTOP_X11";

    /// <summary>The profile of the running process.</summary>
    public static PlatformProfile Current { get; } = Detect(Environment.GetEnvironmentVariable, CurrentOs());

    public static PlatformProfile Detect(Func<string, string?> environment, PlatformOs os)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var desktop = environment("XDG_CURRENT_DESKTOP") is { Length: > 0 } name ? name : null;
        if (os != PlatformOs.Linux)
        {
            return new PlatformProfile(os, PlatformWindowing.Native, desktop, []);
        }

        return environment("WAYLAND_DISPLAY") is { Length: > 0 } && environment(X11Variable) != "1"
            ? new PlatformProfile(os, PlatformWindowing.Wayland, desktop, [PlatformRules.WaylandBackend, PlatformRules.DispatcherClock])
            : new PlatformProfile(os, PlatformWindowing.X11, desktop, []);
    }

    /// <summary>One line with what was detected and applied, for the app's log.</summary>
    public string Describe()
    {
        var line = new StringBuilder("platform: ").Append(Os.ToString().ToLowerInvariant()).Append(' ').Append(Windowing.ToString().ToLowerInvariant());
        if (Windowing != PlatformWindowing.Native && Desktop is not null)
        {
            line.Append(" (").Append(Desktop).Append(')');
        }

        foreach (var rule in Rules)
        {
            line.Append(" · ").Append(rule);
        }

        return line.ToString();
    }

    private static PlatformOs CurrentOs() =>
        OperatingSystem.IsLinux() ? PlatformOs.Linux
        : OperatingSystem.IsWindows() ? PlatformOs.Windows
        : OperatingSystem.IsMacOS() ? PlatformOs.MacOS
        : PlatformOs.Other;
}
