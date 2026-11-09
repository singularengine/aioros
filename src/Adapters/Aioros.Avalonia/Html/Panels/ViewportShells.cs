using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;

namespace Aioros.Avalonia.Html.Panels;

/// <summary>
/// A window-filling layout whose <c>ScrollViewer</c> Avalonia gives the window's height: every parent from the window down
/// to it hands its children a bounded height (a split view, a dock panel's fill or side, a grid's star rows, a panel, a
/// border or a content control, each child stretched without its own height), so the viewer scrolls its content while
/// the rest of the window stays, or a library's control that says it does (<c>a:Web.Layout="Dock"</c> for a panel,
/// <c>"Fill"</c> for a content control), or a layout's outlet. On the web the document takes the viewport's height and that chain fills it, so the
/// viewer is the scroll container instead of the document. A vertical <c>StackPanel</c> or another scroll viewer measures
/// with an infinite height and breaks the chain.
/// </summary>
internal static class ViewportShells
{
    public const string Fill = "vf";

    public const string Scroll = "vs";

    public const string ScrollBoth = "vsx";

    public const string Css = "form:has(.vf){height:100dvh;min-height:0!important;overflow:hidden}\n.vf{min-height:0!important}\n.c>.vf{flex:1 1 0!important}\n.vs{overflow:hidden auto!important}\n.vsx{overflow:auto!important}\n";

    /// <summary>The role of each control of the tree that holds <paramref name="control"/>: a filling element or a scroll container.</summary>
    public static Dictionary<Control, string> Roles(Control control)
    {
        var root = control;
        while (root.Parent is Control parent and not TopLevel)
        {
            root = parent;
        }

        var roles = new Dictionary<Control, string>();
        foreach (var scroll in root.GetLogicalDescendants().Prepend(root).OfType<ScrollViewer>())
        {
            if (scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled || Chain(scroll) is not { } chain)
            {
                continue;
            }

            roles[scroll] = Fill + " " + (ScrollFrames.Scrolls(scroll.HorizontalScrollBarVisibility) ? ScrollBoth : Scroll);
            chain.ForEach(element => roles.TryAdd(element, Fill));
        }

        return roles;
    }

    private static List<Control>? Chain(ScrollViewer scroll)
    {
        var chain = new List<Control>();
        for (Control child = scroll; ; )
        {
            if (!Stretched(child))
            {
                return null;
            }

            switch (child.Parent)
            {
                case null or TopLevel:
                    return chain;
                case Control parent when Passes(parent, child):
                    chain.Add(parent);
                    child = parent;
                    break;
                default:
                    return null;
            }
        }
    }

    private static bool Stretched(Control control) =>
        control.IsVisible && control.VerticalAlignment == VerticalAlignment.Stretch && double.IsNaN(control.Height) && double.IsPositiveInfinity(control.MaxHeight);

    private static bool Passes(Control parent, Control child) => parent switch
    {
        SplitView or Controls.Outlet => true,
        DockPanel dock => (dock.LastChildFill && dock.Children.LastOrDefault() == child) || DockPanel.GetDock(child) is Dock.Left or Dock.Right,
        Panel library when DockAreas.Docks(library) => library.Children.LastOrDefault() == child || DockPanel.GetDock(child) is Dock.Left or Dock.Right,
        ContentControl library when Web.GetLayout(library) == "Fill" => true,
        Grid grid => grid.RowDefinitions.Count == 0 || grid.RowDefinitions
            .Skip(Math.Clamp(Grid.GetRow(child), 0, grid.RowDefinitions.Count - 1)).Take(Math.Max(1, Grid.GetRowSpan(child)))
            .All(row => row.Height.IsStar),
        StackPanel or ScrollViewer or ItemsControl => false,
        Panel => parent.GetType() == typeof(Panel),
        Decorator or UserControl or TransitioningContentControl => true,
        ContentControl => parent.GetType() == typeof(ContentControl),
        _ => false,
    };
}
