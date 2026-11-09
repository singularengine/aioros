using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Vectors;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// A <c>ComboBox</c> written as a <c>&lt;select&gt;</c> drawn as its control theme draws it: the browser's own field and
/// arrow off (<c>appearance:none</c>), its <c>Background</c> part's fill, border, radius and shadow at rest and when pointed
/// at, focused from the keyboard or invalid, its <c>DropDownGlyph</c> drawn from the template as an SVG background where
/// the theme places it, and, where the browser has the customizable select (<c>appearance:base-select</c>, Chromium),
/// its list drawn as the theme's popup (<c>PopupBorder</c>) and items (the <c>ComboBoxItem</c> theme), the browser's
/// own list elsewhere (Firefox).
/// </summary>
internal static class SelectLooks
{
    /// <summary>The select's classes, its look appended to <paramref name="style"/>; null when the theme has no <c>Background</c> part to read.</summary>
    public static string? Of(HtmlContext context, ComboBox combo, StringBuilder style)
    {
        ThemeLooks.ExpandTemplates(combo);
        if (FieldLooks.Part<Border>(combo, "Background") is not { } part)
        {
            return null;
        }

        (IBrush? Background, IBrush? Border)? rest = null;
        var look = StateLooks.Resolve(combo, combo.IsEffectivelyEnabled ? FieldLooks.States(combo, ":focus-visible", ":focus-visible") : [], () =>
        {
            rest ??= (part.Background, part.BorderBrush);
            return FieldLooks.Snapshot(part, combo.Foreground);
        });
        var normal = new Frame(rest?.Background, rest?.Border, part.BorderThickness, part.CornerRadius, combo.Padding);
        StateLooks.LayOut(combo);
        var glyph = Glyph(combo);
        var padding = glyph is null ? combo.Padding : new Thickness(combo.Padding.Left, combo.Padding.Top, Math.Max(combo.Padding.Right, glyph.Value.Reserved), combo.Padding.Bottom);
        BoxStyles.Fill(style, normal.Background, normal.Border, normal.Thickness, normal.Radius, padding);
        style.Append(FieldLooks.Rest(look, null, normal));
        if (glyph is { } drawn)
        {
            style.Append(CultureInfo.InvariantCulture, $"background-image:{drawn.Image};background-repeat:no-repeat;background-origin:border-box;background-position:right {CssValues.Px(drawn.Right)} center;background-size:{CssValues.Px(drawn.Size.Width)} {CssValues.Px(drawn.Size.Height)};");
        }

        style.Append(part.Transitions is { Count: > 0 } transitions && MotionStyles.TransitionList(transitions) is { } moves ? $"transition:{moves};" : string.Empty);
        style.Append("appearance:none;-webkit-appearance:none;");
        style.Append(CssValues.Brush(Ink(combo, "ContentPresenter")) is { } ink && ink != CssValues.Brush(combo.Foreground) ? $"color:{ink};" : string.Empty);
        var rules = context.Sheet.Rules(FieldLooks.Rules(look, normal.Thickness).Select(rule => (rule.Selector, rule.Rule.Replace("background:", "background-color:", StringComparison.Ordinal))));
        return CssValues.Join(rules, CssValues.Join(Placeholder(context, combo), CssValues.Join(Picker(context, combo), SelectButtons.Class(context, combo))));
    }

    /// <summary>
    /// The theme's drop-down glyph as an SVG, its distance from the right edge and the room the content leaves it: from
    /// where the content's column ends to the right border (or, with no such column, from the glyph's margin), the same
    /// whichever content shows (the chosen item or the placeholder), so a choice writes the select as it is before it.
    /// </summary>
    private static (string Image, double Right, double Reserved, Size Size)? Glyph(ComboBox combo)
    {
        if (combo.GetVisualDescendants().OfType<Control>().FirstOrDefault(part => part.Name == "DropDownGlyph") is not { } glyph
            || glyph.TransformToVisual(combo) is not { } placed)
        {
            return null;
        }

        var shapes = glyph.GetSelfAndVisualDescendants().OfType<Shape>().Where(shape => shape.GetSelfAndVisualAncestors().TakeWhile(visual => !ReferenceEquals(visual, glyph)).All(visual => visual.IsVisible)).ToList();
        if (shapes.Count == 0 || DrawingSvg.Parts(shapes.Select(shape => ((Visual)shape, shape.TransformToVisual(glyph) ?? Matrix.Identity, glyph.Opacity * (shape == glyph ? 1 : shape.Opacity))), glyph.Bounds.Size) is not { } svg)
        {
            return null;
        }

        var width = combo.Bounds.Width;
        var right = width - (placed.M31 + glyph.Bounds.Width);
        var reserved = width - combo.BorderThickness.Right - (ContentEdge(combo) ?? placed.M31 - glyph.Margin.Left);
        return (DrawingSvg.Url(svg), right, Math.Max(0, reserved), glyph.Bounds.Size);
    }

