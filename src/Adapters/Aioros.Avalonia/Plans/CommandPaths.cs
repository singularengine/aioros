using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The path a plan reads a control's command through: a binding to a command; a binding to a method, which Avalonia runs
/// as a command (<c>Command="{Binding Save}"</c>, its <c>MethodToCommandConverter</c>), read as the member <c>Save()</c>;
/// or a command action an MVVM framework's markup extension sets (Stylet's <c>{s:Action Save}</c>), recognized by its
/// shape (a public <c>MethodName</c> and <c>Target</c>) and read as <c>Save(CanSave)</c>, with the framework's
/// <c>Can&lt;Method&gt;</c> guard property when the target has one.
/// </summary>
internal static partial class CommandPaths
{
    /// <summary>Whether the command varies with the model: bound, or a command action.</summary>
    public static bool Follows(Control control, AvaloniaProperty command) =>
        (LocalBindings.IsBound(control, command) && ElementMethod(control, command) is null) || Action(Value(control, command)) is not null;

    /// <summary>The path of a command that is not a control's (a <c>KeyBinding</c>'s): a binding to a command or to a method.</summary>
    public static string? Of(PlanRecorder plan, AvaloniaObject target, AvaloniaProperty command) =>
        target is Control control ? Of(plan, control, command) : plan.PathOf(target, command) is { } path ? Method(plan, path) ? path + "()" : path : null;

    public static string? Of(PlanRecorder plan, Control control, AvaloniaProperty command)
    {
        if (plan.Planning && ElementMethod(control, command) is { } element)
        {
            if (element.Guard is { } guard)
            {
                plan.Fail($"{control.GetType().Name}.{command.Name} is bound to the method '{element.Description}' of an element whose {guard} enables it, which the plan cannot follow");
            }
            else
            {
                plan.Follow(control, command);
            }

            return null;
        }

        if (plan.PathOf(control, command) is { } path)
        {
            return Method(plan, path) ? path + "()" : path;
        }

        if (!plan.Planning)
        {
            return null;
        }

        if (LocalBindings.Of(control, command) is { } binding)
        {
            return (binding as IDescription)?.Description is { } description && MethodBinding().IsMatch(description) && plan.InScope(control)
                ? Followed(plan, control, command, description)
                : null;
        }

        return Action(Value(control, command)) is { } action ? ActionPath(plan, control, command, action) : null;
    }

    /// <summary>What names a command the plan cannot follow, for the reason it gives.</summary>
    public static string Describe(Control control, AvaloniaProperty command) =>
        Action(Value(control, command)) is { } action ? $"the action '{action.Method}' ({Value(control, command)!.GetType().Name})"
        : LocalBindings.Of(control, command) is IDescription { Description: { } description } ? $"'{description}'"
        : $"a {Value(control, command)?.GetType().Name ?? "missing"} command";

    /// <summary>The command set on the control, under the value a variant of its enabled state puts above it.</summary>
    private static object? Value(Control control, AvaloniaProperty command) => control.GetBaseValue(command) is var local && local != AvaloniaProperty.UnsetValue ? local : control.GetValue(command);

    /// <summary>Whether a path's last member is a method of its owner rather than a property or a field, as a compiled binding to a method reads it.</summary>
    private static bool Method(PlanRecorder plan, string path)
    {
        var dot = path.LastIndexOf('.');
        var owner = dot < 0 ? plan.Scope?.GetType() : plan.PathType(path[..dot]);
        var member = path[(dot + 1)..];
        return owner is not null && member.Length > 0 && owner.GetProperty(member) is null && owner.GetField(member) is null
            && owner.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(method => method.Name == member);
    }

    private static string Followed(PlanRecorder plan, Control control, AvaloniaProperty command, string path)
    {
        plan.Follow(control, command);
        return path;
    }

    private static string? ActionPath(PlanRecorder plan, Control control, AvaloniaProperty command, (string Method, object? Target) action)
    {
        var name = $"{control.GetType().Name}.{command.Name} is the action '{action.Method}' ({Value(control, command)!.GetType().Name})";
        if (action.Target is null || !ReferenceEquals(action.Target, plan.Scope))
        {
            plan.Fail($"{name}, whose target ({action.Target?.GetType().Name ?? "none"}) is not the data context the plan follows");
            return null;
        }

        var type = action.Target.GetType();
        if (type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Count(method => method.Name == action.Method && method.GetParameters().Length <= 1) != 1)
        {
            plan.Fail($"{name}, and its target ({type.Name}) has no single public method of that name with at most one parameter");
            return null;
        }

        FollowTarget(plan, control, action.Target);
        var guard = "Can" + action.Method;
        return type.GetProperty(guard)?.PropertyType == typeof(bool) ? $"{action.Method}({guard})" : $"{action.Method}()";
    }

    /// <summary>Follows the binding that hands the action its target (Stylet's <c>View.ActionTarget="{Binding}"</c>): it reads the data context the plan follows.</summary>
    private static void FollowTarget(PlanRecorder plan, Control control, object target)
    {
        for (Visual? current = control; current is not null; current = current.GetVisualParent())
        {
            foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(current.GetType())
                .Where(property => LocalBindings.Of(current, property) is IDescription { Description: "" } && ReferenceEquals(current.GetValue(property), target)))
            {
                plan.Follow(current, property);
            }
        }
    }

    private static (string Method, object? Target)? Action(object? value)
    {
        if (value is not ICommand || value.GetType() is not { } type
            || type.GetProperty("MethodName") is not { PropertyType: var methodType } methodName || methodType != typeof(string)
            || type.GetProperty("Target") is not { } target)
        {
            return null;
        }

        return methodName.GetValue(value) is string { Length: > 0 } method ? (method, target.GetValue(value)) : null;
    }

    [GeneratedRegex(@"^([A-Za-z_][A-Za-z0-9_]*\.)*[A-Za-z_][A-Za-z0-9_]*\(\)$")]
    private static partial Regex MethodBinding();

    /// <summary>
    /// A command bound to a method of another element (<c>$parent[IClearControl].Clear</c>, as Ursa's themes clear their
    /// controls), with the <c>Can</c> guard Avalonia's method commands read when the element's type has one: the element
    /// runs its own code, which only the Avalonia engine can, so the plan writes the command as rendered and leaves its
    /// post undescribed.
    /// </summary>
    /// <summary>Whether the property is a command bound to a method of another element with no guard, which reads no model data.</summary>
    public static bool IsElementMethod(Control control, AvaloniaProperty property) => ElementMethod(control, property) is { Guard: null };

    private static (string Description, string? Guard)? ElementMethod(Control control, AvaloniaProperty command)
    {
        if (LocalBindings.Of(control, command) is not IDescription { Description: { } description }
            || ElementBindings.ElementOf(control, description.EndsWith("()", StringComparison.Ordinal) ? description[..^2] : description) is not { } element)
        {
            return null;
        }

        var name = description.TrimEnd(')', '(')[(description.TrimEnd(')', '(').LastIndexOf('.') + 1)..];
        var type = element.GetType();
        if (type.GetProperty(name) is not null || !type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(method => method.Name == name && method.GetParameters().Length <= 1))
        {
            return null;
        }

        var guard = "Can" + name;
        return (description, type.GetMethod(guard) is not null || type.GetProperty(guard) is not null ? $"{type.Name}.{guard}" : null);
    }
}
