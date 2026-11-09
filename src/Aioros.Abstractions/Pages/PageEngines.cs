using System.Diagnostics.CodeAnalysis;

namespace Aioros.Pages;

/// <summary>
/// Whether this build can run the Avalonia page engine. A Native AOT publish turns the <see cref="AvaloniaSwitch"/>
/// feature switch off, so the trimmer removes every path to Avalonia, the generated code's included: the app then
/// serves the pages and posts its plans file covers, and nothing else.
/// </summary>
public static class PageEngines
{
    public const string AvaloniaSwitch = "Aioros.Avalonia.IsSupported";

    [FeatureSwitchDefinition(AvaloniaSwitch)]
    public static bool AvaloniaIsSupported => !AppContext.TryGetSwitch(AvaloniaSwitch, out var supported) || supported;
}
