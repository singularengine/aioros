using System.Globalization;
using System.Text;
using Aioros.Markup;
using Aioros.Avalonia.Html.Panels;
using Aioros.Avalonia.Plans;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

internal static class BoxStyles
{
    public static StringBuilder Box(Control control, ParentKind parent, IReadOnlySet<string> animated, bool size = true)
    {
        var style = new StringBuilder();
        Margin(style, control);
        if (size)
        {
            Size(style, control);
        }

        if (Web.GetAspectRatio(control) is var ratio && double.IsFinite(ratio) && ratio > 0)
        {
            style.Append(CultureInfo.InvariantCulture, $"aspect-ratio:{CssValues.Num(ratio)};");
            style.Append(Spans(control, parent) is { } slot ? $"width:{slot};" : string.Empty);
        }

        if (control.MinWidth > 0)
        {
            style.Append(CultureInfo.InvariantCulture, $"min-width:{CssValues.Px(control.MinWidth)};");
        }

        if (Bounded(control, parent) is { } bound)
        {
            style.Append(CultureInfo.InvariantCulture, $"max-width:{bound};");
        }

        style.Append(double.IsFinite(control.MaxHeight) && !LocalBindings.IsBound(control, Layoutable.MaxHeightProperty) ? $"max-height:{CssValues.Px(control.MaxHeight)};" : string.Empty);
        Effects(style, control, animated);
        Placement(style, control, parent);
        style.Append(Variants.ContainerQueries.Box(control));
        return style;
    }

    /// <summary>
    /// The control's whole box in its parent, for the element that holds the control and what Avalonia draws inside it
    /// beside its own part (a field's validation messages): its margin, size and size limits, opacity, transform, clip,
    /// z-index, alignment and place.
    /// </summary>
    public static StringBuilder Outer(Control control, ParentKind parent, IReadOnlySet<string> animated)
    {
        var style = new StringBuilder();
        Margin(style, control);
        Size(style, control);
        if (control.MinWidth > 0)
        {
            style.Append(CultureInfo.InvariantCulture, $"min-width:{CssValues.Px(control.MinWidth)};");
        }

        if (Bounded(control, parent) is { } bound)
        {
            style.Append(CultureInfo.InvariantCulture, $"max-width:{bound};");
        }

        style.Append(control.MinHeight > 0 ? $"min-height:{CssValues.Px(control.MinHeight)};" : string.Empty);
        style.Append(double.IsFinite(control.MaxHeight) ? $"max-height:{CssValues.Px(control.MaxHeight)};" : string.Empty);
        Effects(style, control, animated);
        Placement(style, control, parent);
        return style;
    }

    /// <summary>
    /// The control's size where it is fixed before the browser lays it out, its width and height; empty otherwise. Never
    /// its bounds, which depend on whether something laid the page out before it was written, so a plan and a render
    /// would differ.
    /// </summary>
    public static Size Sized(Control control) =>
        control.Width > 0 && control.Height > 0 ? new Size(control.Width, control.Height) : default;

