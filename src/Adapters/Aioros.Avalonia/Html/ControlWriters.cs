using System.Collections.Immutable;
using Aioros.Avalonia.Html.Families;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html;

internal static class ControlWriters
{
    public static ImmutableArray<IControlWriter> All { get; } =
    [
        .. TextFamily.Writers,
        .. RangesFamily.Writers,
        .. ItemsFamily.Writers,
        .. NavigationFamily.Writers,
        .. MediaFamily.Writers,
        .. LayoutFamily.Writers,
        .. ButtonsFamily.Writers,
    ];

    public static IControlWriter? For(Control control) => All.FirstOrDefault(writer => writer.Handles(control));
}
