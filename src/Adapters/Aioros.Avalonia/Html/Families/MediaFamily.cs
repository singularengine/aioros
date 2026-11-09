using System.Collections.Immutable;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Writers;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Aioros.Avalonia.Html.Families;

internal static class MediaFamily
{
    public static ImmutableArray<IControlWriter> Writers { get; } =
    [
        new ControlWriter<Aioros.Avalonia.Controls.Icon>(InputWriters.WriteIcon) { Rooted = true },
        new ControlWriter<Aioros.Avalonia.Controls.Chart>(ChartWriters.WriteChart),
        new ControlWriter<Canvas>(CanvasWriters.WriteCanvas),
        new ControlWriter<PathIcon>(ShapeWriters.WritePathIcon),
        new ControlWriter<Image>(MediaWriters.WriteImage) { Rooted = true },
        new ControlWriter<Viewbox>(MediaWriters.WriteViewbox),
        new ControlWriter<Rectangle>(ShapeWriters.WriteBox) { Rooted = true },
        new ControlWriter<Ellipse>(ShapeWriters.WriteBox) { Rooted = true },
        new ControlWriter<Shape>(ShapeWriters.WriteGeometry),
    ];

    public static ImmutableArray<IFieldBinder> Binders { get; } = [];
}
