using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Vectors;

/// <summary>
/// A self-drawn control's <see cref="Visual.Render"/> recorded through a <see cref="DrawingGroup"/>'s drawing context and
/// written as SVG: geometries with solid fills and strokes and text as glyph outlines, under the pushed transforms,
/// geometry clips and opacities, linear and radial gradient brushes as SVG gradients on Avalonia's targets. A drawing
/// that the recording context refuses (bitmaps, rounded clips, custom draw operations) or that SVG would not reproduce
/// exactly here (conic gradients, image brushes, opacity masks, effects) is not written, and the control stays a PNG.
/// </summary>
internal static class DrawingSvg
{
    public static string? Write(Control control, Size size, string attributes)
    {
        var drawing = new DrawingGroup();
        try
        {
            using (var context = drawing.Open())
            {
                control.Render(context);
            }
        }
        catch (Exception exception) when (exception is NotSupportedException or NotImplementedException)
        {
            return null;
        }

        var body = new StringBuilder();
        var clips = new StringBuilder();
        var paints = new SvgPaints();
        var written = Append(body, clips, paints, drawing);
        clips.Append(paints.Definitions);
        return written
            ? string.Create(CultureInfo.InvariantCulture, $"<svg width=\"{CssValues.Num(size.Width)}\" height=\"{CssValues.Num(size.Height)}\" viewBox=\"0 0 {CssValues.Num(size.Width)} {CssValues.Num(size.Height)}\"{attributes}>{(clips.Length > 0 ? $"<defs>{clips}</defs>" : string.Empty)}{body}</svg>")
            : null;
    }

