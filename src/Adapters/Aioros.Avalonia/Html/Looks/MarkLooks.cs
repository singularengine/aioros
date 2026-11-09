using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Vectors;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// The mark of a check box, a radio button or a switch as its control theme draws it, for a native input drawn with
/// <c>appearance:none</c>: its box (the theme's sized border: size, background, border, radius, box shadow), what is drawn
/// over it (a check glyph, a dot, a switch's knob) as an SVG background from the template's own shapes, and where the
/// content sits beside or above it, in every combination of the control's states.
/// </summary>
internal sealed record MarkLook(Size Size, StateLook States, string? Transition, double Gap, bool Above, Thickness Margin, double LineHeight, string? Color, bool NoOutline, double Height)
{
    /// <summary>The content's color in each combination of the control's states where a style changes it (<c>CheckBox:checked</c> setting <c>Foreground</c>, which the content inherits).</summary>
    public StateLook? Colors { get; init; }
}

internal static class MarkLooks
{
    private const string Knobs = "PART_MovingKnobs";

    private static ImmutableDictionary<string, MarkLook?> _looks = ImmutableDictionary<string, MarkLook?>.Empty.WithComparers(StringComparer.Ordinal);

    public static void Clear() => _looks = _looks.Clear();

    /// <summary>
    /// The mark of a control as its theme draws it: from the whole template, or only from the part named
    /// <paramref name="mark"/> and what it holds (a choice card's indicator, beside the card's other sized parts).
    /// </summary>
    public static MarkLook? Of(TemplatedControl control, IReadOnlyList<WebState> states, string? mark = null)
    {
        var content = control is ContentControl { Content: not null };
        var key = $"{control.GetType().FullName}.{string.Join('.', control.Classes.Where(name => !name.StartsWith(':')))}.{control.IsEffectivelyEnabled}.{control.ActualThemeVariant}.{StyleContext.Own(control)}.{content}.{string.Join(',', states.Select(state => state.Pseudo))}.{mark}"
            + $".{BoxStyles.Edges(control.Padding)}|{BoxStyles.Edges(control.BorderThickness)}|{BoxStyles.Corners(control.CornerRadius)}|{CssValues.Paint(control.Background)}|{CssValues.Brush(control.BorderBrush)}|{CssValues.Brush(control.Foreground)}|{Measured(control)}|{StyleContext.Of(control)}";
        return ImmutableInterlocked.GetOrAdd(ref _looks, key, _ => Resolve(control, states, mark));
    }

    /// <summary>
    /// What the control's height is measured from, since a look reads where the mark stands in it: its own size and size
    /// limits, and its text where it can wrap within a width or holds a line break.
    /// </summary>
    private static string Measured(TemplatedControl control)
    {
        var sizes = string.Join(',', new[] { control.Width, control.MinWidth, control.MaxWidth, control.Height, control.MinHeight, control.MaxHeight }.Select(size => size.ToString(CultureInfo.InvariantCulture)));
        var bounded = !double.IsNaN(control.Width) || !double.IsPositiveInfinity(control.MaxWidth);
        var text = control is ContentControl { Content: string content } && (bounded || content.Contains('\n', StringComparison.Ordinal)) ? content : string.Empty;
        return $"{sizes}|{text}";
    }