    /// <summary>
    /// Where the chosen item's content ends in the combo box: the right edge of the grid column its presenter stands in
    /// (the grid's width less the columns after it, the glyph's: fixed, or as wide as their widest part), less the
    /// presenter's right margin, which holds whether an item or the placeholder shows; nothing for a template whose
    /// presenter stands in no grid or has a star column after it.
    /// </summary>
    private static double? ContentEdge(ComboBox combo)
    {
        if (combo.GetVisualDescendants().OfType<Control>().FirstOrDefault(static part => part.Name == "ContentPresenter") is not { } presenter
            || presenter.GetVisualParent() is not Grid grid || grid.TransformToVisual(combo) is not { } placed)
        {
            return null;
        }

        var first = Grid.GetColumn(presenter) + Grid.GetColumnSpan(presenter);
        var widths = grid.ColumnDefinitions.Select((column, index) => (column, index)).Skip(first)
            .Select(entry => entry.column.Width switch
            {
                { IsAbsolute: true } width => width.Value,
                { IsAuto: true } => grid.Children.Where(child => Grid.GetColumn(child) == entry.index && Grid.GetColumnSpan(child) == 1 && child.IsVisible).Select(static child => child.DesiredSize.Width).DefaultIfEmpty(0).Max(),
                _ => double.NaN,
            }).ToList();
        return widths.Exists(double.IsNaN) ? null : placed.M31 + grid.Bounds.Width - widths.Sum() - presenter.Margin.Right;
    }

    /// <summary>The text color a template part takes in the control's state (a theme's <c>:disabled</c> look), as Avalonia draws its text.</summary>
    private static IBrush? Ink(ComboBox combo, string name) =>
        combo.GetVisualDescendants().OfType<Control>().FirstOrDefault(part => part.Name == name)?.GetValue(TextElement.ForegroundProperty);

    /// <summary>The placeholder's color while the placeholder option is the selected one.</summary>
    private static string? Placeholder(HtmlContext context, ComboBox combo)
    {
        if (string.IsNullOrEmpty(combo.PlaceholderText) || CssValues.Brush(Ink(combo, "PlaceholderTextBlock") ?? combo.PlaceholderForeground) is not { } color)
        {
            return null;
        }

        var name = "sp" + CssValues.Hash(color);
        context.Sheet.AddRule(name, $".{name}:has(>option[value=\"\"]:checked){{color:{color}}}\n");
        return name;
    }

    /// <summary>
    /// The list of a customizable select drawn as the theme's popup border (fill, border, radius, shadow, the items
    /// presenter's margin as padding) and its items as the theme's item (padding, radius, line height, and its look when
    /// pointed at, focused or selected), under <c>@supports (appearance:base-select)</c>.
    /// </summary>
    private static string? Picker(HtmlContext context, ComboBox combo)
    {
        var host = Aioros.Markup.Web.GetNativeList(combo) ? null : combo.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
        if (host?.Child is not { } child
            || child.GetSelfAndLogicalDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "PopupBorder") is not { } popup)
        {
            return null;
        }

        ThemeLooks.ExpandTemplates(popup);
        var motion = string.Empty;
        if (!child.IsAttachedToVisualTree())
        {
            host.SetCurrentValue(Popup.ChildProperty, null);
            try
            {
                Writers.Probes.With(combo, child, shown => motion = shown is null ? string.Empty : Motion(popup));
            }
            finally
            {
                host.SetCurrentValue(Popup.ChildProperty, child);
            }
        }

