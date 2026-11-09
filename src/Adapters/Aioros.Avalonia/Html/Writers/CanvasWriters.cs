using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Vectors;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A <see cref="Canvas"/> as one <c>&lt;svg&gt;</c> at the canvas size, without a view box, so user units are CSS pixels
/// as in Avalonia: each child is placed where the canvas arranges it (<c>Canvas.Left</c>/<c>Top</c>, else
/// <c>Right</c>/<c>Bottom</c>, else 0), in <c>ZIndex</c> order, with its opacity and render transform; a shape is the
/// element of its rendered geometry (its stretch and its size applied) and any other control is written in a
/// <c>&lt;foreignObject&gt;</c> at its arranged bounds, unless the canvas is part of a control template, where the control's
/// code places its children at run time (Ursa's <c>Marquee</c> moves its content on a timer) and no position is the page's.
/// </summary>
internal static class CanvasWriters
{
    public static void WriteCanvas(HtmlContext context, Canvas canvas, ParentKind parent)
    {
        var size = LayOut(canvas);
        var style = context.Box(canvas, parent);
        style.Append(canvas.ClipToBounds ? "display:block;" : "display:block;overflow:visible;");
        style.Append(CssValues.Paint(canvas.Background, size) is { } background ? $"background:{background};" : "pointer-events:none;");
        style.Append(double.IsNaN(canvas.Width) && canvas.HorizontalAlignment == global::Avalonia.Layout.HorizontalAlignment.Stretch ? "width:100%;" : string.Empty);
        context.Plan.Literal($"<svg width=\"{CssValues.Num(size.Width)}\" height=\"{CssValues.Num(size.Height)}\" class=\"{context.Classes(canvas, null, style)}\">");
        var paints = new SvgPaints();
        foreach (var child in canvas.Children.Where(child => child.IsVisible).OrderBy(child => child.ZIndex))
        {
            if (child is Shape shape)
            {
                WriteShape(context, shape, paints);
            }
            else if (canvas.TemplatedParent is null)
            {
                var bounds = child.Bounds;
                context.Plan.Literal($"<foreignObject x=\"{CssValues.Num(bounds.X)}\" y=\"{CssValues.Num(bounds.Y)}\" width=\"{CssValues.Num(bounds.Width)}\" height=\"{CssValues.Num(bounds.Height)}\" overflow=\"visible\">");
                context.Write(child, ParentKind.Single);
                context.Plan.Literal("</foreignObject>");
            }
        }

        context.Plan.Literal($"{paints.Defs}</svg>");
    }

    private static Size LayOut(Canvas canvas)
    {
        var size = new Size(double.IsNaN(canvas.Width) ? 0 : canvas.Width, double.IsNaN(canvas.Height) ? 0 : canvas.Height);
        canvas.Measure(size);
        canvas.Arrange(new Rect(size));
        return size;
    }

    private static void WriteShape(HtmlContext context, Shape shape, SvgPaints paints)
    {
        var pointed = shape is Polyline or Polygon && shape.Stretch == Stretch.None;
        ShapeWriters.FailWhenBound(context, shape, pointsFollow: pointed);
        var placement = Placement(shape);
        if (pointed)
        {
            var (tag, points, rule) = shape is Polyline polyline ? ("polyline", Polyline.PointsProperty, polyline.FillRule) : ("polygon", Polygon.PointsProperty, ((Polygon)shape).FillRule);
            context.Plan.Literal($"<{tag}{placement} points=\"");
            context.Plan.Value(shape, points, ValueFormat.Points);
            context.Plan.Literal($"\"{ShapePaint.Attributes(shape, paints, shape.DefiningGeometry?.Bounds ?? default, Matrix.Identity, rule == FillRule.EvenOdd)}/>");
            return;
        }

        if (ShapeElements.Of(shape.RenderedGeometry, shape.Fill is not null) is not { } element)
        {
            return;
        }

        context.Plan.Literal(ShapePaint.Markup(shape, element, paints, placement, element.Bounds, Matrix.Identity));
    }

    private static string Placement(Shape shape)
    {
        var bounds = shape.Bounds;
        var opacity = shape.Opacity < 1 ? string.Create(CultureInfo.InvariantCulture, $" opacity=\"{CssValues.Num(shape.Opacity)}\"") : string.Empty;
        if (shape.RenderTransform is not { } transform || transform.Value.IsIdentity)
        {
            return bounds.Position == default
                ? opacity
                : string.Create(CultureInfo.InvariantCulture, $" transform=\"translate({CssValues.Num(bounds.X)} {CssValues.Num(bounds.Y)})\"{opacity}");
        }

        var origin = shape.RenderTransformOrigin.ToPixels(bounds.Size);
        var offset = Matrix.CreateTranslation(origin.X, origin.Y);
        var matrix = -offset * transform.Value * offset * Matrix.CreateTranslation(bounds.X, bounds.Y);
        return string.Create(CultureInfo.InvariantCulture, $" transform=\"matrix({CssValues.Num(matrix.M11)} {CssValues.Num(matrix.M12)} {CssValues.Num(matrix.M21)} {CssValues.Num(matrix.M22)} {CssValues.Num(matrix.M31)} {CssValues.Num(matrix.M32)})\"{opacity}");
    }
}