    private static MarkLook? Resolve(TemplatedControl control, IReadOnlyList<WebState> states, string? mark)
    {
        ThemeLooks.ExpandTemplates(control);
        StateLooks.LayOut(control);
        var elements = Elements(control, mark);
        var placed = elements.Select(element => Placed(element, control)).Where(rect => rect.Width > 0 && rect.Height > 0).ToList();
        if (placed.Count == 0)
        {
            return null;
        }

        var rect = placed.Aggregate((union, next) => union.Union(next));
        var presenter = control.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault(part => part.Name == "PART_ContentPresenter");
        var (gap, above, margin) = Placement(control, presenter, rect);
        var look = StateLooks.Resolve(control, states, () => Snapshot(control, rect, mark));
        var transition = control is ToggleSwitch { KnobTransitions: { } moves } ? KnobTransition(moves) : null;
        var line = presenter?.GetValue(TextBlock.LineHeightProperty) is { } height and > 0 ? height : 0;
        var color = presenter is not null && presenter.IsSet(ContentPresenter.ForegroundProperty) ? CssValues.Brush(presenter.Foreground) : null;
        var noOutline = control.IsSet(Control.FocusAdornerProperty) && control.FocusAdorner is null;
        var colors = presenter is null || !presenter.IsSet(ContentPresenter.ForegroundProperty) ? null : StateLooks.Resolve(control, states, () => new Dictionary<string, string>(StringComparer.Ordinal) { ["color"] = CssValues.Brush(presenter.Foreground) ?? "inherit" });
        var rest = colors is { Rules.Count: > 0 } ? colors.Rest["color"] : color;
        return new MarkLook(rect.Size, look, transition, gap, above, margin, line, rest, noOutline, above ? 0 : (2 * rect.Top) + rect.Height) { Colors = colors };
    }

    private static string? KnobTransition(global::Avalonia.Animation.Transitions moves) =>
        moves.OfType<global::Avalonia.Animation.TransitionBase>().FirstOrDefault(move => move.Property == Canvas.LeftProperty) is { } move
            ? $"background-position {CssValues.Ms(move.Duration)} {CssValues.Ease(move.Easing)} {CssValues.Ms(move.Delay)}"
            : null;

    /// <summary>
    /// The parts that draw the mark: the template's borders of a fixed size and its shapes, outside its content
    /// presenters; only the part named <paramref name="mark"/> and what it holds when it is given.
    /// </summary>
    private static List<Control> Elements(TemplatedControl control, string? mark) =>
        [.. (mark is null ? control.GetVisualDescendants() : control.GetVisualDescendants().OfType<Control>().Where(part => part.Name == mark).Take(1).SelectMany(part => part.GetVisualDescendants().Prepend(part))).OfType<Control>()
            .Where(element => element is Shape || (element is Border border && !double.IsNaN(border.Width) && !double.IsNaN(border.Height)))
            .Where(element => !element.GetVisualAncestors().TakeWhile(ancestor => !ReferenceEquals(ancestor, control)).Any(ancestor => ancestor is ContentPresenter))];

    private static Rect Placed(Visual element, Visual control) =>
        element.TransformToVisual(control) is { } matrix ? new Rect(element.Bounds.Size).TransformToAABB(matrix) : default;

    private static (double Gap, bool Above, Thickness Margin) Placement(TemplatedControl control, ContentPresenter? presenter, Rect rect)
    {
        var height = control.DesiredSize.Height - control.Margin.Top - control.Margin.Bottom;
        if (presenter is null || !presenter.IsVisible || presenter.Content is null || Placed(presenter, control) is not { Width: > 0 } content)
        {
            return (0, false, new Thickness(0, Math.Max(0, rect.Top), 0, Math.Max(0, height - rect.Bottom)));
        }

        if (content.Bottom <= rect.Top + 0.5)
        {
            return (rect.Top - content.Bottom, true, new Thickness(0, 0, 0, Math.Max(0, height - rect.Bottom)));
        }

        return (Math.Max(0, content.Left - rect.Right), false, default);
    }

    private static double Opacity(Visual element, Visual control)
    {
        var opacity = 1.0;
        for (var current = element; current is not null && !ReferenceEquals(current, control); current = current.GetVisualParent())
        {
            opacity *= current.IsVisible ? current.Opacity : 0;
        }

        return opacity;
    }

