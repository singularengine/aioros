using Aioros.Markup;
using Aioros.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>A left click on a control with a local <c>a:Web.Href</c>, inside one, or on a linked run of a text (<see cref="InlineLinks"/>) navigates the window there.</summary>
internal static class LinkClicks
{
    public static void Attach(Window window, DesktopNavigator navigator)
    {
        window.AddHandler(InputElement.PointerReleasedEvent, (_, args) => OnReleased(navigator, args), RoutingStrategies.Bubble, handledEventsToo: true);
        window.AddHandler(InputElement.PointerMovedEvent, (_, args) => OnMoved(args), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void OnMoved(PointerEventArgs args)
    {
        if (args.Source is TextBlock text)
        {
            InlineLinks.Hover(text, args.GetPosition(text));
        }
    }

    private static void OnReleased(DesktopNavigator navigator, PointerReleasedEventArgs args)
    {
        if (args.InitialPressMouseButton != MouseButton.Left || args.Source is not Visual source)
        {
            return;
        }

        var link = (source as TextBlock is { } text ? InlineLinks.At(text, args.GetPosition(text)) : null)
            ?? source.GetSelfAndVisualAncestors().OfType<Control>().Select(Web.GetHref).FirstOrDefault(href => href is not null);
        if (link is not null && DesktopRequests.IsLocal(link))
        {
            args.Handled = true;
            Dispatcher.UIThread.Post(() => _ = navigator.TryNavigateAsync(link));
        }
    }
}
