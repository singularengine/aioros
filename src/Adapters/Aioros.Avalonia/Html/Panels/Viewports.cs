using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Panels;

internal static class Viewports
{
    public static Size Of(Visual visual) => TopLevel.GetTopLevel(visual) is { } root
        ? new Size(double.IsNaN(root.Width) ? 1280 : root.Width, double.IsNaN(root.Height) ? 800 : root.Height)
        : new Size(1280, 800);
}
