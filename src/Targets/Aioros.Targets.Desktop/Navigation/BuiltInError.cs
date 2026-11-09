using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Aioros.Targets.Desktop.Navigation;

internal static class BuiltInError
{
    public static Control Create() => new StackPanel
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Spacing = 8,
        Children =
        {
            new TextBlock { Text = "Something went wrong", FontSize = 22, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Text = "This page could not be shown.", Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center },
        },
    };
}
