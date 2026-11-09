using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// The look of an overlay's surface as its control theme realizes it: a flyout's or a menu's presenter, a context menu,
/// or the border a template's popup shows. Its size limits, the root border of its template (fill, border, radius, box
/// shadow), the room between that border and its content (the border's padding and the content presenter's padding, or
/// the items presenter's margin), its font and its transitions; nothing of Aioros's own.
/// </summary>
internal static class SurfaceLooks
{
    public static StringBuilder Of(Control surface)
    {
        ThemeLooks.ExpandTemplates(surface);
        var style = new StringBuilder();
        Size(style, surface);
        var root = Root(surface);
        var inside = root is null ? default : Inside(root);
        if (root is not null)
        {
            BoxStyles.Fill(style, root.Background, root.BorderBrush, root.BorderThickness, root.CornerRadius, root.Padding + inside);
            style.Append(root.BoxShadow.Count > 0 ? $"box-shadow:{BoxStyles.Shadow(root.BoxShadow)};" : "box-shadow:none;");
        }
        else if (surface is TemplatedControl templated)
        {
            BoxStyles.Fill(style, templated.Background, templated.BorderBrush, templated.BorderThickness, templated.CornerRadius, templated.Padding);
        }

        if (surface is TemplatedControl font)
        {
            BoxStyles.Font(style, font.FontSize, font.FontWeight, font.Foreground);
        }

        if (root?.GetVisualDescendants().FirstOrDefault(part => part.GetType() == typeof(ContentPresenter)) is ContentPresenter presenter && presenter.GetValue(TextBlock.LineHeightProperty) is > 0 and var line)
        {
            style.Append(CultureInfo.InvariantCulture, $"line-height:{CssValues.Px(line)};");
        }

        if ((root ?? surface).Transitions is { Count: > 0 } moves && MotionStyles.TransitionList(moves) is { } css)
        {
            var longest = CssValues.Ms(moves.OfType<global::Avalonia.Animation.TransitionBase>().Select(move => move.Duration + move.Delay).DefaultIfEmpty().Max());
            style.Append(CultureInfo.InvariantCulture, $"transition:{css},display {longest} allow-discrete,overlay {longest} allow-discrete;");
        }

        return style;
    }

    /// <summary>The root border of a surface's template: the surface itself when it is a border, or the first border its template shows.</summary>
    public static Border? Root(Control surface) => surface as Border ?? surface.GetVisualDescendants().OfType<Border>().FirstOrDefault();

    private static void Size(StringBuilder style, Control surface)
    {
        style.Append(double.IsNaN(surface.Width) ? "width:max-content;" : $"width:{CssValues.Px(surface.Width)};");
        style.Append(surface.MinWidth > 0 ? $"min-width:{CssValues.Px(surface.MinWidth)};" : string.Empty);
        style.Append(double.IsInfinity(surface.MaxWidth) ? string.Empty : $"max-width:{CssValues.Px(surface.MaxWidth)};");
    }

    /// <summary>
    /// The room inside a surface's root border before its content: its content presenter's padding, or its items
    /// presenter's margin, also where a scroll viewer holds it (a closed popup's scroll viewer has not presented it yet);
    /// never a part of the scroll bars.
    /// </summary>
    private static Thickness Inside(Border root) =>
        (root.GetVisualDescendants().FirstOrDefault(part => (part is ItemsPresenter || part.GetType() == typeof(ContentPresenter)) && !part.GetVisualAncestors().OfType<ScrollBar>().Any())
            ?? root.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.Content as Visual) switch
        {
            ItemsPresenter items => items.Margin,
            ContentPresenter content => content.Padding + content.Margin,
            _ => default,
        };
}
