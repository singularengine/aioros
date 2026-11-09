using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Vectors;
using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// A select list's scroll buttons (controls of its popup template marked <see cref="Web.ScrollButtonProperty"/> <c>Up</c>
/// or <c>Down</c>, Radix Select's scroll buttons): an <c>aria-hidden</c> element written before the options and after them,
/// each drawn as its control (its height and its drawing centered), sticky against the picker's border at its top or bottom (over the list's padding) and shown only
/// while the picker can scroll that way (scroll-state container queries), the picker's scroll padding their height, under
/// <c>@supports (appearance:base-select)</c>. The scroller module of the framework's script scrolls the list while one is
/// pointed at (ScrollerScript).
/// </summary>
internal static class SelectButtons
{
    private sealed record Drawn(double Height, double Width, string? Svg);

    /// <summary>The select's class drawing its buttons, or nothing when its popup template marks none.</summary>
    public static string? Class(HtmlContext context, ComboBox combo)
    {
        if (Host(combo) is not ({ } host, { } child, { } popup))
        {
            return null;
        }

        var drawn = Read(combo, host, child, popup);
        if (drawn.Count == 0)
        {
            return null;
        }

        var css = Css(drawn, CssValues.Brush(popup.Background) ?? "transparent", popup.GetLogicalDescendants().OfType<ItemsPresenter>().FirstOrDefault()?.Margin ?? default);
        var name = "sc" + CssValues.Hash(css);
        context.Sheet.AddRule(name, css.Replace("$", name, StringComparison.Ordinal));
        return name;
    }

    /// <summary>The element of the button a side names (<c>Up</c> or <c>Down</c>), written before or after the options; nothing when the template marks none.</summary>
    public static string Element(ComboBox combo, string side) =>
        Host(combo) is (_, { } child, _) && child.GetSelfAndLogicalDescendants().OfType<Control>().Any(control => Web.GetScrollButton(control) == side)
            ? $"<div aria-hidden=\"true\" data-aioros-scroller=\"{side.ToLowerInvariant()}\"></div>"
            : string.Empty;

    private static (Popup? Host, Control? Child, Border? Popup) Host(ComboBox combo) =>
        !Web.GetNativeList(combo) && combo.GetVisualDescendants().OfType<Popup>().FirstOrDefault() is { Child: { } child } host
            ? (host, child, child.GetSelfAndLogicalDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "PopupBorder"))
            : (null, null, null);

    private static Dictionary<string, Drawn> Read(ComboBox combo, Popup host, Control child, Border popup)
    {
        var drawn = new Dictionary<string, Drawn>(StringComparer.Ordinal);
        ThemeLooks.ExpandTemplates(popup);
        if (child.IsAttachedToVisualTree())
        {
            Measure(child, drawn);
            return drawn;
        }

        host.SetCurrentValue(Popup.ChildProperty, null);
        try
        {
            Writers.Probes.With(combo, child, shown => Measure(shown, drawn));
        }
        finally
        {
            host.SetCurrentValue(Popup.ChildProperty, child);
        }

        return drawn;
    }

    private static void Measure(Control? surface, Dictionary<string, Drawn> drawn)
    {
        foreach (var button in surface?.GetVisualDescendants().OfType<Control>().Where(control => Web.GetScrollButton(control) is "Up" or "Down").ToList() ?? [])
        {
            var shown = button.IsVisible;
            button.SetCurrentValue(Visual.IsVisibleProperty, true);
            Writers.Probes.Invalidate(button);
            Writers.Probes.Settle(surface!);
            var size = button.Bounds.Size;
            var shapes = button.GetVisualDescendants().OfType<Shape>().Select(shape => ((Visual)shape, shape.TransformToVisual(button) ?? Matrix.Identity, shape.Opacity));
            drawn[Web.GetScrollButton(button)!] = new Drawn(size.Height, size.Width, DrawingSvg.Parts(shapes, size));
            button.SetCurrentValue(Visual.IsVisibleProperty, shown);
        }
    }

    private static string Css(Dictionary<string, Drawn> drawn, string background, Thickness padding)
    {
        var css = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"[data-aioros-scroller]{{display:none}}@supports (appearance:base-select){{.$::picker(select){{container-type:scroll-state;container-name:aioros-pick;scroll-padding-block:{CssValues.Px(drawn.Values.Max(button => button.Height))}}}"));
        foreach (var (side, button) in drawn)
        {
            var (name, edge, inset) = side == "Up" ? ("up", "top", padding.Top) : ("down", "bottom", padding.Bottom);
            var image = button.Svg is { } svg ? $" {DrawingSvg.Url(svg)} no-repeat center/{CssValues.Px(button.Width)} {CssValues.Px(button.Height)}" : string.Empty;
            css.Append(CultureInfo.InvariantCulture, $".$>[data-aioros-scroller={name}]{{position:sticky;{edge}:{CssValues.Px(-inset)};margin-inline:{CssValues.Px(-padding.Left)} {CssValues.Px(-padding.Right)};z-index:1;height:{CssValues.Px(button.Height)};background:{background}{image}}}");
            css.Append(CultureInfo.InvariantCulture, $"@container aioros-pick scroll-state(scrollable:{edge}){{.$>[data-aioros-scroller={name}]{{display:block}}}}");
        }

        return css.Append("}\n").ToString();
    }
}
