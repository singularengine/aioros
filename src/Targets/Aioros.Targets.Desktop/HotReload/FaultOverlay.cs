using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Aioros.Targets.Desktop.HotReload;

/// <summary>An edit's error shown over the window, as the web's development overlay is, while the page stays as it was.</summary>
internal static class FaultOverlay
{
    private const string Name = "AiorosHotReloadFault";

    public static void Show(Window window, string fault)
    {
        Hide(window);
        if (OverlayLayer.GetOverlayLayer(window) is not { } layer)
        {
            return;
        }

        var shown = new Border
        {
            Name = Name,
            Background = new SolidColorBrush(Color.FromRgb(0x7f, 0x1d, 0x1d)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12),
            Margin = new Thickness(16),
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new TextBlock { Text = fault, Foreground = Brushes.White, FontFamily = new FontFamily("monospace"), FontSize = 12, TextWrapping = TextWrapping.Wrap },
        };
        shown.Width = layer.Bounds.Width > 32 ? layer.Bounds.Width - 32 : double.NaN;
        Canvas.SetBottom(shown, 0);
        layer.Children.Add(shown);
    }

    public static void Hide(Window window)
    {
        if (OverlayLayer.GetOverlayLayer(window) is { } layer && layer.Children.OfType<Control>().FirstOrDefault(static child => child.Name == Name) is { } shown)
        {
            layer.Children.Remove(shown);
        }
    }
}
