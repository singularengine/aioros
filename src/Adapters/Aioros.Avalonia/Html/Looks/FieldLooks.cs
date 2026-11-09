using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

internal readonly record struct Frame(IBrush? Background, IBrush? Border, Thickness Thickness, CornerRadius Radius, Thickness Padding);

/// <summary>
/// The look of a text field as its control theme draws it: its border part's fill (transparent when the theme's is, so
/// the browser's own field background never shows), border, radius and box shadow at rest, and in each combination of
/// pointed at, focused and invalid (<c>aria-invalid</c>, the theme's <c>:error</c> look), resolved with the part's
/// transitions held and its transitions written as CSS ones.
/// </summary>
internal static class FieldLooks
{
    public static (string Classes, StringBuilder Style) Classes(HtmlContext context, Control owner, ParentKind parent, TextBox field, Frame? frame, string? baseClass)
    {
        ThemeLooks.ExpandTemplates(field);
        var part = Part<Border>(field, "PART_BorderElement");
        var states = States(owner, ":focus", ":focus");
        (IBrush? Background, IBrush? Border)? rest = null;
        var look = part is null ? null : StateLooks.Resolve(field, owner.IsEffectivelyEnabled ? states : [], () =>
        {
            rest ??= (part.Background, part.BorderBrush);
            return Snapshot(part, field.Foreground);
        });
        var normal = frame ?? (part is null
            ? new Frame(field.Background, field.BorderBrush, field.BorderThickness, field.CornerRadius, field.Padding)
            : new Frame(rest?.Background ?? part.Background, rest?.Border ?? part.BorderBrush, part.BorderThickness, part.CornerRadius, field.Padding));
        normal = frame is { } own && CssValues.Paint(own.Background) is null && part is not null ? own with { Background = rest?.Background ?? part.Background } : normal;
        var style = context.Box(owner, parent);
        BoxStyles.Font(style, field.FontSize, field.FontWeight, field.Foreground);
        BoxStyles.Fill(style, normal.Background, normal.Border, normal.Thickness, normal.Radius, normal.Padding);
        style.Append(Rest(look, frame, normal));
        style.Append(part?.Transitions is { Count: > 0 } transitions && MotionStyles.TransitionList(transitions) is { } moves ? $"transition:{moves};" : string.Empty);
        var minHeight = context.Wrapped.Contains(owner) ? context.MinHeight(owner) : Math.Max(owner.MinHeight, field.MinHeight);
        style.Append(minHeight > 0 ? $"min-height:{CssValues.Px(minHeight)};" : string.Empty);
        style.Append(field.TextAlignment switch
        {
            TextAlignment.Center => "text-align:center;",
            TextAlignment.Right => "text-align:right;",
            _ => string.Empty,
        });
        var rules = look is null ? string.Empty : context.Sheet.Rules(Rules(look, normal.Thickness));
        return (CssValues.Join(baseClass, CssValues.Join(rules, TextClass(context, field))), style);
    }

    /// <summary>The states of a field: pointed at, focused (<paramref name="focus"/>, as its theme names it), and invalid while it shows errors.</summary>
    public static List<WebState> States(Control owner, string focus, string selector)
    {
        List<WebState> states = [new(":pointerover", ":hover"), new(focus, selector)];
        if (Writers.OverlayWriters.Validates(owner))
        {
            states.Add(new(":error", "[aria-invalid=\"true\"]"));
        }

        return states;
    }

    /// <summary>What the rest look adds to the frame: a transparent fill written as such, and the part's box shadow.</summary>
    public static string Rest(StateLook? look, Frame? frame, Frame normal)
    {
        var css = new StringBuilder();
        css.Append(CssValues.Paint(normal.Background) is null ? "background:transparent;" : string.Empty);
        css.Append(frame is null && look?.Rest.GetValueOrDefault("box-shadow") is { } shadow and not "none" ? $"box-shadow:{shadow};" : string.Empty);
        return css.ToString();
    }

