using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Looks;

internal static class ThemeResources
{
    public static string? Paint(Control control, string key) => Find(control, key) switch
    {
        IBrush brush => CssValues.Brush(brush) ?? "transparent",
        Color color => CssValues.Color(color),
        string text => text,
        _ => null,
    };

    public static string Css(Control control, string key, string fallback) => Find(control, key) switch
    {
        IBrush brush => CssValues.Brush(brush) ?? "transparent",
        Color color => CssValues.Color(color),
        Thickness thickness => BoxStyles.Edges(thickness),
        CornerRadius radius => string.Create(CultureInfo.InvariantCulture, $"{CssValues.Px(radius.TopLeft)} {CssValues.Px(radius.TopRight)} {CssValues.Px(radius.BottomRight)} {CssValues.Px(radius.BottomLeft)}"),
        double number => CssValues.Px(number),
        _ => fallback,
    };

    public static double Number(Control control, string key, double fallback) => Find(control, key) is double number ? number : fallback;

    private static object? Find(Control control, string key) =>
        control.TryFindResource(key, control.ActualThemeVariant, out var value) ? value : null;
}
