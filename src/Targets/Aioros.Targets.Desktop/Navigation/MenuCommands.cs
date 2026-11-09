using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Aioros.Targets.Desktop.Navigation;

internal static class MenuCommands
{
    private static readonly AttachedProperty<DesktopNavigator?> NavigatorProperty =
        AvaloniaProperty.RegisterAttached<DesktopNavigator, Control, DesktopNavigator?>("Navigator", inherits: true);

    private static readonly IDisposable[] Wrappers =
    [
        MenuItem.CommandProperty.Changed.AddClassHandler<MenuItem>(static (item, _) => Wrap(item)),
        NavigatorProperty.Changed.AddClassHandler<MenuItem>(static (item, _) => Wrap(item)),
    ];

    public static void Attach(Window window, DesktopNavigator navigator)
    {
        _ = Wrappers;
        window.SetValue(NavigatorProperty, navigator);
    }

    private static void Wrap(MenuItem item)
    {
        var navigator = item.GetValue(NavigatorProperty);
        var command = item.Command is AwaitedMenuCommand awaited ? awaited.Inner : item.Command;
        if (item.Command is AwaitedMenuCommand current && ReferenceEquals(current.Navigator, navigator) || item.Command is null || navigator is null && ReferenceEquals(command, item.Command))
        {
            return;
        }

        item.SetCurrentValue(MenuItem.CommandProperty, navigator is null ? command : new AwaitedMenuCommand(command!, navigator, item));
    }

    private sealed class AwaitedMenuCommand(ICommand inner, DesktopNavigator navigator, MenuItem item) : ICommand
    {
        public ICommand Inner { get; } = inner;

        public DesktopNavigator Navigator { get; } = navigator;

        public event EventHandler? CanExecuteChanged
        {
            add => Inner.CanExecuteChanged += value;
            remove => Inner.CanExecuteChanged -= value;
        }

        public bool CanExecute(object? parameter) => Inner.CanExecute(parameter);

        public void Execute(object? parameter) => Navigator.Run(Inner, parameter, item);
    }
}
