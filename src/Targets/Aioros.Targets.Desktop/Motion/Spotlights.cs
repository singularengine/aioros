using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Motion;

/// <summary>
/// Glows over a control marked <c>a:Web.Spotlight</c> where the pointer is, as the web does (DESKTOP-104): an adorner
/// over the control, which takes no input, filled with a radial gradient of the color centred on the pointer, removed
/// when the pointer leaves.
/// </summary>
internal sealed class Spotlights
{
    private const double Radius = 320;

    private static readonly AttachedProperty<Border?> GlowProperty = AvaloniaProperty.RegisterAttached<Spotlights, Control, Border?>("Glow");

    private Spotlights()
    {
    }

    public static void Attach(Window window)
    {
        window.AddHandler(InputElement.PointerMovedEvent, (_, args) => Moved(args), RoutingStrategies.Tunnel, handledEventsToo: true);
        window.AddHandler(InputElement.PointerExitedEvent, (_, args) => Left(args), RoutingStrategies.Direct | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void Moved(PointerEventArgs args)
    {
        var lit = (args.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(control => Web.GetSpotlight(control) is not null);
        if (lit is null || Web.GetSpotlight(lit) is not ISolidColorBrush color || AdornerLayer.GetAdornerLayer(lit) is null)
        {
            return;
        }

        var glow = lit.GetValue(GlowProperty);
        if (glow is null)
        {
            glow = new Border { IsHitTestVisible = false, CornerRadius = lit is Border border ? border.CornerRadius : default };
            lit.SetValue(GlowProperty, glow);
            AdornerLayer.SetAdorner(lit, glow);
            lit.PointerExited += (_, _) => Clear(lit);
        }

        var at = args.GetPosition(lit);
        glow.Background = new RadialGradientBrush
        {
            Center = new RelativePoint(at, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(at, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(Radius, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(Radius, RelativeUnit.Absolute),
            GradientStops = { new GradientStop(color.Color, 0), new GradientStop(Color.FromArgb(0, color.Color.R, color.Color.G, color.Color.B), 0.7) },
        };
    }

    private static void Left(PointerEventArgs args)
    {
        if (args.Source is Control control && control.GetValue(GlowProperty) is not null)
        {
            Clear(control);
        }
    }

    private static void Clear(Control lit)
    {
        if (lit.GetValue(GlowProperty) is not null)
        {
            AdornerLayer.SetAdorner(lit, null);
            lit.SetValue(GlowProperty, null);
        }
    }
}
