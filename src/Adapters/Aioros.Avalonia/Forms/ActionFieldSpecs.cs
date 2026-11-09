using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;

namespace Aioros.Avalonia.Forms;

/// <summary>The fields of what a post runs rather than binds: a button's command and its parameter, and a drop target's command and keys.</summary>
internal static class ActionFieldSpecs
{
    public static SlotSpec? Command(PlanRecorder plan, Button button)
    {
        var command = CommandPaths.Of(plan, button, Button.CommandProperty);
        if (command is null && (button.Command is not null || LocalBindings.HasBinding(button, Button.CommandProperty)))
        {
            return null;
        }

        if (plan.PathOf(button, Button.CommandParameterProperty) is { } parameter)
        {
            return new SlotSpec("command", command, parameter);
        }

        return button.CommandParameter switch
        {
            null => new SlotSpec("command", command),
            string text => new SlotSpec("command", command) { Values = [text] },
            _ => null,
        };
    }

    /// <summary>
    /// A check box menu item with no command and its checked state bound to write back: its press flips that value, as
    /// the item's click flips it (NAVIGATION-108); nothing for any other menu item, whose press the engine answers.
    /// </summary>
    public static SlotSpec? Flip(PlanRecorder plan, Control control) =>
        control is MenuItem { ToggleType: MenuItemToggleType.CheckBox, Command: null } item && !LocalBindings.HasBinding(item, MenuItem.CommandProperty)
        && FieldSpecs.Written(plan, item, MenuItem.IsCheckedProperty) is { } flipped
            ? new SlotSpec("flip", flipped)
            : null;

    /// <summary>
    /// A drop target: its command's path and its key's (or the key itself), with the paths of its items and of their keys,
    /// so a move posted as a step finds the item's place.
    /// </summary>
    public static SlotSpec? Drop(PlanRecorder plan, Control target)
    {
        if (plan.PathOf(target, Drag.CommandProperty) is not { } command)
        {
            return null;
        }

        var key = plan.PathOf(target, Drag.TargetProperty);
        var items = target is ItemsControl list ? plan.PathOf(list, ItemsControl.ItemsSourceProperty) : null;
        var itemKey = ItemKey(Drag.Items(target)) ?? (target is ItemsControl templated ? ItemKey(TypedItems.Templates(templated).Select(template => template.Build(null)).OfType<Control>().SelectMany(root => root.GetSelfAndLogicalDescendants().OfType<Control>())) : null);
        return new SlotSpec("drop", command, key) { Values = [items ?? string.Empty, items is null ? string.Empty : itemKey ?? string.Empty, key is null ? Drag.Key(Drag.GetTarget(target)) : string.Empty] };
    }

    /// <summary>The path of the first item key binding among the items, realized or built from the list's templates.</summary>
    private static string? ItemKey(IEnumerable<Control> controls) =>
        controls.Select(item => LocalBindings.Of(item, Drag.ItemProperty) is IDescription { Description: { } path } && ElementBindings.IsSimplePath(path) ? path : null).FirstOrDefault(path => path is not null);
}
