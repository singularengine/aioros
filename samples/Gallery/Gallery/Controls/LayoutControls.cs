using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Gallery.Controls;

public sealed class Badge : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Badge, string?>(nameof(Label));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}

public sealed class Sparkline : Control
{
    private static readonly double[] Values = [0.2, 0.5, 0.35, 0.8, 0.6, 0.9, 0.4, 0.7];

    public override void Render(DrawingContext context)
    {
        var pen = new Pen(Brushes.SeaGreen, 2);
        var step = Bounds.Width / (Values.Length - 1);
        for (var index = 1; index < Values.Length; index++)
        {
            var from = new Point((index - 1) * step, Bounds.Height * (1 - Values[index - 1]));
            var to = new Point(index * step, Bounds.Height * (1 - Values[index]));
            context.DrawLine(pen, from, to);
        }
    }
}
