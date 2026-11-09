using Avalonia;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// Gradient brushes as CSS gradients on a box of the given size, placed as Avalonia's Skia renderer places them: a
/// linear gradient's line runs from its start to its end point in pixels on the box (relative points on a unit square
/// when the size is unknown), a reflected spread repeats its stops mirrored over twice the length.
/// </summary>
internal static class CssGradients
{
    public static string? Of(IBrush? brush, Size size = default) => brush switch
    {
        ILinearGradientBrush linear when linear.GradientStops.Count > 0 => Linear(linear, size),
        IRadialGradientBrush radial when radial.GradientStops.Count > 0 => Radial(radial),
        IConicGradientBrush conic when conic.GradientStops.Count > 0 => Conic(conic, Position(conic.Center)),
        _ => null,
    };

    /// <summary>A conic gradient around <paramref name="center"/>, a CSS position.</summary>
    public static string Conic(IConicGradientBrush brush, string center) =>
        $"conic-gradient(from {CssValues.Num(brush.Angle)}deg at {center},{Stops(brush, brush.GradientStops.Select(stop => (stop.Offset, stop.Color)), offset => offset * 100)})";

    private static string Linear(ILinearGradientBrush brush, Size size)
    {
        var known = size.Width > 0 && size.Height > 0;
        var box = known ? new Rect(size) : new Rect(0, 0, 1, 1);
        var start = brush.StartPoint.ToPixels(box);
        var end = brush.EndPoint.ToPixels(box);
        var (dx, dy) = (end.X - start.X, end.Y - start.Y);
        var angle = Math.Atan2(dx, -dy);
        var (sin, cos) = Math.SinCos(angle);
        var length = Math.Abs(box.Width * sin) + Math.Abs(box.Height * cos);
        var placed = known || brush.StartPoint.Unit == RelativeUnit.Relative;
        var origin = placed ? ((((start.X - box.Center.X) * sin) - ((start.Y - box.Center.Y) * cos)) / length) + 0.5 : 0;
        var scale = placed ? Math.Sqrt((dx * dx) + (dy * dy)) / length : 1;
        var degrees = (angle * 180 / Math.PI + 360) % 360;
        var spread = brush.SpreadMethod == GradientSpreadMethod.Pad ? string.Empty : "repeating-";
        return $"{spread}linear-gradient({CssValues.Num(degrees)}deg,{Stops(brush, Spread(brush), offset => (origin + (offset * scale)) * 100)})";
    }

    private static string Radial(IRadialGradientBrush brush)
    {
        var spread = brush.SpreadMethod == GradientSpreadMethod.Pad ? string.Empty : "repeating-";
        return $"{spread}radial-gradient(ellipse {Length(brush.RadiusX)} {Length(brush.RadiusY)} at {Position(brush.Center)},{Stops(brush, Spread(brush), offset => offset * 100)})";
    }

    private static IEnumerable<(double Offset, Color Color)> Spread(IGradientBrush brush)
    {
        var stops = brush.GradientStops.Select(stop => (Offset: Math.Clamp(stop.Offset, 0, 1), stop.Color)).OrderBy(stop => stop.Offset).ToList();
        if (brush.SpreadMethod == GradientSpreadMethod.Pad)
        {
            return stops;
        }

        if (stops[0].Offset > 0)
        {
            stops.Insert(0, (0, stops[0].Color));
        }

        if (stops[^1].Offset < 1)
        {
            stops.Add((1, stops[^1].Color));
        }

        return brush.SpreadMethod == GradientSpreadMethod.Reflect
            ? stops.Concat(stops.AsEnumerable().Reverse().Skip(1).Select(stop => (2 - stop.Offset, stop.Color)))
            : stops;
    }

    private static string Stops(IGradientBrush brush, IEnumerable<(double Offset, Color Color)> stops, Func<double, double> percent) =>
        string.Join(',', stops.Select(stop =>
            $"{CssValues.Color(Color.FromArgb((byte)Math.Round(stop.Color.A * brush.Opacity), stop.Color.R, stop.Color.G, stop.Color.B))} {CssValues.Num(percent(stop.Offset))}%"));

    private static string Position(RelativePoint point) => point.Unit == RelativeUnit.Relative
        ? $"{CssValues.Num(point.Point.X * 100)}% {CssValues.Num(point.Point.Y * 100)}%"
        : $"{CssValues.Px(point.Point.X)} {CssValues.Px(point.Point.Y)}";

    private static string Length(RelativeScalar scalar) => scalar.Unit == RelativeUnit.Relative
        ? CssValues.Num(scalar.Scalar * 100) + "%"
        : CssValues.Px(scalar.Scalar);
}
