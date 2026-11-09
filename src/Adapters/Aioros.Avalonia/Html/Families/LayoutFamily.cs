using System.Collections.Immutable;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Html.Vectors;
using Aioros.Avalonia.Html.Writers;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Families;

internal static class LayoutFamily
{
    public static ImmutableArray<IControlWriter> Writers { get; } =
    [
        new ControlWriter<global::Avalonia.Controls.Primitives.ScrollBar>(static (_, _, _) => { }),
        new ControlWriter<GridSplitter>(SplitterWriters.Write) { Rooted = true },
        new ControlWriter<Separator>(LayoutWriters.WriteSeparator) { Rooted = true },
        new ControlWriter<GroupBox>(LayoutWriters.WriteGroupBox) { Rooted = true },
        new ControlWriter<ThemeVariantScope>(LayoutWriters.WriteThemeScope) { Rooted = true },
        new ControlWriter<ExperimentalAcrylicBorder>(LayoutWriters.WriteAcrylic) { Rooted = true },
        new ControlWriter<LayoutTransformControl>(LayoutWriters.WriteLayoutTransform),
        new ControlWriter<Control>(WrittenSlots.Write, WrittenSlots.Handles),
        new ControlWriter<Control>(LayoutWriters.WriteDrawing, Drawings.IsSelfDrawn),
    ];

    public static ImmutableArray<IFieldBinder> Binders { get; } = [new FieldBinder<Panel>(Variants.BrowserFlags.Bind), new FieldBinder<GridSplitter>(SplitterWriters.Bind)];
}