    public static Dictionary<string, string> Snapshot(Border part, IBrush? foreground) => new(StringComparer.Ordinal)
    {
        ["background"] = CssValues.Paint(part.Background) ?? "transparent",
        ["border-color"] = CssValues.Brush(part.BorderBrush) ?? "transparent",
        ["border-width"] = BoxStyles.Edges(part.BorderThickness),
        ["box-shadow"] = part.BoxShadow.Count == 0 ? "none" : BoxStyles.Shadow(part.BoxShadow),
        ["color"] = CssValues.Brush(foreground) ?? "inherit",
    };

    /// <summary>
    /// The state rules: a state that thickens the border (Fluent's focused field) draws the extra width as an inset shadow
    /// in the border's color, under the theme's own shadow, so the text does not move.
    /// </summary>
    public static IEnumerable<(string Selector, string Rule)> Rules(StateLook look, Thickness normal) => look.Rules.Select(rule =>
    {
        var css = new StringBuilder();
        var values = rule.Rule.ToDictionary(StringComparer.Ordinal);
        var shadows = new List<string>();
        if (values.Remove("border-width", out var width) && Inset(Parse(width), normal) is { } inset)
        {
            var color = values.GetValueOrDefault("border-color") ?? look.Rest["border-color"];
            shadows.AddRange(inset.Select(edge => $"inset {edge} {color}"));
        }

        if (values.Remove("box-shadow", out var shadow) && shadow != "none")
        {
            shadows.Add(shadow);
        }
        else if (shadows.Count > 0 && look.Rest["box-shadow"] != "none")
        {
            shadows.Add(look.Rest["box-shadow"]);
        }

        css.Append(string.Concat(values.Select(entry => $"{entry.Key}:{entry.Value};")));
        css.Append(shadows.Count > 0 ? $"box-shadow:{string.Join(',', shadows)};" : shadow == "none" ? "box-shadow:none;" : string.Empty);
        return (rule.Selector, css.ToString());
    });

    private static Thickness Parse(string edges)
    {
        var parts = edges.Split(' ').Select(edge => double.Parse(edge.AsSpan(0, edge.Length - 2), CultureInfo.InvariantCulture)).ToArray();
        return new Thickness(parts[3], parts[0], parts[1], parts[2]);
    }

    private static List<string>? Inset(Thickness state, Thickness normal)
    {
        var (top, right, bottom, left) = (state.Top - normal.Top, state.Right - normal.Right, state.Bottom - normal.Bottom, state.Left - normal.Left);
        if (top <= 0 && right <= 0 && bottom <= 0 && left <= 0)
        {
            return null;
        }

        if (new Thickness(left, top, right, bottom).IsUniform)
        {
            return [$"0 0 0 {CssValues.Px(top)}"];
        }

        List<string> edges = [];
        edges.AddRange(top > 0 ? [$"0 {CssValues.Px(top)} 0 0"] : []);
        edges.AddRange(right > 0 ? [$"-{CssValues.Px(right)} 0 0 0"] : []);
        edges.AddRange(bottom > 0 ? [$"0 -{CssValues.Px(bottom)} 0 0"] : []);
        edges.AddRange(left > 0 ? [$"{CssValues.Px(left)} 0 0 0"] : []);
        return edges;
    }

    private static string TextClass(HtmlContext context, TextBox field)
    {
        var rules = new StringBuilder();
        if (field.PlaceholderForeground is ISolidColorBrush placeholder)
        {
            var color = placeholder.Color;
            var alpha = color.A / 255.0 * placeholder.Opacity * (Part<TextBlock>(field, "PART_Placeholder")?.Opacity ?? 1);
            rules.Append(CultureInfo.InvariantCulture, $"::placeholder{{color:rgba({color.R},{color.G},{color.B},{CssValues.Num(alpha)})}}");
        }

        if (CssValues.Brush(field.SelectionBrush) is { } selection)
        {
            var foreground = CssValues.Brush(field.SelectionForegroundBrush) is { } text ? $";color:{text}" : string.Empty;
            rules.Append(CultureInfo.InvariantCulture, $"::selection{{background:{selection}{foreground}}}");
        }

        if (rules.Length == 0)
        {
            return string.Empty;
        }

        var css = rules.ToString();
        var name = "ph" + CssValues.Hash(css);
        context.Sheet.AddRule(name, $".{name}{css.Replace("}::", $"}}.{name}::", StringComparison.Ordinal)}\n");
        return name;
    }

    public static T? Part<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(descendant => descendant.Name == name);
}
