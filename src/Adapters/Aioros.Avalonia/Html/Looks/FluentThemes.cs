using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>Whether a control wears Fluent's own control theme, which Aioros's writers render as their built-in look.</summary>
internal static class FluentThemes
{
    public static bool Owns(StyledElement control, Type type)
    {
        if (Application.Current?.Styles.OfType<FluentTheme>().FirstOrDefault() is not { } fluent
            || !fluent.TryGetResource(type, control is IThemeVariantHost host ? host.ActualThemeVariant : null, out var standard))
        {
            return false;
        }

        var own = control.Theme ?? (control.TryFindResource(type, out var found) ? found as ControlTheme : null);
        return ReferenceEquals(own, standard);
    }
}