    /// <summary>
    /// Visuals of a template (a check mark, a radio's dot, a switch's knob) drawn as one SVG of the given size, each
    /// under its transform into that box and its opacity, or null when one of them cannot be recorded as SVG.
    /// </summary>
    public static string? Parts(IEnumerable<(Visual Visual, Matrix Transform, double Opacity)> parts, Size size)
    {
        var drawing = new DrawingGroup();
        try
        {
            using var context = drawing.Open();
            foreach (var (visual, transform, opacity) in parts)
            {
                using (context.PushTransform(transform))
                using (context.PushOpacity(opacity))
                {
                    if (visual is Border border)
                    {
                        Box(context, border);
                    }
                    else
                    {
                        visual.Render(context);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is NotSupportedException or NotImplementedException)
        {
            return null;
        }

        var body = new StringBuilder();
        var clips = new StringBuilder();
        var paints = new SvgPaints();
        if (!Append(body, clips, paints, drawing))
        {
            return null;
        }

        clips.Append(paints.Definitions);
        return string.Create(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{CssValues.Num(size.Width)}\" height=\"{CssValues.Num(size.Height)}\" viewBox=\"0 0 {CssValues.Num(size.Width)} {CssValues.Num(size.Height)}\">{(clips.Length > 0 ? $"<defs>{clips}</defs>" : string.Empty)}{body}</svg>");
    }

    /// <summary>A border as the rounded box it draws (its fill and its border's pen), which its own render records without its radius.</summary>
    private static void Box(DrawingContext context, Border border)
    {
        var size = border.Bounds.Size;
        var thickness = border.BorderThickness.Top;
        var radius = Math.Min(border.CornerRadius.TopLeft, Math.Min(size.Width, size.Height) / 2);
        var pen = thickness > 0 && border.BorderBrush is { } stroke ? new Pen(stroke, thickness) : null;
        var inset = thickness / 2;
        context.DrawGeometry(border.Background, pen, new RectangleGeometry(new Rect(inset, inset, Math.Max(0, size.Width - thickness), Math.Max(0, size.Height - thickness)), Math.Max(0, radius - inset), Math.Max(0, radius - inset)));
    }

    /// <summary>An SVG as a CSS <c>url()</c> of a data URI, its markup escaped as a URL needs.</summary>
    public static string Url(string svg) =>
        "url(\"data:image/svg+xml," + svg.Replace("\"", "'", StringComparison.Ordinal).Replace("%", "%25", StringComparison.Ordinal).Replace("#", "%23", StringComparison.Ordinal)
            .Replace("<", "%3C", StringComparison.Ordinal).Replace(">", "%3E", StringComparison.Ordinal) + "\")";

    private static bool Append(StringBuilder svg, StringBuilder clips, SvgPaints paints, Drawing drawing) => drawing switch
    {
        DrawingGroup group => Group(svg, clips, paints, group),
        GeometryDrawing geometry => Geometry(svg, paints, geometry.Geometry, geometry.Brush, geometry.Pen),
        GlyphRunDrawing text => text.GlyphRun is null || Geometry(svg, paints, text.GlyphRun.BuildGeometry(), text.Foreground, null),
        _ => false,
    };

    private static bool Group(StringBuilder svg, StringBuilder clips, SvgPaints paints, DrawingGroup group)
    {
        if (group.OpacityMask is not null || group.Effect is not null)
        {
            return false;
        }

        var attributes = new StringBuilder();
        if (group.Transform is { } transform && !transform.Value.IsIdentity)
        {
            var matrix = transform.Value;
            attributes.Append(CultureInfo.InvariantCulture, $" transform=\"matrix({CssValues.Num(matrix.M11)} {CssValues.Num(matrix.M12)} {CssValues.Num(matrix.M21)} {CssValues.Num(matrix.M22)} {CssValues.Num(matrix.M31)} {CssValues.Num(matrix.M32)})\"");
        }

        if (group.Opacity < 1)
        {
            attributes.Append(CultureInfo.InvariantCulture, $" opacity=\"{CssValues.Num(group.Opacity)}\"");
        }

        if (group.ClipGeometry is { } clip)
        {
            if (SkiaGeometry.Path(clip) is not { } path)
            {
                return false;
            }

            var id = "dc" + CssValues.Hash(path.Data);
            clips.Append(CultureInfo.InvariantCulture, $"<clipPath id=\"{id}\"><path d=\"{path.Data}\"{(path.EvenOdd ? " clip-rule=\"evenodd\"" : string.Empty)}/></clipPath>");
            attributes.Append(CultureInfo.InvariantCulture, $" clip-path=\"url(#{id})\"");
        }

        var grouped = attributes.Length > 0;
        svg.Append(grouped ? $"<g{attributes}>" : string.Empty);
        if (!group.Children.All(child => Append(svg, clips, paints, child)))
        {
            return false;
        }

        svg.Append(grouped ? "</g>" : string.Empty);
        return true;
    }

    private static bool Geometry(StringBuilder svg, SvgPaints paints, Geometry? geometry, IBrush? brush, IPen? pen)
    {
        if ((brush is not null and not ISolidColorBrush && !SvgPaints.IsGradient(brush)) || (pen?.Brush is not null and not ISolidColorBrush && !SvgPaints.IsGradient(pen.Brush)))
        {
            return false;
        }

        var target = geometry?.Bounds ?? default;
        var fill = paints.Paint(brush, target, Matrix.Identity);
        var stroke = pen is { Thickness: > 0 } ? paints.Paint(pen.Brush, target.Inflate(pen.Thickness / 2), Matrix.Identity) : null;

        if (geometry is null || (fill is null && stroke is null))
        {
            return true;
        }

        if (SkiaGeometry.Path(geometry, fill is not null, placed: true) is not { } path)
        {
            return false;
        }

        svg.Append(CultureInfo.InvariantCulture, $"<path d=\"{path.Data}\" fill=\"{fill ?? "none"}\"{(fill is not null && path.EvenOdd ? " fill-rule=\"evenodd\"" : string.Empty)}");
        if (stroke is not null)
        {
            Stroke(svg, pen!, stroke);
        }

        svg.Append("/>");
        return true;
    }

    private static void Stroke(StringBuilder svg, IPen pen, string stroke)
    {
        svg.Append(CultureInfo.InvariantCulture, $" stroke=\"{stroke}\" stroke-width=\"{CssValues.Num(pen.Thickness)}\"");
        if (pen.DashStyle is { Dashes.Count: > 0 } dash)
        {
            svg.Append(CultureInfo.InvariantCulture, $" stroke-dasharray=\"{string.Join(' ', dash.Dashes.Select(length => CssValues.Num(length * pen.Thickness)))}\"");
            svg.Append(dash.Offset != 0 ? string.Create(CultureInfo.InvariantCulture, $" stroke-dashoffset=\"{CssValues.Num(dash.Offset * pen.Thickness)}\"") : string.Empty);
        }

        svg.Append(pen.LineCap switch
        {
            PenLineCap.Round => " stroke-linecap=\"round\"",
            PenLineCap.Square => " stroke-linecap=\"square\"",
            _ => string.Empty,
        });
        svg.Append(pen.LineJoin switch
        {
            PenLineJoin.Round => " stroke-linejoin=\"round\"",
            PenLineJoin.Bevel => " stroke-linejoin=\"bevel\"",
            _ => string.Create(CultureInfo.InvariantCulture, $" stroke-miterlimit=\"{CssValues.Num(pen.MiterLimit)}\""),
        });
    }
}
