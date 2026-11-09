using System.Collections.Immutable;
using System.Windows.Input;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>
/// The commands a library's templated control runs from its own code (Ursa's <c>PopConfirm.ConfirmCommand</c> on a
/// confirmation, <c>PathPicker.Command</c> on a picked file), bound to the model: only the Avalonia engine can run the
/// control's code, so the plan writes the control as Avalonia renders it, learned by the command's state (whether one is
/// set, and whether it can run with the control's <c>&lt;Name&gt;Parameter</c>), since its code may give it the look
/// that state calls for, as Avalonia's <c>ICommandSource</c> controls do; and no field the control holds is described,
/// so its posts go through the engine, as a template button running its control's method does.
/// </summary>
internal static class LibraryCommands
{
    private const string ParameterSuffix = "Parameter";

    /// <summary>Whether the property is a command of a library's own, bound on the control.</summary>
    public static bool Is(Control control, AvaloniaProperty property) =>
        LibraryValues.FromLibrary(property.OwnerType) && typeof(ICommand).IsAssignableFrom(property.PropertyType) && LocalBindings.Of(control, property) is not null;

    /// <summary>Whether the property is the parameter of a bound library command, which the command's state is learned with.</summary>
    public static bool IsParameter(Control control, AvaloniaProperty property) =>
        property.Name.EndsWith(ParameterSuffix, StringComparison.Ordinal)
        && AvaloniaPropertyRegistry.Instance.GetRegistered(control).FirstOrDefault(command => command.Name == property.Name[..^ParameterSuffix.Length]) is { } command
        && Is(control, command);

    /// <summary>Whether a field sits in a library control that runs a bound command of its own, so only the engine can answer its post.</summary>
    public static bool Inside(Control field) =>
        field.GetVisualAncestors().OfType<Control>().Any(control => AvaloniaPropertyRegistry.Instance.GetRegistered(control).Any(property => Is(control, property)));

    /// <summary>The control's body learned by its command's state: set or not, then whether it can run.</summary>
    public static ImmutableArray<IPlanNode> Learned(PlanRecorder plan, Control control, AvaloniaProperty property, string path, ImmutableArray<IPlanNode> body)
    {
        if (Parameter(plan, control, property) is not { } parameter)
        {
            return body;
        }

        var set = new SlotSpec("test", path, "set");
        var enabled = new SlotSpec("enabled", null, path) { Values = parameter };
        var runs = new LearnedNode(Slots.Getter(enabled), [LearnedNode.KeyOf(Slots.Getter(enabled)(plan.Scope))], [body]) { Source = enabled };
        return [new LearnedNode(Slots.Getter(set), [LearnedNode.KeyOf(Slots.Getter(set)(plan.Scope))], [[runs]]) { Source = set }];
    }

    private static ImmutableArray<string>? Parameter(PlanRecorder plan, Control control, AvaloniaProperty command)
    {
        if (AvaloniaPropertyRegistry.Instance.GetRegistered(control).FirstOrDefault(property => property.Name == command.Name + ParameterSuffix) is not { } parameter)
        {
            return ["none", string.Empty];
        }

        if (LocalBindings.Of(control, parameter) is { } binding)
        {
            if (!BindingSources.ReadsSelf(binding, control) && binding is IDescription { Description: { } described } && ElementBindings.IsSimplePath(described)
                && plan.PathOf(control, parameter) is { } path)
            {
                return ["path", path];
            }
        }
        else if (control.GetValue(parameter) is var value && value is null or string)
        {
            return value is string text ? ["text", text] : ["none", string.Empty];
        }

        plan.Fail($"{control.GetType().Name}.{parameter.Name}, the parameter of its command, is bound to or set to a value the plan cannot follow");
        return null;
    }
}
