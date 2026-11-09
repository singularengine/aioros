using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Aioros.Avalonia.Tests.Models;

public sealed class Badge : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Badge, string?>(nameof(Label));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}

/// <summary>A library panel with a styled property that sets a pseudo-class, as Aioros.Cosmo's <c>Field.IsInvalid</c> sets <c>:invalid</c>.</summary>
public sealed class Flagged : StackPanel
{
    public static readonly StyledProperty<bool> IsInvalidProperty = AvaloniaProperty.Register<Flagged, bool>(nameof(IsInvalid));

    public bool IsInvalid
    {
        get => GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(":invalid", IsInvalid);
        }
    }
}

public sealed class HalfSwatch : Control
{
    public override void Render(DrawingContext context) => context.FillRectangle(Brushes.Red, new Rect(0, 0, Bounds.Width / 2, Bounds.Height));
}

public sealed class DashedLabel : Control
{
    public override void Render(DrawingContext context)
    {
        context.DrawLine(new Pen(Brushes.Blue, 2, new DashStyle([3, 1], 0), PenLineCap.Round), new Point(0, 5), new Point(40, 5));
        using (context.PushTransform(Matrix.CreateTranslation(4, 10)))
        using (context.PushOpacity(0.5))
        {
            context.DrawText(new FormattedText("Hi", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.Green), default);
        }
    }
}

public sealed class ConicSwatch : Control
{
    public override void Render(DrawingContext context) =>
        context.FillRectangle(new ConicGradientBrush { GradientStops = [new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1)] }, new Rect(Bounds.Size));
}

public sealed class GradientDrawn : Control
{
    public override void Render(DrawingContext context)
    {
        var stops = new GradientStops { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1) };
        context.DrawRectangle(new LinearGradientBrush { StartPoint = RelativePoint.TopLeft, EndPoint = RelativePoint.BottomRight, GradientStops = stops }, null, new Rect(0, 0, 20, 10));
        context.DrawEllipse(null, new Pen(new RadialGradientBrush { GradientStops = stops }, 2), new Point(30, 5), 4, 4);
    }
}

public sealed class CustomDrawn : Control
{
    public override void Render(DrawingContext context) => context.Custom(new Operation(new Rect(Bounds.Size)));

    private sealed class Operation(Rect bounds) : global::Avalonia.Rendering.SceneGraph.ICustomDrawOperation
    {
        public Rect Bounds => bounds;

        public bool HitTest(Point p) => false;

        public bool Equals(global::Avalonia.Rendering.SceneGraph.ICustomDrawOperation? other) => ReferenceEquals(this, other);

        public void Render(global::Avalonia.Media.ImmediateDrawingContext context) => context.FillRectangle(new global::Avalonia.Media.Immutable.ImmutableSolidColorBrush(Colors.Red), bounds);

        public void Dispose()
        {
        }
    }
}

public sealed class BitmapDrawn : Control
{
    public override void Render(DrawingContext context)
    {
        using var bitmap = new global::Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), global::Avalonia.Platform.PixelFormats.Bgra8888, global::Avalonia.Platform.AlphaFormat.Premul);
        context.DrawImage(bitmap, new Rect(Bounds.Size));
    }
}

public sealed class RoundClipped : Control
{
    public override void Render(DrawingContext context)
    {
        using (context.PushClip(new RoundedRect(new Rect(Bounds.Size), 2)))
        {
            context.FillRectangle(Brushes.Red, new Rect(Bounds.Size));
        }
    }
}

public sealed class Greedy : Control
{
    protected override Size MeasureOverride(Size availableSize) => availableSize;

    public override void Render(DrawingContext context) => context.FillRectangle(Brushes.Red, new Rect(Bounds.Size));
}
