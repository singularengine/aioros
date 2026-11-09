using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Motion;

/// <summary>
/// Plays the style animations of a control marked <c>a:Web.Reveal</c> again when it first scrolls into its scroll viewer's
/// view, as the web plays them (DESKTOP-100): a control below the view when its page lays out ran them out of sight, so they
/// start over once, when the view reaches a tenth above its bottom edge; a control in view keeps the run it opened with.
/// </summary>
internal sealed class Reveals
{
    private static readonly AttachedProperty<RevealState> StateProperty =
        AvaloniaProperty.RegisterAttached<Reveals, Control, RevealState>("State");

    private Reveals()
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

        foreach (var control in viewer.GetVisualDescendants().OfType<Control>().Where(Web.GetReveal))
        {
            var state = control.GetValue(StateProperty);
            if (state == RevealState.Played)
            {
                continue;
            }

            var shown = InView(control, viewer);
            if (state == RevealState.Waiting && shown)
            {
                Replay(control);
            }

            control.SetValue(StateProperty, shown ? RevealState.Played : RevealState.Waiting);
        }
    }

    private static bool InView(Control control, ScrollViewer viewer)
    {
        if (control.TransformToVisual(viewer) is not { } transform)
        {
            return false;
        }

        var bounds = new Rect(control.Bounds.Size).TransformToAABB(transform);
        return bounds.Bottom > 0 && bounds.Top < viewer.Viewport.Height * 0.9;
    }

    private static void Replay(Control control)
    {
        var classes = control.Classes.Where(name => !name.StartsWith(':')).ToList();
        control.Classes.RemoveAll(classes);
        control.Classes.AddRange(classes);
    }

    private enum RevealState
    {
        Unseen,
        Waiting,
        Played,
    }
}