    /// <summary>
    /// A background under a gradient border: the fill clipped to the padding box over the border's gradient on the border
    /// box, so the transparent border shows the gradient with the corner radii kept; null without both.
    /// </summary>
    public static string? Layered(string? fill, string? border) => fill is null || border is null
        ? null
        : $"{(fill.Contains("gradient(", StringComparison.Ordinal) ? fill : $"linear-gradient({fill},{fill})")} padding-box,{border} border-box";

    public static void Fill(StringBuilder style, IBrush? background, IBrush? border, Thickness thickness, CornerRadius radius, Thickness padding, bool resetBorder = true, Size size = default)
    {
        var fill = CssValues.Paint(background, size);
        var gradient = thickness != default && CssValues.Brush(border) is null ? CssGradients.Of(border, size) : null;
        if (Layered(fill, gradient) is { } layers)
        {
            style.Append(CultureInfo.InvariantCulture, $"background:{layers};");
        }
        else if (fill is not null)
        {
            style.Append(CultureInfo.InvariantCulture, $"background:{fill};");
        }

        if (thickness != default)
        {
            style.Append(CultureInfo.InvariantCulture, $"border-color:{CssValues.Brush(border) ?? "transparent"};border-width:{Edges(thickness)};border-style:solid;");
            style.Append(gradient is not null && fill is null ? $"border-image:{gradient} 1;" : string.Empty);
        }
        else if (resetBorder)
        {
            style.Append("border-width:0;");
        }

        if (radius != default)
        {
            style.Append(CultureInfo.InvariantCulture, $"border-radius:{Corners(radius)};");
        }

        if (padding != default)
        {
            style.Append(CultureInfo.InvariantCulture, $"padding:{Edges(padding)};");
        }
    }

    /// <summary>The font; a size of NaN leaves the size to the element's own style, where a bound size goes.</summary>
    public static void Font(StringBuilder style, double size, FontWeight weight, IBrush? foreground) => Font(style, size, weight, foreground, default);

    /// <summary>A text's font and foreground, a gradient foreground painted through its glyphs over <paramref name="box"/> (MOTION-25).</summary>
    public static void Font(StringBuilder style, double size, FontWeight weight, IBrush? foreground, Size box)
    {
        style.Append(double.IsNaN(size) ? string.Empty : $"font-size:{CssValues.Px(size)};");
        style.Append(CultureInfo.InvariantCulture, $"font-weight:{(int)weight};");
        if (CssValues.Brush(foreground) is { } color)
        {
            style.Append(CultureInfo.InvariantCulture, $"color:{color};");
        }
        else if (CssGradients.Of(foreground, box) is { } gradient)
        {
            style.Append(CultureInfo.InvariantCulture, $"background:{gradient};background-clip:text;-webkit-background-clip:text;color:transparent;-webkit-text-fill-color:transparent;");
        }
    }

    public static string Edges(Thickness thickness) =>
        $"{CssValues.Px(thickness.Top)} {CssValues.Px(thickness.Right)} {CssValues.Px(thickness.Bottom)} {CssValues.Px(thickness.Left)}";

    public static string Corners(CornerRadius radius) =>
        $"{CssValues.Px(radius.TopLeft)} {CssValues.Px(radius.TopRight)} {CssValues.Px(radius.BottomRight)} {CssValues.Px(radius.BottomLeft)}";

    public static string Tracks(IEnumerable<GridLength> lengths)
    {
        var parts = lengths.Select(length => length.GridUnitType switch
        {
            GridUnitType.Auto => "auto",
            GridUnitType.Star => $"minmax(0,{CssValues.Num(length.Value)}fr)",
            _ => CssValues.Px(length.Value),
        }).ToList();
        return parts.Count == 0 ? "minmax(0,1fr)" : string.Join(' ', parts);
    }

    public static string Shadow(BoxShadows shadows) =>
        string.Join(',', Enumerable.Range(0, shadows.Count).Select(index => shadows[index]).Select(shadow =>
            $"{(shadow.IsInset ? "inset " : string.Empty)}{CssValues.Px(shadow.OffsetX)} {CssValues.Px(shadow.OffsetY)} {CssValues.Px(shadow.Blur)} {CssValues.Px(shadow.Spread)} {CssValues.Color(shadow.Color)}"));

    /// <summary>
    /// The control's largest width: its <c>MaxWidth</c>, and for a control aligned across its slot without a width of its
    /// own, never more than that slot, as Avalonia's <c>ArrangeCore</c> arranges it at the smaller of its slot and its
    /// desired size, where CSS would grow it to its widest child; the content of a scroll viewer, arranged at its extent,
    /// keeps its own. A bound margin, written inline, is not taken from the slot, so the class stays the same for every model.
    /// </summary>
    private static string? Bounded(Control control, ParentKind parent)
    {
        var max = double.IsInfinity(control.MaxWidth) ? null : CssValues.Px(control.MaxWidth);
        if (control.HorizontalAlignment == HorizontalAlignment.Stretch || !double.IsNaN(control.Width) || Scrolled(control)
            || parent is not (ParentKind.Column or ParentKind.Single or ParentKind.Overlay or ParentKind.Dock or ParentKind.Uniform or ParentKind.Grid))
        {
            return max;
        }

        var slot = Slot(control);
        return max is null ? slot : $"min({max},{slot})";
    }

    /// <summary>
    /// The width of a control keeping a ratio (<c>a:Web.AspectRatio</c>) that is aligned in its slot without a width of
    /// its own: the slot, which its own layout measures as its width (Radix's <c>width:100%</c> wrapper), where CSS would
    /// shrink it to its empty content; null otherwise.
    /// </summary>
    private static string? Spans(Control control, ParentKind parent) =>
        control.HorizontalAlignment != HorizontalAlignment.Stretch && double.IsNaN(control.Width) && !Scrolled(control)
        && parent is ParentKind.Column or ParentKind.Single or ParentKind.Overlay or ParentKind.Dock or ParentKind.Uniform or ParentKind.Grid
            ? Slot(control)
            : null;

    private static string Slot(Control control)
    {
        var sides = BoundStyles.Margin(control) ? 0 : control.Margin.Left + control.Margin.Right;
        return sides == 0 ? "100%" : $"calc(100% - {CssValues.Px(sides)})";
    }

    public static bool CentersByMaxWidth(Control control) =>
        !double.IsInfinity(control.MaxWidth) && control.HorizontalAlignment == HorizontalAlignment.Stretch;

    private static void Margin(StringBuilder style, Control control)
    {
        var margin = control.Margin;
        var center = CentersByMaxWidth(control);
        if ((margin == default && !center) || BoundStyles.Margin(control))
        {
            return;
        }

        var right = center ? "auto" : CssValues.Px(margin.Right);
        var left = center ? Centered(margin, control.MaxWidth) : CssValues.Px(margin.Left);
        style.Append(CultureInfo.InvariantCulture, $"margin:{CssValues.Px(margin.Top)} {right} {CssValues.Px(margin.Bottom)} {left};");
        if (center)
        {
            var sides = margin.Left + margin.Right;
            style.Append(sides == 0 ? "width:100%;" : $"width:calc(100% - {CssValues.Px(sides)});");
        }
    }

    /// <summary>
    /// The left margin of a stretched control no wider than its <c>MaxWidth</c>, as Avalonia's layout places it: its slot
    /// is the parent's width less its margins and it is centered in that slot, so equal margins are <c>auto</c> and
    /// unequal ones the larger of the left margin and the offset that centers it in the slot.
    /// </summary>
    private static string Centered(Thickness margin, double maxWidth) =>
        Math.Abs(margin.Left - margin.Right) < 0.01 ? "auto" : $"max({CssValues.Px(margin.Left)},calc((100% - {CssValues.Px(maxWidth + margin.Right - margin.Left)}) / 2))";

    public static void Size(StringBuilder style, Control control)
    {
        if (!double.IsNaN(control.Width) && !LocalBindings.IsBound(control, Layoutable.WidthProperty))
        {
            style.Append(CultureInfo.InvariantCulture, $"width:{CssValues.Px(control.Width)};");
        }

        if (!double.IsNaN(control.Height) && !LocalBindings.IsBound(control, Layoutable.HeightProperty))
        {
            style.Append(CultureInfo.InvariantCulture, $"height:{CssValues.Px(control.Height)};");
        }
    }

    private static void Effects(StringBuilder style, Control control, IReadOnlySet<string> animated)
    {
        var opacity = Entrances.Opacity(control);
        if (opacity < 1 && !animated.Contains("opacity") && !BoundStyles.Opacity(control))
        {
            style.Append(CultureInfo.InvariantCulture, $"opacity:{CssValues.Num(opacity)};");
        }

        var bound = BoundStyles.Transform(control);
        var settled = Entrances.Matrix(control.RenderTransform);
        if ((bound || settled is { IsIdentity: false }) && control.Parent is not LayoutTransformControl)
        {
            style.Append(bound ? string.Empty : $"transform:{CssValues.Matrix(settled!.Value)};");
            style.Append(control.RenderTransformOrigin == RelativePoint.Center ? string.Empty : $"transform-origin:{CssValues.Origin(control.RenderTransformOrigin)};");
        }

        style.Append(Clips(control) ? "overflow:hidden;" : string.Empty);
        style.Append(control.ZIndex != 0 ? $"z-index:{control.ZIndex.ToString(CultureInfo.InvariantCulture)};" : string.Empty);
    }

    private static bool Clips(Control control) =>
        control.ClipToBounds
        && control.GetDiagnostic(Visual.ClipToBoundsProperty).Priority is BindingPriority.LocalValue or BindingPriority.StyleTrigger or BindingPriority.Template or BindingPriority.Style;


    private static void Placement(StringBuilder style, Control control, ParentKind parent)
    {
        switch (parent)
        {
            case ParentKind.Overlay:
                style.Append("grid-area:1/1;");
                InCell(style, control);
                break;
            case ParentKind.Dock:
                style.Append(DockAreas.Area(control));
                InCell(style, control);
                break;
            case ParentKind.Relative:
                style.Append(RelativePlacement.Of(control));
                break;
            case ParentKind.Column or ParentKind.Single:
                InColumn(style, control, parent);
                break;
            case ParentKind.Row or ParentKind.Wrap:
                InRow(style, control, parent);
                break;
            case ParentKind.Uniform:
                InCell(style, control);
                break;
            case ParentKind.Grid:
                var columns = Math.Max(1, GridOf(control)?.ColumnDefinitions.Count ?? 1);
                var rows = Math.Max(1, GridOf(control)?.RowDefinitions.Count ?? 1);
                var column = Math.Clamp(Grid.GetColumn(control), 0, columns - 1);
                var row = Math.Clamp(Grid.GetRow(control), 0, rows - 1);
                style.Append(CultureInfo.InvariantCulture, $"grid-column:{column + 1} / span {Math.Clamp(Grid.GetColumnSpan(control), 1, columns - column)};");
                style.Append(CultureInfo.InvariantCulture, $"grid-row:{row + 1} / span {Math.Clamp(Grid.GetRowSpan(control), 1, rows - row)};");
                InCell(style, control);
                break;
        }
    }

    /// <summary>
    /// The grid that places the control: the panel that holds it, which for an item of an <c>ItemsControl</c> whose
    /// panel is a <c>Grid</c> is that panel (its visual parent), not the items control (its logical parent).
    /// </summary>
    public static Grid? GridOf(Control control) => control.GetVisualParent() as Grid ?? control.Parent as Grid;

    /// <summary>The only child's stretch in a parent that bounds its height: its slot, never more (Tailwind's <c>flex-1 min-h-0</c>), or its content where the parent has no height.</summary>
    public const string Fills = "flex:1 1 0%;min-height:0;";

    /// <summary>The only child's stretch in a scroll viewer, which measures it with no height: at least the viewport, as tall as its content.</summary>
    public const string Grows = "flex:1 0 auto;";

    /// <summary>
    /// Whether the control is the content of a scroll viewer, which Avalonia measures with an infinite height, or a part of
    /// a scroll viewer's own template, which the web writes inside the viewer's element where the viewer scrolls them.
    /// </summary>
    private static bool Scrolled(Control control) =>
        control.TemplatedParent is global::Avalonia.Controls.ScrollViewer
        || control.GetVisualParent() is global::Avalonia.Controls.Presenters.ScrollContentPresenter
        || control.GetVisualParent() is global::Avalonia.Controls.Presenters.ContentPresenter { } presenter && presenter.GetVisualParent() is global::Avalonia.Controls.Presenters.ScrollContentPresenter;

    private static void InColumn(StringBuilder style, Control control, ParentKind parent)
    {
        if (!CentersByMaxWidth(control) && HorizontalSelf(control) is { } align)
        {
            style.Append(CultureInfo.InvariantCulture, $"align-self:{align};");
        }

        if (parent != ParentKind.Single)
        {
            return;
        }

        style.Append(Placed(control) switch
        {
            VerticalAlignment.Stretch when double.IsNaN(control.Height) => Scrolled(control) ? Grows : Fills,
            VerticalAlignment.Bottom => "margin-top:auto;",
            VerticalAlignment.Center => "margin-top:auto;margin-bottom:auto;",
            VerticalAlignment.Stretch when control.Margin == default => "margin-top:auto;margin-bottom:auto;",
            _ => string.Empty,
        });
    }

    /// <summary>
    /// A control's vertical place in its only parent: a content presenter's <c>VerticalContentAlignment</c> where it is
    /// not stretched (Avalonia's <c>ContentPresenter</c> arranges its child at its desired height there), else its own.
    /// </summary>
    private static VerticalAlignment Placed(Control control) =>
        control.GetVisualParent() is global::Avalonia.Controls.Presenters.ContentPresenter { VerticalContentAlignment: not VerticalAlignment.Stretch and var content }
        && control.VerticalAlignment == VerticalAlignment.Stretch
            ? content
            : control.VerticalAlignment;

    private static void InRow(StringBuilder style, Control control, ParentKind parent)
    {
        if (VerticalSelf(control) is { } align)
        {
            style.Append(CultureInfo.InvariantCulture, $"align-self:{align};");
        }

        if (parent == ParentKind.Row && control is global::Avalonia.Controls.Presenters.ContentPresenter { Child: Control { Width: not double.NaN } })
        {
            style.Append("flex:0 0 auto;");
        }
        else if (parent == ParentKind.Row && !double.IsNaN(control.Width))
        {
            style.Append("flex-shrink:0;");
        }
    }

    private static void InCell(StringBuilder style, Control control)
    {
        if (HorizontalSelf(control) is { } justify)
        {
            style.Append(CultureInfo.InvariantCulture, $"justify-self:{justify.Replace("flex-", string.Empty, StringComparison.Ordinal)};");
        }

        if (VerticalSelf(control) is { } align)
        {
            style.Append(CultureInfo.InvariantCulture, $"align-self:{align.Replace("flex-", string.Empty, StringComparison.Ordinal)};");
        }
    }

    private static string? HorizontalSelf(Control control) =>
        control.HorizontalAlignment == HorizontalAlignment.Stretch && !double.IsNaN(control.Width) ? "center" : Horizontal(control.HorizontalAlignment);

    private static string? VerticalSelf(Control control) =>
        control.VerticalAlignment == VerticalAlignment.Stretch && !double.IsNaN(control.Height) ? "center" : Vertical(control.VerticalAlignment);

    private static string? Horizontal(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Left => "flex-start",
        HorizontalAlignment.Center => "center",
        HorizontalAlignment.Right => "flex-end",
        _ => null,
    };

    private static string? Vertical(VerticalAlignment alignment) => alignment switch
    {
        VerticalAlignment.Top => "flex-start",
        VerticalAlignment.Center => "center",
        VerticalAlignment.Bottom => "flex-end",
        _ => null,
    };
}
