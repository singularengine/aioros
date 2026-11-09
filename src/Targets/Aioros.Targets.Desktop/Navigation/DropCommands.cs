using Aioros.Avalonia.Controls;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>Runs the command of an item dropped on a target as a button's: awaited, then the page or view is revalidated.</summary>
internal static class DropCommands
{
    public static void Attach(Window window, DesktopNavigator navigator) =>
        window.AddHandler(Drag.MovedEvent, (_, args) =>
        {
            if (args.Source is Control source)
            {
                args.Handled = true;
                navigator.Run(args.Command, args.Move, source);
            }
        }, RoutingStrategies.Bubble);
}
