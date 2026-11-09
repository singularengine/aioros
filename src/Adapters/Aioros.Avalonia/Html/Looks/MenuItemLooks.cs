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

/// <summary>The parts of a menu item as its theme lays them out: its box's classes, its toggle glyph, and its icon's, gesture's and chevron's places.</summary>
internal sealed record MenuItemLook(string Classes, string? Glyph, string? Icon, string? Gesture, string? Chevron);

/// <summary>
/// The look of a menu item as its control theme realizes it in its open menu: its layout root's fill, border, radius and
/// states (Avalonia gives an item <c>:selected</c> while it is pointed at or focused, so it is the item's <c>:focus</c>,
/// which the overlay module moves with the pointer, and an item with sub-items <c>:open</c> while its menu is open), its
/// font, its foreground, its opacity, and its parts placed where Avalonia lays them out: the room before its icon or
/// header, between its parts and after its last one, its height, and the shapes its template draws over it for a checked
/// toggle or a radio (a check, a dot) at their place.
/// </summary>
internal static class MenuItemLooks
{
    private static readonly (string Pseudo, string Selector)[] States = [(":pointerover", "hover"), (":selected", "focus"), (":pressed", "active")];

    /// <summary>The states of an item with sub-items, open while its menu popover, written right after it, is open (Radix's <c>data-state=open</c>).</summary>
    private static readonly (string Pseudo, string Selector)[] ParentStates = [.. States, (":open", "has(+:popover-open)")];

    private static readonly string[] Transient = [":selected", ":focus", ":focus-within", ":focus-visible", ":pointerover", ":pressed", ":open"];

    private static readonly string[] Parts = ["PART_IconPresenter", "PART_HeaderPresenter", "PART_InputGestureText", "PART_ChevronPath"];

    public static MenuItemLook Of(HtmlContext context, MenuItem item)
    {
        ThemeLooks.ExpandTemplates(item);
        var classes = (IPseudoClasses)item.Classes;
        foreach (var state in Transient)
        {
            classes.Remove(state);
        }

        TopLevel.GetTopLevel(item)?.UpdateLayout();
        var look = ThemeLooks.Of(item, "PART_LayoutRoot", item.ItemCount > 0 ? ParentStates : States);
        var style = new StringBuilder(look.Css);
        var parts = item.GetVisualDescendants().OfType<Control>().Where(part => part.Name is { } name && Parts.Contains(name)).ToDictionary(part => part.Name!, StringComparer.Ordinal);
        var header = parts.GetValueOrDefault("PART_HeaderPresenter");
        BoxStyles.Font(style, item.FontSize, item.FontWeight, (header as ContentPresenter)?.Foreground ?? item.Foreground);
        style.Append(header?.GetValue(TextBlock.LineHeightProperty) is > 0 and var line ? $"line-height:{CssValues.Px(line)};" : string.Empty);
        var opacity = Entrances.Opacity(item);
        style.Append(opacity < 1 ? $"opacity:{CssValues.Num(opacity)};" : string.Empty);
        style.Append("position:relative;display:flex;align-items:center;width:100%;text-align:start;");
        var placed = Placed(item, parts);
        Layout(style, item, placed);
        var name = context.Sheet.Class(context.Sheet.States(look), style);
        return new MenuItemLook(name, Glyph(item, parts), Gap(placed, "PART_IconPresenter"), Gap(placed, "PART_InputGestureText", Gesture(parts)), Chevron(placed));
    }

    /// <summary>
    /// The look of a toggle item whose check is bound, checked or not (a theme may lay a checked item out otherwise, as
    /// Fluent's moves its header past the check): read with the item set so for the time it is read, its binding kept.
    /// </summary>
    public static MenuItemLook Checked(HtmlContext context, MenuItem item, bool isChecked)
    {
        var held = item.IsChecked == isChecked ? null : Plans.LocalBindings.Override(item, MenuItem.IsCheckedProperty, isChecked);
        Relayout(item);
        try
        {
            return Of(context, item);
        }
        finally
        {
            if (held is not null)
            {
                held.Dispose();
                Relayout(item);
            }
        }
    }

    /// <summary>Lays out again the menu surface an item is shown in, measured at its own size as it was realized.</summary>
    private static void Relayout(MenuItem item)
    {
        if (item.GetVisualAncestors().OfType<Control>().LastOrDefault(ancestor => ancestor is MenuFlyoutPresenter or ContextMenu) is { } surface)
        {
            foreach (var part in item.GetVisualDescendants().OfType<global::Avalonia.Layout.Layoutable>().Prepend(item).Concat(item.GetVisualAncestors().OfType<global::Avalonia.Layout.Layoutable>()))
            {
                part.InvalidateMeasure();
            }

            Writers.Probes.Settle(surface);
        }
    }

    /// <summary>The parts shown, in their order, with their box in the item's space.</summary>
    private static List<(string Name, Rect Box, Control Part)> Placed(MenuItem item, Dictionary<string, Control> parts) =>
        [.. Parts.Where(parts.ContainsKey).Select(name => (Name: name, Part: parts[name]))
            .Where(entry => Writers.Probes.Shows(entry.Part, item) && entry.Part.Bounds.Width > 0 && (entry.Name != "PART_InputGestureText" || entry.Part is TextBlock { Text.Length: > 0 }))
            .Select(entry => (entry.Name, Box: new Rect(entry.Part.TranslatePoint(default, item) ?? default, entry.Part.Bounds.Size), entry.Part))];

