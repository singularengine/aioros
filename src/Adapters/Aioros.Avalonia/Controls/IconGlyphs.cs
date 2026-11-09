using System.Collections.Immutable;
using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;

namespace Aioros.Avalonia.Controls;

internal static class IconGlyphs
{
    private static ImmutableDictionary<string, ImmutableArray<IconStroke>> _glyphs = ImmutableDictionary<string, ImmutableArray<IconStroke>>.Empty;

    /// <summary>The strokes of an icon's markup as the icon set holds it now, parsed once per markup, so a kind registered or registered again later draws its newest markup.</summary>
    public static ImmutableArray<IconStroke> Of(string? kind) =>
        kind is null ? [] : ImmutableInterlocked.GetOrAdd(ref _glyphs, Runtime.Slots.IconSet.Markup(kind), Parse);

    private static ImmutableArray<IconStroke> Parse(string markup)
    {
        if (markup.Length == 0)
        {
            return [];
        }

        var root = XElement.Parse("<svg>" + markup + "</svg>");
        return [.. root.Elements().Select(Stroke).OfType<IconStroke>()];
    }

    private static IconStroke? Stroke(XElement element)
    {
        var geometry = element.Name.LocalName switch
        {
            "path" => Geometry.Parse(Text(element, "d")),
            "circle" => new EllipseGeometry(Circle(element)),
            "rect" => new RectangleGeometry(Box(element), Number(element, "rx"), Number(element, "rx")),
            "line" => Line(element),
            "polyline" => Points(element, closed: false),
            "polygon" => Points(element, closed: true),
            _ => null,
        };
        if (geometry is null)
        {
            return null;
        }

        var filled = Text(element, "fill") == "currentColor";
        var stroked = Text(element, "stroke") != "none";
        var opacity = element.Attribute("opacity") is { } value ? double.Parse(value.Value, CultureInfo.InvariantCulture) : 1;
        return new IconStroke(geometry, filled, stroked, opacity);
    }

    private static Rect Circle(XElement element)
    {
        var radius = Number(element, "r");
        return new Rect(Number(element, "cx") - radius, Number(element, "cy") - radius, radius * 2, radius * 2);
    }

    private static Rect Box(XElement element) =>
        new(Number(element, "x"), Number(element, "y"), Number(element, "width"), Number(element, "height"));

    private static StreamGeometry Line(XElement element)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(Number(element, "x1"), Number(element, "y1")), isFilled: false);
        context.LineTo(new Point(Number(element, "x2"), Number(element, "y2")));
        context.EndFigure(isClosed: false);
        return geometry;
    }

    private static StreamGeometry Points(XElement element, bool closed)
    {
        var values = Text(element, "points").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(values[0], values[1]), isFilled: closed);
        for (var index = 2; index + 1 < values.Length; index += 2)
        {
            context.LineTo(new Point(values[index], values[index + 1]));
        }

        context.EndFigure(closed);
        return geometry;
    }

    private static string Text(XElement element, string name) => element.Attribute(name)?.Value ?? string.Empty;

    private static double Number(XElement element, string name) =>
        double.TryParse(Text(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
}

internal sealed record IconStroke(Geometry Geometry, bool Filled, bool Stroked, double Opacity);