        var list = new StringBuilder();
        BoxStyles.Fill(list, popup.Background, popup.BorderBrush, popup.BorderThickness, popup.CornerRadius, popup.GetLogicalDescendants().OfType<ItemsPresenter>().FirstOrDefault()?.Margin ?? default);
        list.Append(popup.BoxShadow.Count > 0 ? $"box-shadow:{BoxStyles.Shadow(popup.BoxShadow)};" : string.Empty);
        list.Append(double.IsFinite(popup.MaxHeight) ? $"box-sizing:border-box;max-height:{CssValues.Px(popup.MaxHeight)};" : string.Empty);
        list.Append(CssValues.Brush(popup.GetValue(TextElement.ForegroundProperty)) is { } text ? $"color:{text};" : string.Empty);
        var item = Item(combo) + SelectOptions.Disabled(combo) + SelectOptions.CheckMark(combo, host, child);
        var placement = Writers.Placements.Of(host.Placement, host.HorizontalOffset, host.VerticalOffset, host.PlacementAnchor, host.PlacementGravity, ReferenceEquals(popup, child) ? default : popup.Margin, Aioros.Markup.Web.GetCollisionPadding(host));
        var css = $"@supports (appearance:base-select){{.$.$,.$::picker(select){{appearance:base-select}}.$::picker-icon{{display:none}}.$::picker(select){{{list}min-width:anchor-size(width);{placement}}}{item}{motion}}}\n";
        var name = "pk" + CssValues.Hash(css);
        context.Sheet.AddRule(name, css.Replace("$", name, StringComparison.Ordinal));
        return name;
    }

    /// <summary>
    /// The list's motion, from its popup border lent to the page as an open popup shows it: its transitions on the picker
    /// (with <c>display</c> and <c>overlay</c> kept while it leaves), its closing look while closed, its resting look while
    /// the select is <c>:open</c>, its entrance as the open picker's <c>@starting-style</c>, and none of it for a visitor
    /// who asks for less motion; nothing when the border does not enter.
    /// </summary>
    private static string Motion(Border popup)
    {
        if (!Entrances.Settle(popup).TryGetValue(popup, out var entrance) || popup.Transitions is not { Count: > 0 } moves || MotionStyles.TransitionList(moves) is not { } transitions)
        {
            return string.Empty;
        }

        var longest = CssValues.Ms(moves.OfType<global::Avalonia.Animation.TransitionBase>().Select(move => move.Duration + move.Delay).DefaultIfEmpty().Max());
        var closed = Entrances.Leaving(popup, popup)?.Css ?? entrance;
        var rest = (entrance.Contains("opacity:", StringComparison.Ordinal) ? "opacity:1;" : string.Empty) + (entrance.Contains("transform:", StringComparison.Ordinal) ? "transform:none;" : string.Empty);
        return $".$::picker(select){{transition:{transitions},display {longest} allow-discrete,overlay {longest} allow-discrete;{closed}}}.$:open::picker(select){{{rest}}}" +
            $"@starting-style{{.$:open::picker(select){{{entrance}}}}}@media (prefers-reduced-motion:reduce){{.$::picker(select){{transition:none!important}}}}";
    }

    /// <summary>The items' look from the first item container the combo holds (its own <c>ComboBoxItem</c>s), at rest and pointed at, focused and selected.</summary>
    private static string Item(ComboBox combo)
    {
        if (combo.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.IsEnabled) is not { } item)
        {
            return string.Empty;
        }

        ThemeLooks.ExpandTemplates(item);
        if (item.GetVisualDescendants().OfType<Control>().FirstOrDefault(part => part.Name == "PART_ContentPresenter") is not { } part)
        {
            return string.Empty;
        }

        var look = StateLooks.Resolve(item, [new(":pointerover", ":hover"), new(":focus", ":focus-visible"), new(":selected", ":checked")], () => ThemeLooks.Snapshot(item, part));
        var rest = new StringBuilder();
        BoxStyles.Fill(rest, null, null, default, default, item.Padding);
        rest.Append(look.Rest.GetValueOrDefault("border-radius") is { } radius and not "0px 0px 0px 0px" ? $"border-radius:{radius};" : string.Empty);
        rest.Append(part.GetValue(TextBlock.LineHeightProperty) is var line and > 0 ? $"line-height:{CssValues.Px(line)};" : string.Empty);
        rest.Append(CultureInfo.InvariantCulture, $"font-size:{CssValues.Px(item.FontSize)};");
        var rules = string.Concat(look.Rules.Select(rule => $".$ option{rule.Selector}{{{string.Concat(rule.Rule.Where(entry => entry.Key is "background" or "color").Select(entry => $"{entry.Key}:{entry.Value};"))}}}"));
        return $".$ option{{{rest}}}{rules}";
    }
}
