using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Avalonia;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Vectors;

/// <summary>
/// The paints of one <c>&lt;svg&gt;</c>: a solid brush is its color, a linear or radial gradient brush a gradient in its
/// <c>&lt;defs&gt;</c> placed as Avalonia's Skia renderer places it, in user space on the target rect (the geometry's
/// bounds for a fill, inflated by half the thickness for a stroke), with its spread method, its transform about its
/// transform origin and its opacity folded into the stops.
/// </summary>
internal sealed class SvgPaints
{
    private readonly StringBuilder _defs = new();
    private readonly HashSet<string> _written = new(StringComparer.Ordinal);

    public string Defs => _defs.Length == 0 ? string.Empty : $"<defs>{_defs}</defs>";

    /// <summary>The definitions written so far, without their <c>&lt;defs&gt;</c>.</summary>
    public string Definitions => _defs.ToString();

    /// <summary>The paint of <paramref name="brush"/> on <paramref name="target"/>, mapped into the element's user space by <paramref name="toUser"/>; null for no paint.</summary>
    public string? Paint(IBrush? brush, Rect target, Matrix toUser) => brush switch
    {
        ILinearGradientBrush linear when linear.GradientStops.Count > 0 => Reference(Linear(linear, target, toUser)),
        IRadialGradientBrush radial when radial.GradientStops.Count > 0 && target.Width > 0 => Reference(Radial(radial, target, toUser)),
        _ => CssValues.Brush(brush),
    };

    public static bool IsGradient(IBrush? brush) => brush is ILinearGradientBrush or IRadialGradientBrush;

    /// <summary>The id of a <c>&lt;clipPath&gt;</c> of <paramref name="element"/>'s geometry, defined once.</summary>
    public string Clip(ShapeElement element) =>
        Define("vc", $"<clipPath id=\"\"><{element.Tag}{element.Attributes}{(element.EvenOdd ? " clip-rule=\"evenodd\"" : string.Empty)}/></clipPath>");

    private string Reference(string gradient) => $"url(#{Define("vg", gradient)})";

    private string Define(string prefix, string definition)
    {
        var id = prefix + CssValues.Hash(definition);
        if (_written.Add(id))
        {
            _defs.Append(definition.Replace(" id=\"\"", $" id=\"{id}\"", StringComparison.Ordinal));
        }

        return id;
    }

    private static string Linear(ILinearGradientBrush brush, Rect target, Matrix toUser)
    {
        var start = brush.StartPoint.ToPixels(target);
        var end = brush.EndPoint.ToPixels(target);
        var transform = Transformed(brush, target, Matrix.Identity) * toUser;
        return $"<linearGradient id=\"\" gradientUnits=\"userSpaceOnUse\" x1=\"{CssValues.Num(start.X)}\" y1=\"{CssValues.Num(start.Y)}\" x2=\"{CssValues.Num(end.X)}\" y2=\"{CssValues.Num(end.Y)}\"{Spread(brush)}{Attribute(transform)}>{Stops(brush)}</linearGradient>";
    }

    private static string Radial(IRadialGradientBrush brush, Rect target, Matrix toUser)
    {
        var center = brush.Center.ToPixels(target);
        var origin = brush.GradientOrigin.ToPixels(target);
        var radiusX = brush.RadiusX.ToValue(target.Width);
        var radiusY = brush.RadiusY.ToValue(target.Height);
        var scale = Matrix.Identity;
        if (Math.Abs(radiusX - radiusY) > 1e-9 && radiusX > 0)
        {
            scale = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(1, radiusY / radiusX) * Matrix.CreateTranslation(center.X, center.Y);
            origin = origin.WithY(((origin.Y - center.Y) * radiusX / radiusY) + center.Y);
        }

        var focus = origin == center ? string.Empty : $" fx=\"{CssValues.Num(origin.X)}\" fy=\"{CssValues.Num(origin.Y)}\"";
        var transform = Transformed(brush, target, scale) * toUser;
        return $"<radialGradient id=\"\" gradientUnits=\"userSpaceOnUse\" cx=\"{CssValues.Num(center.X)}\" cy=\"{CssValues.Num(center.Y)}\" r=\"{CssValues.Num(radiusX)}\"{focus}{Spread(brush)}{Attribute(transform)}>{Stops(brush)}</radialGradient>";
    }

    private static Matrix Transformed(IGradientBrush brush, Rect target, Matrix inner)
    {
        if (brush.Transform is not { } transform)
        {
            return inner;
        }

        var origin = brush.TransformOrigin.ToPixels(target);
        var offset = Matrix.CreateTranslation(origin.X, origin.Y);
        return inner * (-offset * transform.Value * offset);
    }

    private static string Attribute(Matrix matrix) => matrix.IsIdentity
        ? string.Empty
        : string.Create(CultureInfo.InvariantCulture, $" gradientTransform=\"matrix({CssValues.Num(matrix.M11)} {CssValues.Num(matrix.M12)} {CssValues.Num(matrix.M21)} {CssValues.Num(matrix.M22)} {CssValues.Num(matrix.M31)} {CssValues.Num(matrix.M32)})\"");

    private static string Spread(IGradientBrush brush) => brush.SpreadMethod switch
    {
        GradientSpreadMethod.Reflect => " spreadMethod=\"reflect\"",
        GradientSpreadMethod.Repeat => " spreadMethod=\"repeat\"",
        _ => string.Empty,
    };

    private static string Stops(IGradientBrush brush)
    {
        var stops = new StringBuilder();
        foreach (var stop in brush.GradientStops)
        {
            var alpha = stop.Color.A / 255.0 * brush.Opacity;
            stops.Append(CultureInfo.InvariantCulture, $"<stop offset=\"{CssValues.Num(Math.Clamp(stop.Offset, 0, 1))}\" stop-color=\"{CssValues.Color(Color.FromRgb(stop.Color.R, stop.Color.G, stop.Color.B))}\"");
            stops.Append(alpha < 1 ? string.Create(CultureInfo.InvariantCulture, $" stop-opacity=\"{CssValues.Num(alpha)}\"") : string.Empty);
            stops.Append("/>");
        }

        return stops.ToString();
    }
}