    private static Dictionary<string, string> Snapshot(TemplatedControl control, Rect rect, string? mark)
    {
        StateLooks.LayOut(control);
        var elements = Elements(control, mark).Where(element => Opacity(element, control) > 0).ToList();
        var box = elements.OfType<Border>().LastOrDefault(border => Near(Placed(border, control), rect));
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background-color"] = CssValues.Brush(box?.Background) ?? "transparent",
            ["border-color"] = CssValues.Brush(box?.BorderBrush) ?? "transparent",
            ["border-width"] = box is null ? "0px" : BoxStyles.Edges(box.BorderThickness),
            ["border-radius"] = box is null ? "0px" : BoxStyles.Corners(box.CornerRadius),
            ["box-shadow"] = box is null || box.BoxShadow.Count == 0 ? "none" : BoxStyles.Shadow(box.BoxShadow),
        };
        var layers = Layers(control, rect, [.. elements.OfType<Shape>()]);
        values["background-image"] = layers.Count == 0 ? "none" : string.Join(',', layers.Select(layer => layer.Image));
        values["background-position"] = layers.Count == 0 ? "0px 0px" : string.Join(',', layers.Select(layer => layer.Position));
        values["background-size"] = layers.Count == 0 ? "auto" : string.Join(',', layers.Select(layer => layer.Size));
        return values;
    }

    /// <summary>
    /// What is drawn over the box, as background layers: a switch's knob on its own (placed where Avalonia's switch puts
    /// it, at its canvas's width when checked, so the move can transition), then the other shapes in the box's space.
    /// </summary>
    private static List<(string Image, string Position, string Size)> Layers(TemplatedControl control, Rect rect, List<Shape> shapes)
    {
        var knobs = control.GetVisualDescendants().OfType<Panel>().FirstOrDefault(panel => panel.Name == Knobs);
        List<Control> moving = knobs is null ? [] : [.. knobs.GetVisualDescendants().OfType<Control>().Where(part => part is Shape or Border && Opacity(part, control) > 0)];
        List<(string Image, string Position, string Size)> layers = [];
        if (knobs is not null && Knob(control, knobs, rect, moving) is { } knob)
        {
            layers.Add(knob);
        }

        var still = shapes.Where(shape => !moving.Contains(shape)).ToList();
        if (still.Count > 0 && DrawingSvg.Parts(still.Select(shape => ((Visual)shape, (shape.TransformToVisual(control) ?? Matrix.Identity) * Matrix.CreateTranslation(-rect.X, -rect.Y), Opacity(shape, control))), rect.Size) is { } glyph)
        {
            layers.Add((DrawingSvg.Url(glyph), "0px 0px", $"{CssValues.Px(rect.Width)} {CssValues.Px(rect.Height)}"));
        }

        return layers;
    }

    private static (string Image, string Position, string Size)? Knob(TemplatedControl control, Panel knobs, Rect rect, List<Control> moving)
    {
        if (moving.Count == 0 || knobs.TransformToVisual(control) is not { } toControl
            || DrawingSvg.Parts(moving.Select(shape => ((Visual)shape, shape.TransformToVisual(knobs) ?? Matrix.Identity, Opacity(shape, knobs))), knobs.Bounds.Size) is not { } knob)
        {
            return null;
        }

        var left = Canvas.GetLeft(knobs);
        var on = control.Classes.Contains(":checked") && knobs.GetVisualParent() is Visual canvas ? canvas.Bounds.Width : 0;
        var x = toControl.M31 - rect.X - (double.IsNaN(left) ? 0 : left) + on;
        return (DrawingSvg.Url(knob), $"{CssValues.Px(x)} {CssValues.Px(toControl.M32 - rect.Y)}", $"{CssValues.Px(knobs.Bounds.Width)} {CssValues.Px(knobs.Bounds.Height)}");
    }

    private static bool Near(Rect first, Rect second) =>
        Math.Abs(first.X - second.X) < 0.5 && Math.Abs(first.Y - second.Y) < 0.5 && Math.Abs(first.Width - second.Width) < 0.5 && Math.Abs(first.Height - second.Height) < 0.5;

    /// <summary>Whether the theme lays the control's content above its mark (Fluent's switch header) rather than beside it.</summary>
    public static bool ContentFirst(TemplatedControl control) => Of(control, States(control, ".tsr>input"))?.Above == true;

    /// <summary>
    /// The states of a check box, a radio button or a switch, on the label that holds the input drawing its mark: as the
    /// label's child, or anywhere inside it when <paramref name="direct"/> is false (a choice card's parts).
    /// </summary>
    public static IReadOnlyList<WebState> States(Control control, string input = "input", bool direct = true)
    {
        var held = (direct ? ">" : string.Empty) + input;
        if (!control.IsEffectivelyEnabled)
        {
            return [new(":checked", $":has({held}:checked)")];
        }

        List<WebState> states = [new(":pointerover", ":hover"), new(":pressed", ":active"), new(":checked", $":has({held}:checked)"), new(":focus-visible", $":has({held}:focus-visible)")];
        if (Writers.OverlayWriters.Validates(control))
        {
            states.Add(new(":error", $":has({held}[aria-invalid=\"true\"])"));
        }

        return states;
    }

    /// <summary>
    /// The class of the label that holds a mark's input: the input drawn as the theme draws the mark (rest and state rules
    /// targeting <paramref name="target"/>), and the label's own layout (the gap to the content, its line height and color)
    /// appended to <paramref name="style"/>; null when the theme draws no mark the web can follow.
    /// </summary>
    public static string? Class(HtmlContext context, TemplatedControl control, StringBuilder style, string target = ">input", string input = "input", IReadOnlyList<WebState>? states = null, string? mark = null, StateLook? own = null)
    {
        if (Of(control, states ?? States(control, input), mark) is not { } look)
        {
            return null;
        }

        var css = new StringBuilder();
        css.Append(CultureInfo.InvariantCulture, $".$${target}{{{InputCss(look)}}}");
        css.Append(look.States.Css("$$", target));
        css.Append(look.Colors?.Css("$$", string.Empty) ?? string.Empty);
        css.Append(own?.Css("$$", string.Empty) ?? string.Empty);
        css.Append(look.NoOutline || !control.IsEffectivelyEnabled ? string.Empty : $".$$:has(>{input}:focus-visible){target}{{outline:2px solid;outline-offset:1px}}");
        var name = "mk" + CssValues.Hash(css.ToString());
        context.Sheet.AddRule(name, css.ToString().Replace("$$", name, StringComparison.Ordinal) + "\n");
        style.Append(look.Above ? "display:inline-flex;flex-direction:column;align-items:flex-start;" : "display:flex;flex-direction:row;align-items:center;");
        style.Append(look.Gap > 0 ? $"gap:{CssValues.Px(look.Gap)};" : string.Empty);
        var height = Math.Max(context.MinHeight(control), look.Height);
        style.Append(height > 0 ? $"min-height:{CssValues.Px(height)};" : string.Empty);
        style.Append(look.LineHeight > 0 ? $"line-height:{CssValues.Px(look.LineHeight)};" : string.Empty);
        style.Append(look.Color is null ? string.Empty : $"color:{look.Color};");
        style.Append("cursor:pointer;user-select:none;");
        return name;
    }

    /// <summary>The CSS of the input that draws the mark: its size and its look at rest.</summary>
    public static string InputCss(MarkLook look) =>
        $"appearance:none;-webkit-appearance:none;display:inline-block;box-sizing:border-box;margin:{(look.Margin == default ? "0" : $"{CssValues.Px(look.Margin.Top)} 0 {CssValues.Px(look.Margin.Bottom)} 0")};flex:0 0 auto;width:{CssValues.Px(look.Size.Width)};height:{CssValues.Px(look.Size.Height)};border-style:solid;background-repeat:no-repeat;background-origin:border-box;cursor:inherit;"
        + string.Concat(look.States.Rest.Select(entry => $"{entry.Key}:{entry.Value};"))
        + (look.Transition is null ? string.Empty : $"transition:{look.Transition};")
        + (look.NoOutline ? "outline:none;" : string.Empty);
}
