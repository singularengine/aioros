using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Avalonia;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Vectors;

/// <summary>An SVG element for a geometry: its tag, its geometric attributes, its bounds and its fill rule.</summary>
internal readonly record struct ShapeElement(string Tag, string Attributes, Rect Bounds, bool EvenOdd);

/// <summary>
/// A shape's geometry as the SVG element that states it: an untransformed rectangle geometry is a <c>&lt;rect&gt;</c>
/// with its corner radii and an ellipse geometry an <c>&lt;ellipse&gt;</c> (what Skia draws for them, without the
/// curves of their path data), anything else a <c>&lt;path&gt;</c> of its Skia path data.
/// </summary>
internal static class ShapeElements
{
    public static ShapeElement? Of(Geometry? geometry, bool filled)
    {
        if (geometry is null)
        {
            return null;
        }

        var plain = geometry.Transform is null || geometry.Transform.Value.IsIdentity;
        var bounds = geometry.Bounds;
        if (plain && geometry is RectangleGeometry rectangle)
        {
            var corners = rectangle.RadiusX > 0 || rectangle.RadiusY > 0
                ? string.Create(CultureInfo.InvariantCulture, $" rx=\"{CssValues.Num(rectangle.RadiusX)}\" ry=\"{CssValues.Num(rectangle.RadiusY)}\"")
                : string.Empty;
            return new("rect", string.Create(CultureInfo.InvariantCulture, $"{Offset("x", bounds.X)}{Offset("y", bounds.Y)} width=\"{CssValues.Num(bounds.Width)}\" height=\"{CssValues.Num(bounds.Height)}\"{corners}"), bounds, false);
        }

        if (plain && geometry is EllipseGeometry)
        {
            return new("ellipse", string.Create(CultureInfo.InvariantCulture, $" cx=\"{CssValues.Num(bounds.Center.X)}\" cy=\"{CssValues.Num(bounds.Center.Y)}\" rx=\"{CssValues.Num(bounds.Width / 2)}\" ry=\"{CssValues.Num(bounds.Height / 2)}\""), bounds, false);
        }

        return SkiaGeometry.Path(geometry, filled) is { } path ? new("path", $" d=\"{path.Data}\"", bounds, path.EvenOdd) : null;
    }

    public static string Offset(string name, double value) =>
        Math.Abs(value) < 0.0005 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $" {name}=\"{CssValues.Num(value)}\"");
}
