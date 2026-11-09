using Aioros.Markup;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;

namespace Aioros.Targets.Desktop.Motion;

/// <summary>
/// Moves a panel marked <c>a:Web.Marquee</c> left by half its width every period, without end, as the web does
/// (DESKTOP-103): an Avalonia animation of its translation, started again when its width changes.
/// </summary>
internal sealed class Marquees
{
    private static readonly AttachedProperty<double> OffsetProperty = AvaloniaProperty.RegisterAttached<Marquees, Control, double>("Offset");

    private static readonly AttachedProperty<CancellationTokenSource?> RunningProperty = AvaloniaProperty.RegisterAttached<Marquees, Control, CancellationTokenSource?>("Running");

    private static readonly AttachedProperty<double> WidthProperty = AvaloniaProperty.RegisterAttached<Marquees, Control, double>("Width");

    static Marquees() => OffsetProperty.Changed.AddClassHandler<Control>(static (control, change) =>
    {
        if (control.RenderTransform is TranslateTransform move)
        {
            move.X = change.GetNewValue<double>();
            control.InvalidateVisual();
        }
    });

    private Marquees()
    {
    }

    public static void Attach(Window window) =>
        window.LayoutUpdated += (_, _) =>
        {
            foreach (var panel in window.GetLogicalDescendants().OfType<Control>().Where(control => Web.GetMarquee(control) > 0 && control.Bounds.Width > 0 && Math.Abs(control.GetValue(WidthProperty) - control.Bounds.Width) > 0.5))
            {
                Start(panel);
            }
        };

    private static void Start(Control panel)
    {
        panel.GetValue(RunningProperty)?.Cancel();
        panel.SetValue(WidthProperty, panel.Bounds.Width);
        panel.RenderTransform = new TranslateTransform();
        var loop = new Animation
        {
            Duration = TimeSpan.FromSeconds(Web.GetMarquee(panel)),
            IterationCount = IterationCount.Infinite,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OffsetProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OffsetProperty, -panel.Bounds.Width / 2) } },
            },
        };
        var running = new CancellationTokenSource();
        panel.SetValue(RunningProperty, running);
        panel.DetachedFromLogicalTree += (_, _) => running.Cancel();
        _ = loop.RunAsync(panel, running.Token);
    }
}
