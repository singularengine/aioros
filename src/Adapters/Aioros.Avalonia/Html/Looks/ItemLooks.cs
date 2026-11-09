using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

internal static class ItemLooks
{
    public const string Checked = ":has(>input:checked)";

    public const string Marked = ".sel";

    private const string Hover = ":pointerover";

    private const string Pressed = ":pressed";

    private const string Selected = ":selected";

    private const string Focused = ":focus-visible";

    private static readonly string[] Toggled = [Hover, Pressed, Selected, Focused];

    public static string Class(HtmlContext context, TemplatedControl item, Control part, Control text, string selected, bool hoverOnPart = false)
    {
        string[][] states = [[], [Hover], [Hover, Pressed], [Selected], [Selected, Hover], [Selected, Hover, Pressed], [Focused], [Selected, Focused]];
        var looks = states.Select(state => Paint(item, part, text, state, hoverOnPart)).ToList();
        var shadowed = looks.Any(look => look.ContainsKey("box-shadow"));
        var css = looks.Select(look => string.Concat(look.Where(entry => entry.Key != "box-shadow" || shadowed).Select(entry => $"{entry.Key}:{entry.Value};"))
            + (shadowed && !look.ContainsKey("box-shadow") ? "box-shadow:none;" : string.Empty)).ToList();
        var (normal, hover, pressed, chosen, chosenHover, chosenPressed) = (css[0], css[1], css[2], css[3], css[4], css[5]);
        var focus = css[6] == normal ? string.Empty : $"|:focus-visible{{{css[6]}outline:none;}}";
        focus += css[7] == chosen ? string.Empty : $"|{selected}:focus-visible{{{css[7]}outline:none;}}";
        var rules = $"{{{normal}}}|:hover{{{hover}}}|:active{{{pressed}}}|{selected}{{{chosen}}}|{selected}:hover{{{chosenHover}}}|{selected}:active{{{chosenPressed}}}{focus}";
        var name = "it" + CssValues.Hash(rules);
        context.Sheet.AddRule(name, string.Concat(rules.Split('|').Select(rule => "." + name + rule)) + "\n");
        return name;
    }

    /// <summary>
    /// The selected tab's pipe (its template's <c>PART_SelectedPipe</c>) as the declarations of an absolute <c>::after</c>,
    /// placed from the part's alignment, size and margins within its parent's place in the item, as Avalonia arranges it, measured from inside the border the tab's element takes from its layout root (where an absolute box starts)
    /// (Fluent's inside the tab's padding, Aioros.Cosmo's line tabs below or beside it); empty when the part does not show
    /// while the tab is selected (no fill, hidden, or transparent, read with its transitions held so none is caught midway).
    /// </summary>
    public static string Pipe(Control item)
    {
        if (Part<Border>(item, "PART_SelectedPipe") is not { } pipe || pipe.GetVisualParent() is not Visual parent)
        {
            return string.Empty;
        }

        var classes = (IPseudoClasses)item.Classes;
        var selected = item.Classes.Contains(Selected);
        var held = pipe.SetValue(Animatable.TransitionsProperty, null, global::Avalonia.Data.BindingPriority.Animation);
        classes.Set(Selected, true);
        var (shown, fill, opacity) = (pipe.IsVisible && pipe.Opacity > 0, CssValues.Brush(pipe.Background), pipe.Opacity);
        var (width, height, margin, across, along, radius) = (pipe.Width, pipe.Height, pipe.Margin, pipe.HorizontalAlignment, pipe.VerticalAlignment, pipe.CornerRadius);
        classes.Set(Selected, selected);
        held?.Dispose();
        if (!shown || fill is null)
        {
            return string.Empty;
        }

        var origin = parent.TranslatePoint(default, item) ?? default;
        var frame = Part<Border>(item, "PART_LayoutRoot")?.BorderThickness ?? default;
        var inside = new Thickness(origin.X - frame.Left, origin.Y - frame.Top, item.Bounds.Width - origin.X - parent.Bounds.Width - frame.Right, item.Bounds.Height - origin.Y - parent.Bounds.Height - frame.Bottom);
        var style = new StringBuilder();
        Axis(style, "left", "right", "width", across == HorizontalAlignment.Stretch ? null : across == HorizontalAlignment.Right, width, inside.Left + margin.Left, inside.Right + margin.Right);
        Axis(style, "top", "bottom", "height", along == VerticalAlignment.Stretch ? null : along == VerticalAlignment.Bottom, double.IsNaN(height) && along != VerticalAlignment.Stretch ? 2 : height, inside.Top + margin.Top, inside.Bottom + margin.Bottom);
        style.Append(CultureInfo.InvariantCulture, $"border-radius:{CssValues.Px(radius.TopLeft)};background:{fill};");
        style.Append(opacity < 1 ? $"opacity:{CssValues.Num(opacity)};" : string.Empty);
        return style.ToString();
    }

