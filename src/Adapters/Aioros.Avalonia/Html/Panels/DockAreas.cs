using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Markup;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Panels;

internal static class DockAreas
{
    /// <summary>Whether a panel is written as a <c>DockPanel</c>: one, or a library's panel that asks for it with <c>a:Web.Layout="Dock"</c>.</summary>
    public static bool Docks(Panel panel) => panel is DockPanel || Web.GetLayout(panel) == "Dock";

    public static void Layout(Panel panel, StringBuilder style)
    {
        var (top, left, right, bottom) = Counts(panel, panel.Children.Count);
        var columns = Tracks(left, right);
        var rows = Tracks(top, bottom);
        style.Append(CultureInfo.InvariantCulture, $"display:grid;min-width:0;grid-template-columns:{columns};grid-template-rows:{rows};");
        if (panel is DockPanel dock)
        {
            style.Append(CultureInfo.InvariantCulture, $"column-gap:{CssValues.Px(dock.HorizontalSpacing)};row-gap:{CssValues.Px(dock.VerticalSpacing)};");
        }
    }

    public static string Area(Control child)
    {
        if (child.GetVisualParent() is not Panel panel || !Docks(panel))
        {
            return string.Empty;
        }

        var index = panel.Children.IndexOf(child);
        var (top, left, right, bottom) = Counts(panel, panel.Children.Count);
        var (topBefore, leftBefore, rightBefore, bottomBefore) = Counts(panel, index);
        var (rowStart, rowEnd) = (topBefore + 1, top + bottom + 2 - bottomBefore);
        var (columnStart, columnEnd) = (leftBefore + 1, left + right + 2 - rightBefore);
        switch (Side(panel, index))
        {
            case Dock.Top:
                rowEnd = rowStart + 1;
                break;
            case Dock.Bottom:
                rowStart = rowEnd - 1;
                break;
            case Dock.Left:
                columnEnd = columnStart + 1;
                break;
            case Dock.Right:
                columnStart = columnEnd - 1;
                break;
            default:
                break;
        }

        return string.Create(CultureInfo.InvariantCulture, $"grid-area:{rowStart}/{columnStart}/{rowEnd}/{columnEnd};{Unspaced(panel, child, index)}");
    }

    /// <summary>
    /// Without a filling child, the grid's free track between the sides would add one spacing more than Avalonia, which
    /// takes the spacing after the last docked child off: the child next to that track gives it back through a negative
    /// margin, the last top child (or else the innermost bottom one) vertically and the last left child (or else the
    /// innermost right one) horizontally, so the panel's content is as tall and wide as Avalonia measures it and a free
    /// track keeps its place.
    /// </summary>
    private static string Unspaced(Panel panel, Control child, int index)
    {
        if (panel is not DockPanel dock || Side(panel, panel.Children.Count - 1) is null)
        {
            return string.Empty;
        }

        var sides = Enumerable.Range(0, panel.Children.Count).Select(at => Side(panel, at)).ToList();
        var style = new StringBuilder();
        if (dock.VerticalSpacing > 0 && Inner(sides, Dock.Top, Dock.Bottom) is var (vertical, top) && vertical == index)
        {
            style.Append(CultureInfo.InvariantCulture, $"margin-{(top ? "bottom" : "top")}:{CssValues.Px((top ? child.Margin.Bottom : child.Margin.Top) - dock.VerticalSpacing)};");
        }

        if (dock.HorizontalSpacing > 0 && Inner(sides, Dock.Left, Dock.Right) is var (horizontal, left) && horizontal == index)
        {
            style.Append(CultureInfo.InvariantCulture, $"margin-{(left ? "right" : "left")}:{CssValues.Px((left ? child.Margin.Right : child.Margin.Left) - dock.HorizontalSpacing)};");
        }

        return style.ToString();
    }

    /// <summary>The child next to the free track on an axis: the last docked at its start, or else the last docked at its end; -1 for none.</summary>
    private static (int Index, bool Start) Inner(List<Dock?> sides, Dock start, Dock end) =>
        sides.LastIndexOf(start) is >= 0 and var first ? (first, true) : (sides.LastIndexOf(end), false);

    private static Dock? Side(Panel panel, int index) =>
        (panel as DockPanel)?.LastChildFill != false && index == panel.Children.Count - 1 ? null : DockPanel.GetDock(panel.Children[index]);

    private static (int Top, int Left, int Right, int Bottom) Counts(Panel panel, int count)
    {
        var sides = Enumerable.Range(0, count).Select(index => Side(panel, index)).ToList();
        return (sides.Count(side => side == Dock.Top), sides.Count(side => side == Dock.Left), sides.Count(side => side == Dock.Right), sides.Count(side => side == Dock.Bottom));
    }

    private static string Tracks(int before, int after) =>
        string.Join(' ', Enumerable.Repeat("auto", before).Append("minmax(0,1fr)").Concat(Enumerable.Repeat("auto", after)));
}
