namespace Aioros.Avalonia.Controls;

/// <summary>
/// Where XAML is loaded: on the desktop, or for the web, set once by the host that renders pages for the web before its
/// application loads. Avalonia evaluates <c>OnPlatform</c> and <c>OnFormFactor</c> the same way, from the platform of the
/// process.
/// </summary>
internal static class XamlPlatform
{
    public const string Windows = "WINDOWS";
    public const string MacOS = "OSX";
    public const string Linux = "LINUX";
    public const string Android = "ANDROID";
    public const string IOS = "IOS";
    public const string Web = "WEB";

    private static int _web;

    public static bool IsWeb => Volatile.Read(ref _web) == 1;

    public static void UseWeb() => Volatile.Write(ref _web, 1);

    public static bool Selects(string option) => IsWeb
        ? option == Web
        : option switch
        {
            Windows => OperatingSystem.IsWindows(),
            MacOS => OperatingSystem.IsMacOS(),
            Linux => OperatingSystem.IsLinux(),
            Android => OperatingSystem.IsAndroid(),
            IOS => OperatingSystem.IsIOS(),
            _ => false,
        };
}