    private static void Layout(StringBuilder style, MenuItem item, List<(string Name, Rect Box, Control Part)> placed)
    {
        if (placed.Count == 0 || item.Bounds.Width <= 0)
        {
            return;
        }

        var right = item.Bounds.Width - placed[^1].Box.Right;
        style.Append(CultureInfo.InvariantCulture, $"padding:0 {CssValues.Px(Math.Max(0, right))} 0 {CssValues.Px(Math.Max(0, placed[0].Box.X))};min-height:{CssValues.Px(item.Bounds.Height)};");
    }

    /// <summary>
    /// A part's style: an icon's box and the room after it, as Avalonia lays them out; a part after the header (the
    /// gesture, the chevron) keeps its own margin from the header, which takes the free room, and the room Avalonia
    /// leaves between it and the part before it on the same side.
    /// </summary>
    private static string? Gap(List<(string Name, Rect Box, Control Part)> placed, string name, string? extra = null)
    {
        var index = placed.FindIndex(entry => entry.Name == name);
        if (index < 0)
        {
            return null;
        }

        var (_, box, part) = placed[index];
        if (name == "PART_IconPresenter")
        {
            var after = index + 1 < placed.Count ? $"margin-right:{CssValues.Px(Math.Max(0, placed[index + 1].Box.X - box.Right))};" : string.Empty;
            return $"display:inline-flex;align-items:center;justify-content:center;flex:0 0 {CssValues.Px(box.Width)};height:{CssValues.Px(box.Height)};" + after + extra;
        }

        var before = index > 0 && placed[index - 1].Name != "PART_HeaderPresenter" && placed[index - 1].Name != "PART_IconPresenter" ? box.X - placed[index - 1].Box.Right : part.Margin.Left;
        return $"margin-left:{CssValues.Px(Math.Max(0, before))};" + extra;
    }

    private static string? Gesture(Dictionary<string, Control> parts) =>
        parts.GetValueOrDefault("PART_InputGestureText") is TextBlock text
            ? $"font-family:inherit;font-size:{CssValues.Px(text.FontSize)};{(CssValues.Brush(text.Foreground) is { } color ? $"color:{color};" : string.Empty)}{(text.LetterSpacing != 0 ? $"letter-spacing:{CssValues.Px(text.LetterSpacing)};" : string.Empty)}{(text.LineHeight > 0 ? $"line-height:{CssValues.Px(text.LineHeight)};" : string.Empty)}"
            : null;

    /// <summary>The theme's chevron of an item with sub-items, as an inline SVG after the room before it.</summary>
    private static string? Chevron(List<(string Name, Rect Box, Control Part)> placed)
    {
        var index = placed.FindIndex(entry => entry.Name == "PART_ChevronPath");
        if (index < 0)
        {
            return null;
        }

        var (_, box, part) = placed[index];
        var before = index > 0 && placed[index - 1].Name == "PART_InputGestureText" ? Math.Max(0, box.X - placed[index - 1].Box.Right) : part.Margin.Left;
        return DrawingSvg.Parts([(part, Matrix.Identity, 1)], box.Size) is { } svg
            ? svg.Insert(4, string.Create(CultureInfo.InvariantCulture, $" aria-hidden=\"true\" style=\"flex-shrink:0;margin-left:{CssValues.Px(before)}\""))
            : null;
    }

    /// <summary>
    /// What the template draws for a checked toggle or radio, outside the item's icon, header, gesture and chevron (a check
    /// mark, a dot), as an SVG laid over the item at the place Avalonia draws it; nothing for an unchecked item.
    /// </summary>
    private static string? Glyph(MenuItem item, Dictionary<string, Control> parts)
    {
        if (item.ToggleType == MenuItemToggleType.None || !item.IsChecked || item.Bounds.Width <= 0)
        {
            return null;
        }

        var excluded = parts.Values.Where(part => part.Name != "PART_ChevronPath").ToHashSet<Visual>();
        var shapes = item.GetVisualDescendants().OfType<Shape>()
            .Where(shape => Writers.Probes.Shows(shape, item) && shape.Name != "PART_ChevronPath" && !shape.GetVisualAncestors().Any(excluded.Contains) && !shape.GetVisualAncestors().OfType<Popup>().Any())
            .ToList();
        if (shapes.Count == 0)
        {
            return null;
        }

        var size = item.Bounds.Size;
        return DrawingSvg.Parts(shapes.Select(shape => ((Visual)shape, shape.TransformToVisual(item) ?? Matrix.Identity, shape.Opacity)), size) is { } svg
            ? svg.Insert(4, string.Create(CultureInfo.InvariantCulture, $" aria-hidden=\"true\" style=\"position:absolute;left:0;top:50%;margin-top:{CssValues.Px(-size.Height / 2)};pointer-events:none\""))
            : null;
    }

    /// <summary>The look of a separator a menu shows: its height, margins and fill as its theme realizes it, or nothing to keep the default rule.</summary>
    public static string? Separator(HtmlContext context, Control separator)
    {
        ThemeLooks.ExpandTemplates(separator);
        var line = separator as Separator ?? separator.GetVisualDescendants().OfType<Separator>().FirstOrDefault();
        if (line is null || line.Bounds.Height <= 0)
        {
            return null;
        }

        var fill = CssValues.Paint(line.Background) ?? CssValues.Paint(line.GetVisualDescendants().OfType<Border>().FirstOrDefault()?.Background);
        var style = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"height:{CssValues.Px(line.Bounds.Height)};margin:{BoxStyles.Edges(line.Margin)};border:0;opacity:{CssValues.Num(Entrances.Opacity(line))};"));
        style.Append(fill is null ? string.Empty : $"background:{fill};");
        return context.Sheet.Class(null, style);
    }
}
