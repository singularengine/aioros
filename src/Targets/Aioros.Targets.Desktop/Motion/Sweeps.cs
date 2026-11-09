using Aioros.Markup;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;

namespace Aioros.Targets.Desktop.Motion;

/// <summary>
/// Sweeps the gradient foreground of a text marked <c>a:Web.Sweep</c> across its glyphs, as the web does (DESKTOP-101): the
/// text gets its own copy of the gradient, drawn twice as wide and repeating, moved one whole gradient to the right every
/// period by an Avalonia animation without end, so the shared brush it came from is left as it was.
/// </summary>
internal sealed class Sweeps
{
    private static readonly AttachedProperty<double> OffsetProperty = AvaloniaProperty.RegisterAttached<Sweeps, TextBlock, double>("Offset");

    static Sweeps() => OffsetProperty.Changed.AddClassHandler<TextBlock>(static (text, change) =>
    {
        if (text.Foreground is Brush { Transform: TransformGroup group } && group.Children.OfType<TranslateTransform>().FirstOrDefault() is { } move)
        {
            move.X = change.GetNewValue<double>();
            text.InvalidateVisual();
        }
    });

    private Sweeps()
    {
    }

    public static void Attach(Window window) =>
        window.LayoutUpdated += (_, _) =>
        {
            foreach (var text in window.GetLogicalDescendants().OfType<TextBlock>().Where(text => Web.GetSweep(text) > 0 && text.Bounds.Width > 0 && text.Foreground is GradientBrush and not GradientBrush { Transform: TransformGroup }))
            {
                Start(text);
            }
        };

    private static void Start(TextBlock text)
    {
        var brush = Copy((GradientBrush)text.Foreground!);
        var move = new TranslateTransform();
        brush.Transform = new TransformGroup { Children = { new ScaleTransform(2, 1), move } };
        brush.TransformOrigin = RelativePoint.TopLeft;
        brush.SpreadMethod = GradientSpreadMethod.Repeat;
        text.Foreground = brush;
        var sweep = new Animation
        {
            Duration = TimeSpan.FromSeconds(Web.GetSweep(text)),
            IterationCount = IterationCount.Infinite,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OffsetProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OffsetProperty, text.Bounds.Width * 2) } },
            },
        };
        var running = new CancellationTokenSource();
        text.DetachedFromLogicalTree += (_, _) =>
        {
            running.Cancel();
            running.Dispose();
        };
        _ = sweep.RunAsync(text, running.Token);
    }

    private static GradientBrush Copy(GradientBrush source)
    {
        GradientBrush copy = source switch
        {
            LinearGradientBrush linear => new LinearGradientBrush { StartPoint = linear.StartPoint, EndPoint = linear.EndPoint },
            RadialGradientBrush radial => new RadialGradientBrush { Center = radial.Center, GradientOrigin = radial.GradientOrigin, RadiusX = radial.RadiusX, RadiusY = radial.RadiusY },
            ConicGradientBrush conic => new ConicGradientBrush { Center = conic.Center, Angle = conic.Angle },
            _ => new LinearGradientBrush(),
        };
        foreach (var stop in source.GradientStops)
        {
            copy.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));
        }

        copy.Opacity = source.Opacity;
        return copy;
    }
}
