using Aioros.Avalonia.Html.Looks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// Realizes an overlay's surface as Avalonia shows it (a flyout's or a menu's presenter, a context menu, a tool tip) for
/// the time it is read, in a host lent to the nearest panel around the control it opens at, so it is styled as a popup
/// opened there is (the page's styles and resources, its theme variant, the control's data context), templated and laid
/// out at its own size, without showing the window or opening a popup: the rest of the page is written from the same
/// state as without overlays. The host is taken back afterwards (the window's overlay layer hosts it when no panel is
/// around the control).
/// </summary>
internal static class Probes
{
    public static void With(Control anchor, Control surface, Action<Control?> read)
    {
        var layer = anchor.GetSelfAndVisualAncestors().OfType<Panel>().FirstOrDefault(panel => !panel.IsItemsHost) ?? OverlayLayer.GetOverlayLayer(anchor);
        if (surface.Parent is not null || surface.GetVisualParentOrNull() is not null || layer is null)
        {
            read(null);
            return;
        }

        var host = new Panel { DataContext = anchor.DataContext };
        host.Children.Add(surface);
        layer.Children.Add(host);
        try
        {
            Settle(surface);
            read(surface);
        }
        finally
        {
            layer.Children.Remove(host);
            host.Children.Remove(surface);
        }
    }

    /// <summary>Styles, templates and lays out a surface at its own size, with the work its parts queued run.</summary>
    public static void Settle(Control surface)
    {
        ThemeLooks.ExpandTemplates(surface);
        Dispatcher.UIThread.RunJobs();
        ThemeLooks.ExpandTemplates(surface);
        surface.Measure(Size.Infinity);
        surface.Arrange(new Rect(surface.DesiredSize));
    }

    /// <summary>Marks a part and its ancestors to be measured again: a probe measures its surface itself, without the layout manager that would.</summary>
    public static void Invalidate(Visual part)
    {
        foreach (var layoutable in part.GetSelfAndVisualAncestors().OfType<Layoutable>())
        {
            layoutable.InvalidateMeasure();
        }
    }

    /// <summary>Whether a part shows in its surface: it and every element up to the surface are visible (a probe's window is not shown).</summary>
    public static bool Shows(Visual part, Visual surface)
    {
        var current = (Visual?)part;
        while (current is not null && !ReferenceEquals(current, surface))
        {
            if (!current.IsVisible)
            {
                return false;
            }

            current = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(current);
        }

        return surface.IsVisible;
    }

    private static global::Avalonia.Visual? GetVisualParentOrNull(this Control control) => global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(control);
}
