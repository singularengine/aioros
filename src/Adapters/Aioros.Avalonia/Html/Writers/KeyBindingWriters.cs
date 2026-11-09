using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Input;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A control's <c>KeyBindings</c>, each written as a hidden submit button numbered with the page's buttons, carrying its
/// gesture as a hot key does (<see cref="HotKeys"/>), so the keyboard module posts its command: the page root's and its
/// window's from anywhere in the document, any other control's (<c>data-aioros-scope</c>, written right before the
/// control's element) while the focus is inside that element, the innermost first, as Avalonia's keyboard device tries
/// the focused element's ancestors before the top level. A binding whose command cannot execute is <c>disabled</c>, so
/// the key falls through to the next, as <c>KeyBinding.TryHandle</c> leaves it unhandled. The window bindings
/// <see cref="HotKeyManager"/> adds for hot keys are left out, since their buttons carry the key.
/// </summary>
internal static class KeyBindingWriters
{
    public static void Write(HtmlContext context, Control control, bool scoped)
    {
        if (!scoped && TopLevel.GetTopLevel(control) is { } top && !ReferenceEquals(top, control))
        {
            foreach (var binding in top.KeyBindings)
            {
                Write(context, control, binding, scoped: false);
            }
        }

        foreach (var binding in control.KeyBindings)
        {
            Write(context, control, binding, scoped);
        }
    }

    private static void Write(HtmlContext context, Control owner, KeyBinding binding, bool scoped)
    {
        var keys = HotKeys.Attributes(binding.Gesture);
        if (keys.Length == 0 || binding.Command?.GetType().DeclaringType == typeof(HotKeyManager))
        {
            return;
        }

        var plan = context.Plan;
        var (command, parameter, spec) = Command(plan, binding);
        plan.Literal("<button type=\"submit\" name=\"_btn\" value=\"");
        context.KeyButton(owner, binding, spec);
        plan.Literal("|");
        plan.Value(binding, KeyBinding.CommandParameterProperty);
        plan.Literal("\" hidden" + (scoped ? " data-aioros-scope" : string.Empty) + keys);
        if (plan.Planning && command is not null)
        {
            var disabled = new SlotSpec("enabled", null, command) { Values = parameter.Add("not") };
            plan.Add(new ConditionNode(Slots.Getter(disabled), [new LiteralNode(" disabled")]) { Source = disabled });
        }
        else if (binding.Command is { } executable && !executable.CanExecute(binding.CommandParameter))
        {
            plan.Literal(" disabled");
        }

        plan.Literal("></button>");
    }

    /// <summary>The binding's command path, its parameter as the enabled state reads it, and the field its post runs.</summary>
    private static (string? Path, ImmutableArray<string> Parameter, SlotSpec? Spec) Command(PlanRecorder plan, KeyBinding binding)
    {
        var command = CommandPaths.Of(plan, binding, KeyBinding.CommandProperty);
        var parameterPath = plan.PathOf(binding, KeyBinding.CommandParameterProperty);
        if (plan.Planning && (command is null || (parameterPath is null && binding.CommandParameter is not (null or string))))
        {
            plan.Fail($"KeyBinding.Command ({binding.Gesture}) is not bound to a command of the model, with a parameter the plan can follow");
            return (null, [], null);
        }

        if (command is null)
        {
            return (null, [], null);
        }

        return parameterPath is not null
            ? (command, ["path", parameterPath], new SlotSpec("command", command, parameterPath))
            : binding.CommandParameter is string text
                ? (command, ["text", text], new SlotSpec("command", command) { Values = [text] })
                : (command, ["none", string.Empty], new SlotSpec("command", command));
    }
}
