using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Metadata;

namespace Aioros.Avalonia.Controls;

/// <summary>
/// Avalonia's <c>OnPlatform</c> with a <c>Web</c> value (<c>{a:OnPlatform Default=…, Linux=…, Web=…}</c>). Avalonia's XAML
/// compiler turns it into one branch per option, as it does Avalonia's own: on the desktop the running operating
/// system's value applies, and when the page is rendered for the web the <c>Web</c> value, else <c>Default</c>.
/// </summary>
public sealed class OnPlatformExtension : IAddChild<On>
{
    public OnPlatformExtension()
    {
    }

    public OnPlatformExtension(object? defaultValue) => Default = defaultValue;

    [MarkupExtensionDefaultOption]
    public object? Default { get; set; }

    [MarkupExtensionOption(XamlPlatform.Windows)]
    public object? Windows { get; set; }

    [MarkupExtensionOption(XamlPlatform.MacOS)]
    public object? macOS { get; set; }

    [MarkupExtensionOption(XamlPlatform.Linux)]
    public object? Linux { get; set; }

    [MarkupExtensionOption(XamlPlatform.Android)]
    public object? Android { get; set; }

    [MarkupExtensionOption(XamlPlatform.IOS)]
    public object? iOS { get; set; }

    [MarkupExtensionOption(XamlPlatform.Web)]
    public object? Web { get; set; }

    /// <summary>Whether the branch of <paramref name="option"/> applies where the XAML is loaded; called by the compiled XAML.</summary>
    public static bool ShouldProvideOption(string option) => XamlPlatform.Selects(option);

    /// <summary>Required by Avalonia's XAML compiler, which replaces the call with the branches.</summary>
    public object ProvideValue() => this;

    void IAddChild<On>.AddChild(On child)
    {
    }
}
