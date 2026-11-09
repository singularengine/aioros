using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Motion;

/// <summary>
/// Shifts a layer marked <c>a:Web.Parallax</c> against its scroll viewer's scroll by its share of its distance from the
/// viewport's middle, as the web does (DESKTOP-105), through its render transform.
/// </summary>
internal sealed class Parallaxes
{
    private Parallaxes()
    {
    }

    public static void Attach(Window window) =>
        window.AddHandler(ScrollViewer.ScrollChangedEvent, (_, args) => Update(args.Source as ScrollViewer), RoutingStrategies.Bubble, handledEventsToo: true);

    private static void Update(ScrollViewer? viewer)
    {
        if (viewer is null || viewer.Viewport.Height <= 0)
        {
            return;
        }

        foreach (var layer in viewer.GetVisualDescendants().OfType<Control>().Where(control => !double.IsNaN(Web.GetParallax(control))))
        {
            var shift = layer.RenderTransform as TranslateTransform ?? new TranslateTransform();
            if (layer.TranslatePoint(new Point(0, layer.Bounds.Height / 2), viewer) is not { } middle)
            {
                continue;
            }

            var distance = middle.Y - shift.Y - (viewer.Viewport.Height / 2);
            shift.Y = Math.Round(-distance * Web.GetParallax(layer));
            layer.RenderTransform = shift;
        }
    }
}
