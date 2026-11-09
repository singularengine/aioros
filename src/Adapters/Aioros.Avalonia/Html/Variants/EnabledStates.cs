using System.Collections.Immutable;
using System.Windows.Input;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>
/// A control whose enabled state follows the model: a bound <c>IsEnabled</c>, or a command whose <c>CanExecute</c>
/// enables it, as Avalonia's <c>ICommandSource</c> controls do. The control is written enabled and disabled, each as
/// Avalonia styles it (<c>:disabled</c>, and its descendants with it), its descendants' bindings to it read as constants
/// of each state (<see cref="ConstantBindings.LearnedConstant"/>), and each render picks one.
/// </summary>
internal static class EnabledStates
{
    public static bool Varies(Control control, HashSet<(Control, AvaloniaProperty)> expanding) =>
        !expanding.Contains((control, InputElement.IsEnabledProperty))
        && (LocalBindings.IsBound(control, InputElement.IsEnabledProperty) || (Command(control) is { } command && CommandPaths.Follows(control, command)));

    public static void WriteVariants(PlanRecorder plan, Control control, HashSet<(Control, AvaloniaProperty)> expanding, Action write)
    {
        expanding.Add((control, InputElement.IsEnabledProperty));
        var negated = NegatesLocal(plan, control);
        if ((negated is { } inverse ? new SlotSpec("enabled", inverse) { Values = ["none", string.Empty] } : Spec(plan, control)) is not { } spec)
        {
            write();
            expanding.Remove((control, InputElement.IsEnabledProperty));
            return;
        }

        var leases = new List<IDisposable?>();
        var held = ConstantBindings.LearnedConstant(control, InputElement.IsEnabledProperty);
        try
        {
            var on = Variant(plan, control, leases, enabled: true, write);
            var off = Variant(plan, control, leases, enabled: false, write);
            var yes = Slots.Getter(spec);
            var not = spec with { Values = spec.Values.Add("not") };
            plan.Add(new ConditionNode(yes, negated is null ? on : off) { Source = spec });
            plan.Add(new ConditionNode(Slots.Getter(not), negated is null ? off : on) { Source = not });
        }
        finally
        {
            held.Dispose();
            Settled.Change(control, () => leases.ForEach(lease => lease?.Dispose()));
            expanding.Remove((control, InputElement.IsEnabledProperty));
        }
    }

    /// <summary>The local path of a negated binding to a local input (<c>IsEnabled="{Binding !#a.IsChecked}"</c>), which enables the control while the input's value is false.</summary>
    private static string? NegatesLocal(PlanRecorder plan, Control control) =>
        LocalBindings.Of(control, InputElement.IsEnabledProperty) is IDescription { Description: ['!', '#', ..] } && plan.NegatedPath(control, InputElement.IsEnabledProperty) is { } path
        && Runtime.Slots.LocalPaths.TryRead(path, out _) ? path : null;

    private static SlotSpec? Spec(PlanRecorder plan, Control control)
    {
        string? enabled = null;
        if (plan.FollowsConstantElement(control, InputElement.IsEnabledProperty))
        {
            if (!control.IsEnabled || Command(control) is not { } followed || !CommandPaths.Follows(control, followed))
            {
                return null;
            }
        }
        else if (LocalBindings.IsBound(control, InputElement.IsEnabledProperty) && (enabled = plan.BoundPath(control, InputElement.IsEnabledProperty)) is null)
        {
            return null;
        }

        if (Command(control) is not { } command || !CommandPaths.Follows(control, command))
        {
            return new SlotSpec("enabled", enabled) { Values = ["none", string.Empty] };
        }

        return CommandSpec(plan, control, command) is { } spec ? spec with { Path = enabled } : null;
    }

    private static SlotSpec? CommandSpec(PlanRecorder plan, Control control, AvaloniaProperty command)
    {
        var commandPath = CommandPaths.Of(plan, control, command);
        var parameterProperty = Property(control, "CommandParameter");
        var parameterPath = parameterProperty is null ? null : plan.PathOf(control, parameterProperty);
        var value = parameterProperty is null ? null : control.GetValue(parameterProperty);
        var unfollowed = parameterPath is null && parameterProperty is not null && LocalBindings.IsBound(control, parameterProperty);
        if (commandPath is null || unfollowed || (parameterPath is null && value is not (null or string)))
        {
            plan.Fail($"{control.GetType().Name}.Command is bound to a command or parameter the plan cannot follow ({CommandPaths.Describe(control, command)})");
            return null;
        }

        ImmutableArray<string> parameter = parameterPath is not null ? ["path", parameterPath] : value is string text ? ["text", text] : ["none", string.Empty];
        return new SlotSpec("enabled", null, commandPath) { Values = parameter };
    }

    private static ImmutableArray<IPlanNode> Variant(PlanRecorder plan, Control control, List<IDisposable?> leases, bool enabled, Action write)
    {
        Settled.Change(control, () =>
        {
            leases.ForEach(lease => lease?.Dispose());
            leases.Clear();
            leases.Add(LocalBindings.Override(control, InputElement.IsEnabledProperty, enabled));
            if (enabled && !control.IsEffectivelyEnabled && Command(control) is { } command)
            {
                leases.Add(LocalBindings.Override(control, command, Always.Instance));
            }
        });
        plan.Begin(plan.Scope);
        write();
        return plan.End();
    }

    private static AvaloniaProperty? Command(Control control) => control is ICommandSource ? Property(control, "Command") : null;

    private static AvaloniaProperty? Property(Control control, string name) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(control).FirstOrDefault(property => property.Name == name);

    private sealed class Always : ICommand
    {
        public static readonly Always Instance = new();

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }
}
