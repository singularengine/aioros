using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Looks;
using OxyPlot;

namespace Aioros.Avalonia.Charts;

internal sealed class SvgRenderContext(string prefix, double width = 0, double height = 0, bool stretch = false) : RenderContextBase
{
    private readonly StringBuilder _svg = new();
    private readonly List<Label> _labels = [];
    private int _clips;

    private int _open;

    public string Body => Compose(new OxyRect(0, 0, width, height));

    public string Compose(OxyRect plot)
    {
        if (!stretch)
        {
            var inside = new StringBuilder();
            _labels.ForEach(label => SvgText(inside, label));
            return string.Create(CultureInfo.InvariantCulture, $"<svg viewBox=\"0 0 {N(width)} {N(height)}\" width=\"100%\" height=\"100%\">{_svg}{inside}</svg>");
        }

        var left = plot.Left;
        var right = width - plot.Right;
        var html = new StringBuilder();
        html.Append(CultureInfo.InvariantCulture, $"<svg viewBox=\"{N(left)} 0 {N(plot.Width)} {N(height)}\" preserveAspectRatio=\"none\" overflow=\"visible\" style=\"position:absolute;top:0;left:{N(left)}px;width:calc(100% - {N(left + right)}px);height:{N(height)}px\">{_svg}</svg>");
        foreach (var label in _labels)
        {
            Span(html, label, plot, right);
        }

        return html.ToString();
    }

    private void Span(StringBuilder html, Label label, OxyRect plot, double right)
    {
        var x = label.Point.X;
        var position = x <= plot.Left + 0.5
            ? $"{N(x)}px"
            : x >= plot.Right - 0.5
                ? $"calc(100% - {N(width - x)}px)"
                : $"calc({N(plot.Left)}px + (100% - {N(plot.Left + right)}px) * {N((x - plot.Left) / plot.Width)})";
        var top = label.Valign switch
        {
            VerticalAlignment.Middle => label.Point.Y - (InterMetrics.Height(label.Size) / 2),
            VerticalAlignment.Bottom => label.Point.Y - InterMetrics.Height(label.Size),
            _ => label.Point.Y,
        };
        var align = label.Halign switch { HorizontalAlignment.Center => " cc", HorizontalAlignment.Right => " cr", _ => string.Empty };
        html.Append(CultureInfo.InvariantCulture, $"<span class=\"cl{align}\" style=\"left:{position};top:{N(top)}px;font-size:{N(label.Size)}px;color:{Hex(label.Color)}");
        html.Append(label.Weight >= 600 ? ";font-weight:600" : string.Empty);
        html.Append(label.Rotate == 0 ? string.Empty : $";transform-origin:0 0;rotate:{N(label.Rotate)}deg");
        html.Append("\">").Append(CssValues.Encode(label.Text)).Append("</span>");
    }

