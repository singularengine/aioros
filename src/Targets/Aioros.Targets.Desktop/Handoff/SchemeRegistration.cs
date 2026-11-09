namespace Aioros.Targets.Desktop.Handoff;

/// <summary>
/// Registers the installed app for its scheme where the operating system allows it at run time: Windows and Linux.
/// macOS reads <c>CFBundleURLTypes</c> from the bundle's <c>Info.plist</c>, written at build time.
/// </summary>
public static class SchemeRegistration
{
    /// <summary>
    /// The file the operating system should start: the AppImage on Linux (<c>$APPIMAGE</c>, since the process runs from
    /// its temporary mount), otherwise the process's own executable.
    /// </summary>
    public static string? Executable =>
        OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } image ? image : Environment.ProcessPath;

    /// <summary>Registers the scheme for the running executable; <see langword="true"/> when something changed.</summary>
    public static async Task<bool> RegisterAsync(string scheme, string title, CancellationToken cancellationToken)
    {
        if (Executable is not { } executable)
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return WindowsSchemeRegistration.Register(scheme, title, executable);
        }

        return OperatingSystem.IsLinux() && await LinuxSchemeRegistration.RegisterAsync(scheme, title, executable, XdgPlaces.Current, cancellationToken);
    }
}
