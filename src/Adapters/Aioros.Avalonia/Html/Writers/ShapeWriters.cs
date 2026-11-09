using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Vectors;
using Aioros.Avalonia.Plans;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Layout;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace Aioros.Avalonia.Html.Writers;

internal static class ShapeWriters
{
    private static readonly AvaloniaProperty[] Geometric =
    [
        Path.DataProperty, Polyline.PointsProperty, Polygon.PointsProperty, Line.StartPointProperty, Line.EndPointProperty,
        Shape.FillProperty, Shape.StrokeProperty, Shape.StrokeThicknessProperty, Arc.StartAngleProperty, Arc.SweepAngleProperty,
        Sector.StartAngleProperty, Sector.SweepAngleProperty, PathIcon.DataProperty,
    ];

    public static void WriteGeometry(HtmlContext context, Shape shape, ParentKind parent)
    {
        FailWhenBound(context, shape);
        var laidOut = shape is Arc or Sector or Rectangle or Ellipse ? LayOut(shape) : (Rect?)null;
        if (ShapeElements.Of(shape is Path path ? path.Data : shape.DefiningGeometry, shape.Fill is not null) is not { } element)
        {
            return;
        }

        var bounds = element.Bounds;
        var stretched = shape.Stretch != Stretch.None && laidOut is null;
        var view = laidOut ?? (stretched ? bounds : new Rect(0, 0, Math.Max(bounds.Right, 0), Math.Max(bounds.Bottom, 0)));
        var width = double.IsNaN(shape.Width) ? view.Width : shape.Width;
        var height = double.IsNaN(shape.Height) ? view.Height : shape.Height;
        var style = context.Box(shape, parent, size: false);
        style.Append("flex-shrink:0;");
        style.Append(stretched && double.IsNaN(shape.Width) && shape.HorizontalAlignment == HorizontalAlignment.Stretch ? "width:100%;height:auto;" : string.Empty);
        var preserve = (stretched ? shape.Stretch : Stretch.None) switch
        {
            Stretch.Fill => " preserveAspectRatio=\"none\"",
            Stretch.Uniform => " preserveAspectRatio=\"xMinYMin meet\"",
            Stretch.UniformToFill => " preserveAspectRatio=\"xMinYMin slice\"",
            _ => string.Empty,
        };
        var (target, toUser) = stretched ? Rendered(shape, bounds) : (bounds, Matrix.Identity);
        var paints = new SvgPaints();
        context.Plan.Literal($"<svg width=\"{CssValues.Num(width)}\" height=\"{CssValues.Num(height)}\" viewBox=\"{ViewBox(view)}\"{preserve} overflow=\"visible\" class=\"{context.Classes(shape, null, style)}\">");
        context.Plan.Literal($"{ShapePaint.Markup(shape, element, paints, string.Empty, target, toUser, nonScaling: stretched)}{paints.Defs}</svg>");
    }

    public static void WriteBox(HtmlContext context, Shape shape, ParentKind parent)
    {
        if ((SvgPaints.IsGradient(shape.Fill) || SvgPaints.IsGradient(shape.Stroke) || shape.StrokeDashArray is { Count: > 0 }) && !double.IsNaN(shape.Width) && !double.IsNaN(shape.Height))
        {
            WriteGeometry(context, shape, parent);
            return;
        }

        FailWhenBound(context, shape);
        var style = context.Box(shape, parent);
        style.Append("flex-shrink:0;");
        var stroked = BoxPaint(style, shape);
        if (shape is Ellipse)
        {
            style.Append("border-radius:50%;");
        }
        else if (shape is Rectangle rectangle && (rectangle.RadiusX > 0 || rectangle.RadiusY > 0))
        {
            var half = stroked ? shape.StrokeThickness / 2 : 0;
            var (x, y) = (CssValues.Px(rectangle.RadiusX + half), CssValues.Px(rectangle.RadiusY + half));
            style.Append(x == y ? $"border-radius:{x};" : $"border-radius:{x}/{y};");
        }

        context.Plan.Literal("<div");
        context.Root(shape, null, style);
        context.Plan.Literal("></div>");
    }

