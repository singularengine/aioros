using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Vectors;

/// <summary>A shape's fill and stroke as SVG presentation attributes, with the pen Avalonia builds from its stroke properties.</summary>
internal static class ShapePaint
{
    private const double SvgMiterLimit = 4;

    /// <summary>
    /// The shape's element with its paint, placed by <paramref name="placement"/>; a conic gradient fill, which SVG has no
    /// paint for, is the CSS gradient in a <c>&lt;foreignObject&gt;</c> over the geometry's bounds clipped to the geometry,
    /// under the element's stroke.
    /// </summary>
    public static string Markup(Shape shape, ShapeElement element, SvgPaints paints, string placement, Rect target, Matrix toUser, bool nonScaling = false)
    {
        var paint = Attributes(shape, paints, target, toUser, element.EvenOdd, nonScaling);
        if (shape.Fill is not IConicGradientBrush { GradientStops.Count: > 0 } conic)
        {
            return $"<{element.Tag}{placement}{element.Attributes}{paint}/>";
        }

        var bounds = element.Bounds;
        var center = conic.Center.ToPixels(bounds) - bounds.Position;
        var fill = string.Create(CultureInfo.InvariantCulture, $"<g{placement} clip-path=\"url(#{paints.Clip(element)})\"><foreignObject{ShapeElements.Offset("x", bounds.X)}{ShapeElements.Offset("y", bounds.Y)} width=\"{CssValues.Num(bounds.Width)}\" height=\"{CssValues.Num(bounds.Height)}\"><div style=\"height:100%;background:{CssGradients.Conic(conic, $"{CssValues.Px(center.X)} {CssValues.Px(center.Y)}")}\"></div></foreignObject></g>");
        return paint.Contains(" stroke=", StringComparison.Ordinal) ? $"{fill}<{element.Tag}{placement}{element.Attributes}{paint}/>" : fill;
    }

    /// <summary>
    /// The attributes for a shape whose rendered geometry has <paramref name="bounds"/>, drawn in a user space that
    /// <paramref name="toUser"/> maps the rendered geometry into.
    /// </summary>
    public static string Attributes(Shape shape, SvgPaints paints, Rect bounds, Matrix toUser, bool evenOdd, bool nonScaling = false)
    {
        var fill = paints.Paint(shape.Fill, bounds, toUser);
        var paint = new StringBuilder($" fill=\"{fill ?? "none"}\"");
        var thickness = shape.StrokeThickness;
        if (thickness > 0 && paints.Paint(shape.Stroke, bounds.Inflate(thickness / 2), toUser) is { } stroke)
        {
            paint.Append(CultureInfo.InvariantCulture, $" stroke=\"{stroke}\" stroke-width=\"{CssValues.Num(thickness)}\"");
            if (shape.StrokeDashArray is { Count: > 0 } dashes)
            {
                paint.Append(CultureInfo.InvariantCulture, $" stroke-dasharray=\"{string.Join(' ', dashes.Select(dash => CssValues.Num(dash * thickness)))}\"");
                paint.Append(shape.StrokeDashOffset != 0 ? string.Create(CultureInfo.InvariantCulture, $" stroke-dashoffset=\"{CssValues.Num(shape.StrokeDashOffset * thickness)}\"") : string.Empty);
            }

            paint.Append(shape.StrokeLineCap switch
            {
                PenLineCap.Round => " stroke-linecap=\"round\"",
                PenLineCap.Square => " stroke-linecap=\"square\"",
                _ => string.Empty,
            });
            paint.Append(shape.StrokeJoin switch
            {
                PenLineJoin.Round => " stroke-linejoin=\"round\"",
                PenLineJoin.Bevel => " stroke-linejoin=\"bevel\"",
                _ when Math.Abs(shape.StrokeMiterLimit - SvgMiterLimit) > 1e-9 => string.Create(CultureInfo.InvariantCulture, $" stroke-miterlimit=\"{CssValues.Num(shape.StrokeMiterLimit)}\""),
                _ => string.Empty,
            });
            paint.Append(nonScaling ? " vector-effect=\"non-scaling-stroke\"" : string.Empty);
        }

        paint.Append(fill is not null && evenOdd ? " fill-rule=\"evenodd\"" : string.Empty);
        return paint.ToString();
    }
}
