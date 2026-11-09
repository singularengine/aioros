using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Controls;

/// <summary>
/// Drag and drop for sortable lists and moves between lists, as attached properties in the style of Xaml.Behaviors:
/// <see cref="ItemProperty"/> gives an element its item key, <see cref="TargetProperty"/> makes a list a drop target with
/// its own key, and <see cref="CommandProperty"/> is the target's command, which receives the move as
/// <c>(string Item, string Target, int Index)</c>, the index the item has in the target list after the move.
/// </summary>
public sealed class Drag : AvaloniaObject
{
    public static readonly AttachedProperty<object?> ItemProperty =
        AvaloniaProperty.RegisterAttached<Drag, Control, object?>("Item");

    public static readonly AttachedProperty<object?> TargetProperty =
        AvaloniaProperty.RegisterAttached<Drag, Control, object?>("Target");

    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Drag, Control, ICommand?>("Command");

    /// <summary>Raised on a target when an item is dropped on it; when no handler takes it, the target runs its command.</summary>
    public static readonly RoutedEvent<DragMovedEventArgs> MovedEvent =
        RoutedEvent.Register<DragMovedEventArgs>("Moved", RoutingStrategies.Bubble, typeof(Drag));

    private static readonly AttachedProperty<PointerPressedEventArgs?> PressProperty =
        AvaloniaProperty.RegisterAttached<Drag, Control, PointerPressedEventArgs?>("Press");

    private static readonly IDisposable[] Handlers =
    [
        ItemProperty.Changed.AddClassHandler<Control>(static (control, change) => Draggable(control, change.OldValue is not null, change.NewValue is not null)),
        TargetProperty.Changed.AddClassHandler<Control>(static (control, change) => Droppable(control, change.OldValue is not null, change.NewValue is not null)),
    ];

    /// <summary>The data a dragged item carries: its key.</summary>
    public static DataFormat<string> Format { get; } = DataFormat.CreateInProcessFormat<string>("aioros-drag-item");

    public static object? GetItem(Control control) => control.GetValue(ItemProperty);

    public static void SetItem(Control control, object? value) => control.SetValue(ItemProperty, value);

    public static object? GetTarget(Control control) => control.GetValue(TargetProperty);

    public static void SetTarget(Control control, object? value) => control.SetValue(TargetProperty, value);

    public static ICommand? GetCommand(Control control) => control.GetValue(CommandProperty);

    public static void SetCommand(Control control, ICommand? value) => control.SetValue(CommandProperty, value);

    /// <summary>A key as text, as the web posts it.</summary>
    public static string Key(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Whether a control is a drop target.</summary>
    public static bool IsTarget(Control control) => control.IsSet(TargetProperty);

    /// <summary>The target's items, in order: the elements with an item key whose nearest target is this one.</summary>
    public static IReadOnlyList<Control> Items(Control target) =>
        [.. target.GetVisualDescendants().OfType<Control>().Where(control => control.IsSet(ItemProperty) && NearestTarget(control) == target)];

    /// <summary>
    /// The index an item dropped at a point of the target takes: the number of the target's other items that lie before
    /// the point, along the list's direction.
    /// </summary>
    public static int IndexAt(Control target, string item, Point point)
    {
        var others = Items(target).Where(control => Key(GetItem(control)) != item).ToList();
        var centers = others.Select(control => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), target) ?? default).ToList();
        var across = centers.Count > 1 && Math.Abs(centers[1].X - centers[0].X) > Math.Abs(centers[1].Y - centers[0].Y);
        return centers.Count(center => across ? center.X < point.X : center.Y < point.Y);
    }

    private static Control? NearestTarget(Control item) =>
        item.GetVisualAncestors().OfType<Control>().FirstOrDefault(IsTarget);

    private static void Draggable(Control control, bool was, bool @is)
    {
        _ = Handlers;
        if (was == @is)
        {
            return;
        }

        if (@is)
        {
            control.PointerPressed += OnPressed;
            control.PointerMoved += OnMoved;
            control.PointerReleased += OnReleased;
            return;
        }

        control.PointerPressed -= OnPressed;
        control.PointerMoved -= OnMoved;
        control.PointerReleased -= OnReleased;
    }

    private static void Droppable(Control control, bool was, bool @is)
    {
        if (was == @is)
        {
            return;
        }

        DragDrop.SetAllowDrop(control, @is);
        if (@is)
        {
            control.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            control.AddHandler(DragDrop.DropEvent, OnDrop);
            return;
        }

        control.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        control.RemoveHandler(DragDrop.DropEvent, OnDrop);
    }

    private static void OnPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is Control control && args.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            control.SetValue(PressProperty, args);
        }
    }

    private static void OnReleased(object? sender, PointerReleasedEventArgs args) => (sender as Control)?.ClearValue(PressProperty);

    private static void OnMoved(object? sender, PointerEventArgs args)
    {
        if (sender is not Control control || control.GetValue(PressProperty) is not { } press)
        {
            return;
        }

        var moved = args.GetPosition(control) - press.GetPosition(control);
        if (Math.Abs(moved.X) + Math.Abs(moved.Y) < 5)
        {
            return;
        }

        control.ClearValue(PressProperty);
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(Format, Key(GetItem(control))));
        _ = DragDrop.DoDragDropAsync(press, data, DragDropEffects.Move);
    }

    private static void OnDragOver(object? sender, DragEventArgs args) =>
        args.DragEffects = args.DataTransfer.Contains(Format) ? DragDropEffects.Move : DragDropEffects.None;

    private static void OnDrop(object? sender, DragEventArgs args)
    {
        if (sender is not Control target || GetCommand(target) is not { } command || args.DataTransfer.TryGetValue(Format) is not { } item)
        {
            return;
        }

        args.Handled = true;
        var moved = new DragMovedEventArgs(command, (item, Key(GetTarget(target)), IndexAt(target, item, args.GetPosition(target))));
        target.RaiseEvent(moved);
        if (!moved.Handled && command.CanExecute(moved.Move))
        {
            command.Execute(moved.Move);
        }
    }
}

/// <summary>An item dropped on a target: the target's command and the move it receives.</summary>
public sealed class DragMovedEventArgs(ICommand command, (string Item, string Target, int Index) move) : RoutedEventArgs(Drag.MovedEvent)
{
    public ICommand Command { get; } = command;

    public (string Item, string Target, int Index) Move { get; } = move;
}
