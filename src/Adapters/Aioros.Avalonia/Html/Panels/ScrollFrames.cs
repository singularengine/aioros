using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Markup;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Panels;

/// <summary>
/// A scroll viewer whose own control theme frames its content in a border (Aioros.Cosmo's scroll area, Radix's): one
/// scrolling box with the frame's fill, border and radius, scrolling on the axes its bar visibilities allow, its bars
/// thin in the thumb's color, and its content written in it at its own size on the scrolling axis, as the scroll
/// content presenter measures it. A scroll viewer of Fluent's theme, or whose template is not framed, is written as before.
/// </summary>
internal static class ScrollFrames
{
    public static Border? Frame(ScrollViewer scroll) =>
        !FluentThemes.Owns(scroll, typeof(ScrollViewer)) && scroll.GetVisualChildren().FirstOrDefault() is Border frame
        && frame.GetVisualDescendants().OfType<ScrollContentPresenter>().Any()
            ? frame
            : null;

    public static string Layout(ScrollViewer scroll, Border frame, StringBuilder style, StyleSheet sheet)
    {
        BoxStyles.Fill(style, frame.Background, frame.BorderBrush, frame.BorderThickness, frame.CornerRadius, frame.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault()?.Padding ?? default);
        var horizontal = Scrolls(scroll.HorizontalScrollBarVisibility);
        style.Append(CultureInfo.InvariantCulture, $"overflow-x:{(horizontal ? "auto" : "hidden")};overflow-y:{(Scrolls(scroll.VerticalScrollBarVisibility) ? "auto" : "hidden")};");
        var hover = Bars(scroll, frame, style, sheet);
        if (!horizontal)
        {
            return CssValues.Join("c", hover);
        }

        sheet.AddRule("sxw", ".sxw>*{min-width:max-content}\n");
        return CssValues.Join("c sxw", hover);
    }

    /// <summary>
    /// The viewer's thin bars in its thumb's color, or, when its theme hides its bars at rest (Radix's <c>type="hover"</c>),
    /// transparent until the pointer is over it and transparent again <see cref="Web.ScrollbarHideDelayProperty"/> after it
    /// leaves; returns the class of the hover rule, if any.
    /// </summary>
    private static string? Bars(ScrollViewer scroll, Border frame, StringBuilder style, StyleSheet sheet)
    {
        if (Barless(scroll))
        {
            style.Append("scrollbar-width:none;");
            return null;
        }

        if (Thumb(scroll, frame) is not { } thumb)
        {
            return null;
        }

        if (!HiddenAtRest(scroll, frame))
        {
            style.Append(CultureInfo.InvariantCulture, $"scrollbar-width:thin;scrollbar-color:{thumb} transparent;");
            return null;
        }

        style.Append(CultureInfo.InvariantCulture, $"scrollbar-width:thin;scrollbar-color:transparent transparent;transition:scrollbar-color 0s linear {Math.Max(0, Web.GetScrollbarHideDelay(scroll)).ToString(CultureInfo.InvariantCulture)}ms;");
        var name = "sbh" + CssValues.Hash(thumb);
        sheet.AddRule(name, $".{name}:hover{{scrollbar-color:{thumb} transparent;transition-delay:0s}}\n");
        return name;
    }

    /// <summary>Whether every bar of the viewer's own template is transparent at rest.</summary>
    private static bool HiddenAtRest(ScrollViewer scroll, Border frame) =>
        frame.GetVisualDescendants().OfType<ScrollBar>().Where(part => ReferenceEquals(part.TemplatedParent, scroll)).ToList() is { Count: > 0 } bars
        && bars.TrueForAll(bar => bar.Opacity == 0);

    public static Control? Content(Border frame) =>
        frame.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault()?.Child;

    /// <summary>Whether Avalonia scrolls an axis: every visibility but <c>Disabled</c>, a <c>Hidden</c> bar's axis included.</summary>
    public static bool Scrolls(ScrollBarVisibility visibility) => visibility is not ScrollBarVisibility.Disabled;

    /// <summary>Whether a viewer scrolls with no bar to show: an axis scrolls and every scrolling axis hides its bar.</summary>
    public static bool Barless(ScrollViewer scroll) =>
        (scroll.HorizontalScrollBarVisibility, scroll.VerticalScrollBarVisibility) is (ScrollBarVisibility.Hidden, ScrollBarVisibility.Hidden or ScrollBarVisibility.Disabled)
            or (ScrollBarVisibility.Disabled, ScrollBarVisibility.Hidden);

    /// <summary>The color of the viewer's own thumb, from its template's bars, never from a bar of a viewer its content holds.</summary>
    private static string? Thumb(ScrollViewer scroll, Border frame)
    {
        var bar = frame.GetVisualDescendants().OfType<ScrollBar>().FirstOrDefault(part => ReferenceEquals(part.TemplatedParent, scroll));
        if (bar is null)
        {
            return null;
        }

        ThemeLooks.ExpandTemplates(bar);
        return bar.GetVisualDescendants().OfType<Thumb>().FirstOrDefault() is { } thumb
            ? thumb.GetVisualDescendants().OfType<Border>().Select(part => CssValues.Brush(part.Background)).FirstOrDefault(color => color is not null)
                ?? CssValues.Brush(thumb.Background)
            : null;
    }
}