    /// <summary>
    /// A box shape's fill as its background and its stroke as its border; a gradient stroke is the transparent border
    /// showing a gradient layer under the fill (or a border image without a fill). Whether it has a stroke.
    /// </summary>
    private static bool BoxPaint(StringBuilder style, Shape shape)
    {
        var size = BoxStyles.Sized(shape);
        var fill = CssValues.Paint(shape.Fill, size);
        var gradient = shape.StrokeThickness > 0 && CssValues.Brush(shape.Stroke) is null ? CssGradients.Of(shape.Stroke, size) : null;
        style.Append((BoxStyles.Layered(fill, gradient) ?? fill) is { } background ? $"background:{background};" : string.Empty);
        if ((shape.StrokeThickness > 0 ? CssValues.Brush(shape.Stroke) ?? (gradient is null ? null : "transparent") : null) is not { } stroke)
        {
            return false;
        }

        style.Append(CultureInfo.InvariantCulture, $"border:{CssValues.Px(shape.StrokeThickness)} {LineStyle(shape)} {stroke};");
        style.Append(gradient is not null && fill is null ? $"border-image:{gradient} 1;" : string.Empty);
        return true;
    }

    /// <summary>
    /// The CSS line style of a box's stroke: solid without dashes, dotted for dashes no longer than
    /// the stroke is thick, dashed otherwise; a sized box is drawn as SVG instead, with Avalonia's exact dashes.
    /// </summary>
    private static string LineStyle(Shape shape) => shape.StrokeDashArray switch
    {
        null or { Count: 0 } => "solid",
        [<= 1, ..] => "dotted",
        _ => "dashed",
    };

    public static void WritePathIcon(HtmlContext context, PathIcon icon, ParentKind parent)
    {
        FailWhenBound(context, icon);
        if (icon.Data is not { } geometry || SkiaGeometry.Path(geometry) is not { } data)
        {
            return;
        }

        var style = context.Box(icon, parent, size: false);
        style.Append("flex-shrink:0;");
        var local = icon.GetDiagnostic(PathIcon.ForegroundProperty).Priority < BindingPriority.Inherited;
        style.Append(local && CssValues.Brush(icon.Foreground) is { } color ? $"color:{color};" : string.Empty);
        var width = double.IsNaN(icon.Width) ? geometry.Bounds.Width : icon.Width;
        var height = double.IsNaN(icon.Height) ? geometry.Bounds.Height : icon.Height;
        context.Plan.Literal($"<svg width=\"{CssValues.Num(width)}\" height=\"{CssValues.Num(height)}\" viewBox=\"{ViewBox(geometry.Bounds)}\" fill=\"currentColor\" class=\"{context.Classes(icon, null, style)}\">");
        context.Plan.Literal($"<path d=\"{data.Data}\"{(data.EvenOdd ? " fill-rule=\"evenodd\"" : string.Empty)}/></svg>");
    }

    /// <summary>Fails the plan when a property that shapes the geometry or its paint reads the model; <paramref name="pointsFollow"/> lets bound points through, written as a slot.</summary>
    public static void FailWhenBound(HtmlContext context, Control control, bool pointsFollow = false)
    {
        if (context.Plan.Planning && Geometric.FirstOrDefault(property => (!pointsFollow || (property != Polyline.PointsProperty && property != Polygon.PointsProperty)) && context.Plan.ReadsModel(control, property)) is { } bound)
        {
            context.Plan.Fail($"{control.GetType().Name}.{bound.Name} is bound");
        }
    }

    private static Rect LayOut(Shape shape)
    {
        var size = new Size(double.IsNaN(shape.Width) ? 100 : shape.Width, double.IsNaN(shape.Height) ? 100 : shape.Height);
        shape.Measure(size);
        shape.Arrange(new Rect(size));
        return new Rect(size);
    }

    /// <summary>
    /// The rendered bounds of a stretched shape and the matrix from its rendered geometry back to its own, so its paints
    /// are placed on the rendered geometry as Avalonia places them while the view box does the stretch.
    /// </summary>
    private static (Rect Target, Matrix ToUser) Rendered(Shape shape, Rect bounds)
    {
        var size = BoxStyles.Sized(shape);
        if (size.Width <= 0 || size.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return (bounds, Matrix.Identity);
        }

        var (scaleX, scaleY) = (size.Width / bounds.Width, size.Height / bounds.Height);
        (scaleX, scaleY) = shape.Stretch switch
        {
            Stretch.Uniform => (Math.Min(scaleX, scaleY), Math.Min(scaleX, scaleY)),
            Stretch.UniformToFill => (Math.Max(scaleX, scaleY), Math.Max(scaleX, scaleY)),
            _ => (scaleX, scaleY),
        };
        return (new Rect(0, 0, bounds.Width * scaleX, bounds.Height * scaleY), new Matrix(1 / scaleX, 0, 0, 1 / scaleY, bounds.X, bounds.Y));
    }

    private static string ViewBox(Rect rect) =>
        $"{CssValues.Num(rect.X)} {CssValues.Num(rect.Y)} {CssValues.Num(rect.Width)} {CssValues.Num(rect.Height)}";
}
