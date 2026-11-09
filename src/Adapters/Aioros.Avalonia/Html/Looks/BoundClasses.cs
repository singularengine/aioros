using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// Avalonia's <c>Classes.name="{Binding}"</c>: each bound class is an Avalonia property named with a reserved prefix,
/// which toggles the class when its value changes.
/// </summary>
internal static class BoundClasses
{
    private const string Prefix = "__AvaloniaReserved::Classes::";

    public static (AvaloniaProperty Property, string Name)? Next(Control control, HashSet<(Control, AvaloniaProperty)> expanding) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(control)
            .Where(property => property.Name.StartsWith(Prefix, StringComparison.Ordinal) && !expanding.Contains((control, property))
                && Plans.LocalBindings.Of(control, property) is not null)
            .Select(property => ((AvaloniaProperty, string)?)(property, property.Name[Prefix.Length..]))
            .FirstOrDefault();

    /// <summary>Writes the control with the class and without it, each as Avalonia styles it, chosen by the bound value.</summary>
    public static void WriteVariants(PlanRecorder plan, Control control, (AvaloniaProperty Property, string Name) bound, HashSet<(Control, AvaloniaProperty)> expanding, Action write)
    {
        var inverse = plan.NegatedPath(control, bound.Property);
        var converted = inverse is null ? BoundConverters.Of(plan, control, bound.Property) : null;
        var negated = inverse is null ? BoundConverters.Of(plan, control, bound.Property, negate: true) : null;
        var path = converted is null && inverse is null ? plan.BoundPath(control, bound.Property) : null;
        expanding.Add((control, bound.Property));
        if (converted is null && path is null && inverse is null)
        {
            write();
            expanding.Remove((control, bound.Property));
            return;
        }

        var had = control.Classes.Contains(bound.Name);
        try
        {
            var on = Variant(plan, control, bound.Name, true, write);
            var off = Variant(plan, control, bound.Name, false, write);
            if (inverse is not null)
            {
                var yes = Flags.Of(inverse, negate: true, fallback: false);
                var no = Flags.Of(inverse, negate: true, fallback: false, complement: true);
                plan.Add(new ConditionNode(yes.Getter, on) { Source = yes.Spec });
                plan.Add(new ConditionNode(no.Getter, off) { Source = no.Spec });
            }
            else if (converted is { } yes && negated is { } no)
            {
                plan.Add(new ConditionNode(yes.Getter, on) { Source = yes.Spec });
                plan.Add(new ConditionNode(no.Getter, off) { Source = no.Spec });
            }
            else
            {
                var slot = Flags.Of(path!, negate: false, fallback: false);
                var test = Flags.Of(path!, negate: false, fallback: false, complement: true);
                plan.Add(new ConditionNode(slot.Getter, on) { Source = slot.Spec });
                plan.Add(new ConditionNode(test.Getter, off) { Source = test.Spec });
            }
        }
        finally
        {
            Toggle(control, bound.Name, had);
            expanding.Remove((control, bound.Property));
        }
    }

    private static void Toggle(Control control, string name, bool value) => Settled.Change(control, () => control.Classes.Set(name, value));

    private static ImmutableArray<IPlanNode> Variant(PlanRecorder plan, Control control, string name, bool value, Action write)
    {
        Toggle(control, name, value);
        plan.Begin(plan.Scope);
        write();
        return plan.End();
    }
}