    private static void SvgText(StringBuilder svg, Label label)
    {
        var anchor = label.Halign switch { HorizontalAlignment.Center => "middle", HorizontalAlignment.Right => "end", _ => "start" };
        svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{N(label.Point.X)}\" y=\"{N(label.Point.Y + ChartText.Baseline(0, label.Size, label.Valign))}\" font-size=\"{N(label.Size)}\"");
        svg.Append(label.Weight >= 600 ? " font-weight=\"600\"" : string.Empty).Append(" text-anchor=\"").Append(anchor).Append('"');
        svg.Append(label.Rotate == 0 ? string.Empty : $" transform=\"rotate({N(label.Rotate)} {N(label.Point.X)} {N(label.Point.Y)})\"");
        Fill(svg, label.Color);
        svg.Append('>').Append(CssValues.Encode(label.Text)).Append("</text>");
    }

    public override void DrawLine(IList<ScreenPoint> points, OxyColor stroke, double thickness, EdgeRenderingMode edgeRenderingMode, double[]? dashArray, LineJoin lineJoin)
    {
        if (points is not { Count: > 1 } || !stroke.IsVisible() || thickness <= 0)
        {
            return;
        }

        _svg.Append("<polyline points=\"").Append(Points(points)).Append("\" fill=\"none\"");
        Stroke(stroke, thickness, dashArray, lineJoin);
        _svg.Append("/>");
    }

    public override void DrawPolygon(IList<ScreenPoint> points, OxyColor fill, OxyColor stroke, double thickness, EdgeRenderingMode edgeRenderingMode, double[]? dashArray, LineJoin lineJoin)
    {
        if (points is not { Count: > 2 })
        {
            return;
        }

        _svg.Append("<polygon points=\"").Append(Points(points)).Append('"');
        Fill(fill);
        Stroke(stroke, thickness, dashArray, lineJoin);
        _svg.Append("/>");
    }

    public override void DrawRectangle(OxyRect rect, OxyColor fill, OxyColor stroke, double thickness, EdgeRenderingMode edgeRenderingMode)
    {
        _svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(rect.Left)}\" y=\"{N(rect.Top)}\" width=\"{N(rect.Width)}\" height=\"{N(rect.Height)}\"");
        Fill(fill);
        Stroke(stroke, thickness, null, LineJoin.Miter);
        _svg.Append("/>");
    }

    public override void DrawEllipse(OxyRect rect, OxyColor fill, OxyColor stroke, double thickness, EdgeRenderingMode edgeRenderingMode)
    {
        _svg.Append(CultureInfo.InvariantCulture, $"<ellipse cx=\"{N(rect.Center.X)}\" cy=\"{N(rect.Center.Y)}\" rx=\"{N(rect.Width / 2)}\" ry=\"{N(rect.Height / 2)}\"");
        Fill(fill);
        Stroke(stroke, thickness, null, LineJoin.Miter);
        _svg.Append("/>");
    }

    public override void DrawText(ScreenPoint p, string text, OxyColor fill, string fontFamily, double fontSize, double fontWeight, double rotate, HorizontalAlignment halign, VerticalAlignment valign, OxySize? maxSize)
    {
        if (!string.IsNullOrEmpty(text) && fill.IsVisible())
        {
            _labels.Add(new Label(p, text, fill, fontSize, fontWeight, rotate, halign, valign));
        }
    }

    public override int ClipCount => _open;

    public override OxySize MeasureText(string text, string fontFamily, double fontSize, double fontWeight) =>
        ChartText.Measure(text, fontSize, fontWeight);

    public override void PushClip(OxyRect clippingRectangle)
    {
        var id = prefix + "-" + (++_clips).ToString(CultureInfo.InvariantCulture);
        _open++;
        _svg.Append(CultureInfo.InvariantCulture, $"<clipPath id=\"{id}\"><rect x=\"{N(clippingRectangle.Left)}\" y=\"{N(clippingRectangle.Top)}\" width=\"{N(clippingRectangle.Width)}\" height=\"{N(clippingRectangle.Height)}\"/></clipPath><g clip-path=\"url(#{id})\">");
    }

    public override void PopClip()
    {
        _open--;
        _svg.Append("</g>");
    }

    private void Fill(OxyColor fill) => Fill(_svg, fill);

    private static void Fill(StringBuilder svg, OxyColor fill)
    {
        if (!fill.IsVisible())
        {
            svg.Append(" fill=\"none\"");
            return;
        }

        svg.Append(" fill=\"").Append(Hex(fill)).Append('"');
        svg.Append(fill.A < 255 ? $" fill-opacity=\"{N(fill.A / 255d)}\"" : string.Empty);
    }

    private void Stroke(OxyColor stroke, double thickness, double[]? dashArray, LineJoin lineJoin)
    {
        if (!stroke.IsVisible() || thickness <= 0)
        {
            return;
        }

        _svg.Append(" stroke=\"").Append(Hex(stroke)).Append('"');
        _svg.Append(stroke.A < 255 ? $" stroke-opacity=\"{N(stroke.A / 255d)}\"" : string.Empty);
        _svg.Append(CultureInfo.InvariantCulture, $" stroke-width=\"{N(thickness)}\"");
        _svg.Append(dashArray is { Length: > 0 } ? $" stroke-dasharray=\"{string.Join(',', dashArray.Select(dash => N(dash * thickness)))}\"" : string.Empty);
        _svg.Append(lineJoin == LineJoin.Round ? " stroke-linejoin=\"round\"" : string.Empty);
        _svg.Append(stretch ? " vector-effect=\"non-scaling-stroke\"" : string.Empty);
    }

    private static string Points(IList<ScreenPoint> points) => string.Join(' ', points.Select(point => N(point.X) + "," + N(point.Y)));

    private static string Hex(OxyColor color) => string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}");

    private static string N(double value) => CssValues.Num(Math.Round(value, 2));

    private sealed record Label(ScreenPoint Point, string Text, OxyColor Color, double Size, double Weight, double Rotate, HorizontalAlignment Halign, VerticalAlignment Valign);
}
