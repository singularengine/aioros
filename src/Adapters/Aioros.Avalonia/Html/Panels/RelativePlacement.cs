using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Panels;

internal static class RelativePlacement
{
    private static readonly AvaloniaProperty[] References =
    [
        RelativePanel.AboveProperty, RelativePanel.BelowProperty, RelativePanel.LeftOfProperty, RelativePanel.RightOfProperty,
        RelativePanel.AlignLeftWithProperty, RelativePanel.AlignRightWithProperty, RelativePanel.AlignTopWithProperty, RelativePanel.AlignBottomWithProperty,
        RelativePanel.AlignHorizontalCenterWithProperty, RelativePanel.AlignVerticalCenterWithProperty,
    ];

    public static void Layout(RelativePanel panel, StringBuilder style)
    {
        style.Append("position:relative;anchor-scope:all;");
        if (double.IsNaN(panel.Height))
        {
            panel.Measure(new Size(double.IsNaN(panel.Width) ? double.PositiveInfinity : panel.Width, double.PositiveInfinity));
            style.Append(CultureInfo.InvariantCulture, $"min-height:{CssValues.Px(panel.DesiredSize.Height - panel.Margin.Top - panel.Margin.Bottom)};");
        }
    }

    public static string Of(Control child)
    {
        if (child.GetVisualParent() is not RelativePanel panel)
        {
            return string.Empty;
        }

        var style = new StringBuilder("position:absolute;");
        if (panel.Children.Any(sibling => References.Any(property => ReferenceEquals(sibling.GetValue(property), child))))
        {
            style.Append(CultureInfo.InvariantCulture, $"anchor-name:{Anchor(panel, child)};");
        }

        Horizontal(panel, child, style);
        Vertical(panel, child, style);
        return style.ToString();
    }

    private static void Horizontal(RelativePanel panel, Control child, StringBuilder style)
    {
        var start = Edge(panel, child, RelativePanel.GetAlignLeftWithPanel(child), "left:0;", ("left", RelativePanel.AlignLeftWithProperty, "left"), ("left", RelativePanel.RightOfProperty, "right"));
        var end = Edge(panel, child, RelativePanel.GetAlignRightWithPanel(child), "right:0;", ("right", RelativePanel.AlignRightWithProperty, "right"), ("right", RelativePanel.LeftOfProperty, "left"));
        var center = RelativePanel.GetAlignHorizontalCenterWithPanel(child);
        var centerWith = Target(panel, child, RelativePanel.AlignHorizontalCenterWithProperty);
        style.Append(start).Append(end);
        if (center)
        {
            style.Append(double.IsNaN(child.Width) ? "left:0;right:0;margin-inline:auto;width:fit-content;" : "left:0;right:0;margin-inline:auto;");
        }
        else if (centerWith is not null)
        {
            style.Append(CultureInfo.InvariantCulture, $"position-anchor:{centerWith};justify-self:anchor-center;");
        }
        else if (start.Length == 0 && end.Length == 0)
        {
            style.Append("left:0;");
        }
    }

    private static void Vertical(RelativePanel panel, Control child, StringBuilder style)
    {
        var start = Edge(panel, child, RelativePanel.GetAlignTopWithPanel(child), "top:0;", ("top", RelativePanel.AlignTopWithProperty, "top"), ("top", RelativePanel.BelowProperty, "bottom"));
        var end = Edge(panel, child, RelativePanel.GetAlignBottomWithPanel(child), "bottom:0;", ("bottom", RelativePanel.AlignBottomWithProperty, "bottom"), ("bottom", RelativePanel.AboveProperty, "top"));
        var center = RelativePanel.GetAlignVerticalCenterWithPanel(child);
        var centerWith = Target(panel, child, RelativePanel.AlignVerticalCenterWithProperty);
        style.Append(start).Append(end);
        if (center)
        {
            style.Append(double.IsNaN(child.Height) ? "top:0;bottom:0;margin-block:auto;height:fit-content;" : "top:0;bottom:0;margin-block:auto;");
        }
        else if (centerWith is not null)
        {
            style.Append(CultureInfo.InvariantCulture, $"position-anchor:{centerWith};align-self:anchor-center;");
        }
        else if (start.Length == 0 && end.Length == 0)
        {
            style.Append("top:0;");
        }
    }

    private static string Edge(RelativePanel panel, Control child, bool withPanel, string panelEdge, params (string Inset, AvaloniaProperty Property, string Side)[] siblings)
    {
        if (withPanel)
        {
            return panelEdge;
        }

        foreach (var (inset, property, side) in siblings)
        {
            if (Target(panel, child, property) is { } anchor)
            {
                return $"{inset}:anchor({anchor} {side});";
            }
        }

        return string.Empty;
    }

    private static string? Target(RelativePanel panel, Control child, AvaloniaProperty property) =>
        child.GetValue(property) is Control target && panel.Children.Contains(target) ? Anchor(panel, target) : null;

    private static string Anchor(RelativePanel panel, Control target) =>
        string.IsNullOrEmpty(target.Name) ? "--a" + panel.Children.IndexOf(target).ToString(CultureInfo.InvariantCulture) : "--" + target.Name;
}
