using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

internal static class RangeParts
{
    public static T? Part<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(part => part.Name == name);

    public static string Color(IBrush? brush) => CssValues.Brush(brush) ?? "transparent";

    public static string Resource(Control control, string key, string fallback) =>
        control.TryFindResource(key, control.ActualThemeVariant, out var value)
            ? value switch
            {
                IBrush brush => Color(brush),
                Color color => CssValues.Color(color),
                _ => fallback,
            }
            : fallback;

    public static double Number(object? value) =>
        value is IConvertible convertible ? convertible.ToDouble(CultureInfo.InvariantCulture) : 0;

    public static void Slot(HtmlContext context, AvaloniaObject target, AvaloniaProperty property, Format format) =>
        TextWriters.WriteValue(context, target, property, format);

    public static string Disabled(Control control) => control.IsEffectivelyEnabled ? string.Empty : " disabled";
}
