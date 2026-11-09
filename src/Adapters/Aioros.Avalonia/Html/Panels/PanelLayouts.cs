using Aioros.Markup;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Panels;

internal static class PanelLayouts
{
    /// <summary>A panel's layout: the CSS it adds to its box, its base class and how its children are placed in it.</summary>
    public static (string Classes, ParentKind Kind) Of(Control control, StringBuilder style, StyleSheet sheet)
    {
        switch (control)
        {
            case StackPanel stack:
                return Kept(Stack(stack.Orientation, stack.Spacing, stack is ReversibleStackPanel { ReverseOrder: true }, style), sheet);
            case VirtualizingStackPanel virtualizing:
                Realize(virtualizing);
                return Kept(Stack(virtualizing.Orientation, 0, false, style), sheet);
            case RelativePanel relative:
                RelativePlacement.Layout(relative, style);
                return (string.Empty, ParentKind.Relative);
            case UniformGrid uniform:
                PanelSurfaces.LayoutUniform(uniform, style);
                return (Stacked(uniform, sheet), ParentKind.Uniform);
            case WrapPanel wrap:
                style.Append(CultureInfo.InvariantCulture, $"flex-direction:row;flex-wrap:wrap;column-gap:{CssValues.Px(wrap.ItemSpacing)};row-gap:{CssValues.Px(wrap.LineSpacing)};");
                return ("c", ParentKind.Wrap);
            case Grid grid:
                return (GridTracks(grid, style, sheet), ParentKind.Grid);
            case ScrollViewer framed when ScrollFrames.Frame(framed) is { } frame:
                return (CssValues.Join(CssValues.Join(ScrollFrames.Layout(framed, frame, style, sheet), ScrollEnds.Layout(framed, sheet)), ScrollFades.Mark(framed, sheet)), ParentKind.Single);
            case ScrollViewer scroll:
                style.Append(ScrollFrames.Scrolls(scroll.HorizontalScrollBarVisibility) ? "overflow-x:auto;" : string.Empty);
                style.Append(ScrollFrames.Barless(scroll) ? "scrollbar-width:none;" : string.Empty);
                return (CssValues.Join(CssValues.Join("c", ScrollEnds.Layout(scroll, sheet)), ScrollFades.Mark(scroll, sheet)), ParentKind.Single);
            case Panel docked when DockAreas.Docks(docked):
                DockAreas.Layout(docked, style);
                return (string.Empty, ParentKind.Dock);
            case Panel panel:
                Realize(panel);
                style.Append("display:grid;min-width:0;grid-template:minmax(0,1fr)/minmax(0,1fr);");
                return (string.Empty, ParentKind.Overlay);
            default:
                return ("c", ParentKind.Single);
        }
    }

    private static string GridTracks(Grid grid, StringBuilder style, StyleSheet sheet)
    {
        style.Append("display:grid;min-width:0;");
        style.Append(CultureInfo.InvariantCulture, $"grid-template-columns:{Tracks(grid.ColumnDefinitions.Select(static definition => (definition.Width, LocalBindings.IsBound(definition, ColumnDefinition.WidthProperty))), "c")};");
        if (grid.RowDefinitions.Count > 0)
        {
            style.Append(CultureInfo.InvariantCulture, $"grid-template-rows:{Tracks(grid.RowDefinitions.Select(static definition => (definition.Height, LocalBindings.IsBound(definition, RowDefinition.HeightProperty))), "r")};");
        }

        style.Append(CultureInfo.InvariantCulture, $"column-gap:{CssValues.Px(grid.ColumnSpacing)};row-gap:{CssValues.Px(grid.RowSpacing)};");
        return CssValues.Join(Stacked(grid, sheet), grid.RowSpacing == 0 && grid.ColumnSpacing > 0 && Web.GetStackBelow(grid) is var stack && !double.IsNaN(stack) ? sheet.StackGap(stack, grid.ColumnSpacing) : null);
    }

    /// <summary>A grid's tracks, a bound one read from the custom property its inline style writes (<see cref="BoundStyles.Track"/>).</summary>
    private static string Tracks(IEnumerable<(GridLength Length, bool Bound)> tracks, string axis)
    {
        var list = tracks.ToList();
        return list.Count == 0 ? BoxStyles.Tracks([]) : string.Join(' ', list.Select((track, index) => track.Bound ? $"var({BoundStyles.Track(axis, index)})" : BoxStyles.Tracks([track.Length])));
    }

    private static string Stacked(Control grid, StyleSheet sheet) => Web.GetStackBelow(grid) is var stack && double.IsNaN(stack) ? string.Empty : sheet.StackBelow(stack);

    /// <summary>The rule of a horizontal stack (class <c>hs</c>) that keeps its children, and those of its items written <c>display:contents</c>, at their width.</summary>
    private const string KeptChildren = ".c.hs>*,.c.hs>:is(.li,[style*=\"display:contents\"])>*,.c.hs>:is(.li,[style*=\"display:contents\"])>:is(.li,[style*=\"display:contents\"])>*{flex-shrink:0}\n";

    /// <summary>
    /// A horizontal stack's layout with the shared class whose one rule keeps its children at their width, as Avalonia's
    /// <c>StackPanel</c> arranges each at its desired width whatever the room, so no child needs a style, and an element,
    /// of its own for it.
    /// </summary>
    private static (string Classes, ParentKind Kind) Kept((string Classes, ParentKind Kind) layout, StyleSheet sheet)
    {
        if (layout.Kind != ParentKind.Row)
        {
            return layout;
        }

        sheet.AddRule("hstack", KeptChildren);
        return (layout.Classes + " hs", layout.Kind);
    }

    public static (string Classes, ParentKind Kind) Stack(Orientation orientation, double spacing, bool reverse, StringBuilder style)
    {
        var row = orientation == Orientation.Horizontal;
        style.Append((row, reverse) switch
        {
            (true, true) => "flex-direction:row-reverse;justify-content:flex-end;",
            (true, false) => "flex-direction:row;",
            (false, true) => "flex-direction:column-reverse;justify-content:flex-end;",
            _ => string.Empty,
        });
        style.Append(spacing > 0 ? string.Create(CultureInfo.InvariantCulture, $"gap:{CssValues.Px(spacing)};") : string.Empty);
        return ("c", row ? ParentKind.Row : ParentKind.Column);
    }

    public static void Realize(Panel panel)
    {
        if (panel is not (VirtualizingStackPanel or VirtualizingCarouselPanel) || panel.Children.Count > 0
            || panel.GetVisualParent() is not ItemsPresenter { TemplatedParent: ItemsControl items })
        {
            return;
        }

        var indexes = items is Carousel carousel
            ? carousel.SelectedIndex >= 0 && carousel.SelectedIndex < items.ItemCount ? [carousel.SelectedIndex] : []
            : Enumerable.Range(0, items.ItemCount);
        var generator = items.ItemContainerGenerator;
        foreach (var index in indexes)
        {
            var item = items.Items[index];
            var container = generator.NeedsContainer(item, index, out var recycleKey) ? generator.CreateContainer(item, index, recycleKey) : (Control)item!;
            generator.PrepareItemContainer(container, item, index);
            panel.Children.Add(container);
            generator.ItemContainerPrepared(container, item, index);
        }

        ThemeLooks.ExpandTemplates(panel);
    }
}
