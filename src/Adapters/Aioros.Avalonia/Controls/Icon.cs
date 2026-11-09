using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Aioros.Avalonia.Controls;

public sealed class Icon : Control
{
    public static readonly StyledProperty<string?> KindProperty = AvaloniaProperty.Register<Icon, string?>(nameof(Kind));

    public static readonly StyledProperty<double> SizeProperty = Aioros.Markup.IconStyle.SizeProperty.AddOwner<Icon>();

    public static readonly StyledProperty<double> StrokeWidthProperty = AvaloniaProperty.Register<Icon, double>(nameof(StrokeWidth), 2);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Icon>();

    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var glyph = IconGlyphs.Of(Kind);
        if (glyph.IsEmpty || Foreground is not { } brush)
        {
            return;
        }

        var scale = Size / 24;
        var pen = new Pen(brush, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            foreach (var stroke in glyph)
            {
                using (context.PushOpacity(stroke.Opacity))
                {
                    context.DrawGeometry(stroke.Filled ? brush : null, stroke.Stroked ? pen : null, stroke.Geometry);
                }
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    static Icon() => AffectsRender<Icon>(KindProperty, SizeProperty, StrokeWidthProperty, ForegroundProperty);
}
