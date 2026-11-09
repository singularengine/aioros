using System.Globalization;
using Avalonia;
using Avalonia.Media;
using OxyPlot;

namespace Aioros.Avalonia.Charts;

internal sealed class AvaloniaRenderContext(DrawingContext context) : RenderContextBase
{
    private readonly Stack<DrawingContext.PushedState> _clips = new();

    public static OxySize Measure(string text, string fontFamily, double fontSize, double fontWeight) =>
        ChartText.Measure(text, fontSize, fontWeight);

    public override void DrawLine(IList<ScreenPoint> points, OxyColor stroke, double thickness, EdgeRenderingMode edgeRenderingMode, double[]? dashArray, LineJoin lineJoin)
    {
        if (points is not { Count: > 1 } || !stroke.IsVisible() || thickness <= 0)
        {
            return;
        }

        context.DrawGeometry(null, Pen(stroke, thickness, dashArray, lineJoin), Geometry(points, closed: false));
    }

    public override void DrawPolygon(IList<ScreenPoint> points, OxyColor fill, OxyColor stroke, double thickness, EdgeRenderingMode edgeRenderingMode, double[]? dashArray, LineJoin lineJoin)
    {
        if (points is not { Count: > 2 })
        {
            return;
        }

        context.DrawGeometry(Brush(fill), stroke.IsVisible() && thickness > 0 ? Pen(stroke, thickness, dashArray, lineJoin) : null, Geometry(points, closed: true));
    }

    public override void DrawRectangle(OxyRect rect, OxyColor fill, OxyColor stroke, double thickness, EdgeRenderingMode edgeRenderingMode) =>
        context.DrawRectangle(Brush(fill), stroke.IsVisible() && thickness > 0 ? Pen(stroke, thickness, null, LineJoin.Miter) : null, Rect(rect));

    public override void DrawEllipse(OxyRect rect, OxyColor fill, OxyColor stroke, double thickness, EdgeRenderingMode edgeRenderingMode) =>
        context.DrawEllipse(Brush(fill), stroke.IsVisible() && thickness > 0 ? Pen(stroke, thickness, null, LineJoin.Miter) : null, Rect(rect));

    public override void DrawText(ScreenPoint p, string text, OxyColor fill, string fontFamily, double fontSize, double fontWeight, double rotate, HorizontalAlignment halign, VerticalAlignment valign, OxySize? maxSize)
    {
        if (string.IsNullOrEmpty(text) || !fill.IsVisible())
        {
            return;
        }

        var size = ChartText.Measure(text, fontSize, fontWeight);
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Inter"), weight: fontWeight >= 600 ? FontWeight.SemiBold : FontWeight.Normal),
            fontSize,
            Brush(fill));
        var origin = new Point(ChartText.Left(0, size.Width, halign), ChartText.Baseline(0, fontSize, valign) - formatted.Baseline);
        using (context.PushTransform(Matrix.CreateRotation(rotate * Math.PI / 180) * Matrix.CreateTranslation(p.X, p.Y)))
        {
            context.DrawText(formatted, origin);
        }
    }

    public override int ClipCount => _clips.Count;

    public override OxySize MeasureText(string text, string fontFamily, double fontSize, double fontWeight) =>
        ChartText.Measure(text, fontSize, fontWeight);

    public override void PushClip(OxyRect clippingRectangle)
    {
        _clips.Push(context.PushClip(Rect(clippingRectangle)));
    }

    public override void PopClip()
    {
        _clips.Pop().Dispose();
    }

    private static StreamGeometry Geometry(IList<ScreenPoint> points, bool closed)
    {
        var geometry = new StreamGeometry();
        using var draw = geometry.Open();
        draw.BeginFigure(new Point(points[0].X, points[0].Y), closed);
        for (var index = 1; index < points.Count; index++)
        {
            draw.LineTo(new Point(points[index].X, points[index].Y));
        }

        draw.EndFigure(closed);
        return geometry;
    }

    private static Pen Pen(OxyColor color, double thickness, double[]? dashArray, LineJoin lineJoin) =>
        new(Brush(color), thickness, dashArray is { Length: > 0 } ? new DashStyle(dashArray, 0) : null, lineJoin: lineJoin == LineJoin.Round ? PenLineJoin.Round : PenLineJoin.Miter);

    private static SolidColorBrush? Brush(OxyColor color) =>
        color.IsVisible() ? new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)) : null;

    private static Rect Rect(OxyRect rect) => new(rect.Left, rect.Top, rect.Width, rect.Height);
}