    /// <summary>One axis of the pipe: both edges when it stretches (its size unset), else its size from the edge it is aligned to.</summary>
    private static void Axis(StringBuilder style, string start, string end, string size, bool? toEnd, double length, double before, double after)
    {
        if (toEnd is null && double.IsNaN(length))
        {
            style.Append(CultureInfo.InvariantCulture, $"{start}:{CssValues.Px(before)};{end}:{CssValues.Px(after)};");
            return;
        }

        style.Append(toEnd == true ? $"{end}:{CssValues.Px(after)};" : $"{start}:{CssValues.Px(before)};");
        style.Append(double.IsNaN(length) ? string.Empty : $"{size}:{CssValues.Px(length)};");
    }

    public static T? Part<T>(Control control, string name)
        where T : Control =>
        global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(control).OfType<T>().FirstOrDefault(part => part.Name == name);

    public static string? Resource(Control control, string key) =>
        control.TryFindResource(key, control.ActualThemeVariant, out var value)
            ? value switch
            {
                IBrush brush => CssValues.Brush(brush),
                Color color => CssValues.Color(color),
                _ => null,
            }
            : null;

    private static Dictionary<string, string> Paint(TemplatedControl item, Control part, Control text, string[] states, bool hoverOnPart)
    {
        var classes = (IPseudoClasses)item.Classes;
        var partClasses = (IPseudoClasses)part.Classes;
        var before = Toggled.Where(item.Classes.Contains).ToList();
        foreach (var state in Toggled)
        {
            classes.Remove(state);
        }

        foreach (var state in states)
        {
            (hoverOnPart && state == Hover ? partClasses : classes).Add(state);
        }

        var css = Css(part, text);
        foreach (var state in states)
        {
            (hoverOnPart && state == Hover ? partClasses : classes).Remove(state);
        }

        foreach (var state in before)
        {
            classes.Add(state);
        }

        return css;
    }

    private static Dictionary<string, string> Css(Control part, Control text)
    {
        var (background, border, thickness, shadow) = part switch
        {
            Border frame => (frame.Background, frame.BorderBrush, frame.BorderThickness, frame.BoxShadow),
            ContentPresenter presenter => (presenter.Background, presenter.BorderBrush, presenter.BorderThickness, presenter.BoxShadow),
            Panel panel => (panel.Background, null, default(Thickness), default(BoxShadows)),
            _ => (null, null, default(Thickness), default(BoxShadows)),
        };
        var css = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"] = CssValues.Brush(background) ?? "transparent",
        };
        if (CssValues.Brush(text.GetValue(TextElement.ForegroundProperty)) is { } color)
        {
            css["color"] = color;
        }

        if (thickness != default)
        {
            css["border-color"] = CssValues.Brush(border) ?? "transparent";
        }

        if (shadow.Count > 0)
        {
            css["box-shadow"] = BoxStyles.Shadow(shadow);
        }

        return css;
    }
}
