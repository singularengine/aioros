using System.Collections;
using System.Collections.Immutable;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>
/// The bound properties of a library's templated control, panel or decorator (Ursa's <c>Rating.Value</c>, <c>TimeBox.Time</c>,
/// <c>TagInput.Tags</c>, <c>Badge.Header</c>) whose values have a stable key (a text, a number, a date, an enum, a list
/// of those): the control is written as Avalonia renders it and learned by the values the compiled model holds
/// (<see cref="LearnedNode"/>), and by the value the model held before the page was bound when the control's render wrote
/// another into it (<see cref="FreshValues"/>), so a render with those values writes it from the plan and one with other
/// values is rendered by the engine. Its own properties are learned, since its code may read them as its template does, unless its template only shows them and a second trace with other values records the same plan (<see cref="Slotted"/>);
/// those it inherits from Avalonia are learned only when a part of its template reads them where no slot does (a
/// <c>MultiBinding</c> of them, a brush), or when nothing follows them. A command of the library's own is learned by its
/// state (<see cref="LibraryCommands"/>). A value with no key (a model object, another command) is left to the slots
/// that show it (<see cref="PlanRecorder.TemplateShown"/>) or to the plan to fail on. Nothing is learned in the items
/// of a bound list, whose plan writes every item from the body learned for one; a library list's own items are learned
/// with it (its items are written as rendered), or nothing of it is when they have no key, since the look of each item
/// may follow what is learned (its selection).
/// </summary>
internal static class LibraryValues
{
    public static ImmutableArray<AvaloniaProperty> Candidates(Control control, HashSet<(Control, AvaloniaProperty)> expanding) =>
        Eligible(control)
            ? [.. AvaloniaPropertyRegistry.Instance.GetRegistered(control).Where(property => !expanding.Contains((control, property)) && Learnable(control, property) && (control is TemplatedControl || FromLibrary(property.OwnerType)))]
            : [];

    /// <summary>Whether the plan may learn this bound property of the control by value, so no check fails on it before it is written.</summary>
    public static bool MayLearn(Control control, AvaloniaProperty property) => Eligible(control) && Learnable(control, property) && (control is TemplatedControl || FromLibrary(property.OwnerType));

    private static bool Eligible(Control control) =>
        control is TemplatedControl or Decorator or Panel && FromLibrary(control.GetType()) && (control.TemplatedParent is null || Presented(control)) && !InBoundList(control) && ControlWriters.For(control) is null
        && (control is not ItemsControl || !LocalBindings.IsBound(control, ItemsControl.ItemsSourceProperty) || Learnable(control, ItemsControl.ItemsSourceProperty));

    public static void Write(PlanRecorder plan, Control control, ImmutableArray<AvaloniaProperty> candidates, HashSet<(Control, AvaloniaProperty)> expanding, Action write)
    {
        if (Slotted(plan, control, candidates, expanding, write) is { } slotted)
        {
            foreach (var node in slotted)
            {
                plan.Add(node);
            }

            return;
        }

        var withItems = candidates.Any(property => property != ItemsControl.ItemsSourceProperty);
        var held = candidates.Where(property => withItems || property != ItemsControl.ItemsSourceProperty).Select(property => ConstantBindings.LearnedConstant(control, property)).ToList();
        foreach (var property in candidates)
        {
            expanding.Add((control, property));
        }

        try
        {
            plan.Begin(plan.Scope);
            write();
            var body = plan.End();
            foreach (var property in candidates.Where(property => Learns(plan, control, property)).Reverse())
            {
                body = Learned(plan, control, property, body);
            }

            foreach (var node in body)
            {
                plan.Add(node);
            }
        }
        finally
        {
            held.ForEach(lease => lease.Dispose());
            foreach (var property in candidates)
            {
                expanding.Remove((control, property));
            }
        }
    }

    /// <summary>
    /// The control written with its own properties that its template only shows (a text, a flag of a part's visibility)
    /// as slots of their bindings, as <c>TemplateBinding</c> is a one-way read (WPF, Avalonia): the control is written
    /// again with another value set above each binding, and the slots are kept when both writes record the same plan and
    /// none of the control's other properties of its own changed, as a tracing JIT's guard checks that a trace holds for
    /// another value; or nothing, when the control's code or a part changes with them, and they are learned by value.
    /// </summary>
    private static ImmutableArray<IPlanNode>? Slotted(PlanRecorder plan, Control control, ImmutableArray<AvaloniaProperty> candidates, HashSet<(Control, AvaloniaProperty)> expanding, Action write)
    {
        var shown = candidates.Where(property => FromLibrary(property.OwnerType) && TemplateBindings.ShownByTemplate(control, property) && Probe(control, property) is not null).ToList();
        if (shown.Count == 0 || shown.Count < candidates.Count(property => FromLibrary(property.OwnerType)))
        {
            return null;
        }

        var held = candidates.Except(shown).Where(property => property != ItemsControl.ItemsSourceProperty).Select(property => ConstantBindings.LearnedConstant(control, property)).ToList();
        foreach (var property in candidates)
        {
            expanding.Add((control, property));
        }

        try
        {
            var first = Record(plan, write);
            var own = Own(control, shown);
            List<IDisposable> probes = [.. shown.Select(property => LocalBindings.Probe(control, property, Probe(control, property)))];
            (ImmutableArray<IPlanNode> Body, string? Failure) second = ([], null);
            bool reacted;
            try
            {
                Settled.Rebuilt(control);
                reacted = !Own(control, shown).SequenceEqual(own);
                if (!reacted)
                {
                    second = Record(plan, write);
                }
            }
            finally
            {
                probes.ForEach(probe => probe.Dispose());
                Settled.Rebuilt(control);
            }
            return !reacted && first.Failure is null && second.Failure is null && shown.All(property => plan.TemplateFollows(control, property))
                && PlanSerializer.TryWrite(first.Body) is { Json: { } json } && PlanSerializer.TryWrite(second.Body).Json == json
                ? first.Body
                : null;
        }
        finally
        {
            held.ForEach(lease => lease.Dispose());
            foreach (var property in candidates)
            {
                expanding.Remove((control, property));
            }
        }
    }

    /// <summary>The values of the control's other properties of its library's own, which change when its code reads the probed ones (Aioros.Cosmo's <c>InputOtp.Slots</c>, built from its <c>Text</c>).</summary>
    private static List<object?> Own(Control control, List<AvaloniaProperty> probed) =>
        [.. AvaloniaPropertyRegistry.Instance.GetRegistered(control).Where(property => FromLibrary(property.OwnerType) && !probed.Contains(property)).Select(control.GetValue)];

    private static (ImmutableArray<IPlanNode> Body, string? Failure) Record(PlanRecorder plan, Action write)
    {
        var body = ImmutableArray<IPlanNode>.Empty;
        var failure = plan.Attempt(() =>
        {
            plan.Begin(plan.Scope);
            write();
            body = plan.End();
        });
        return (body, failure);
    }

    /// <summary>Another value of the same kind than the one a property holds, which a part showing it writes through the same slot: a text, or a number.</summary>
    private static object? Probe(Control control, AvaloniaProperty property) =>
        Probe(control.GetValue(property)) is { } probe && property.PropertyType.IsInstanceOfType(probe) ? probe : null;

    private static object? Probe(object? value) => value switch
    {
        null => "\u2060",
        string text => text + "\u2060",
        int number => number + 1,
        long number => number + 1,
        double number => (number * 2) + 1,
        float number => (number * 2) + 1,
        decimal number => (number * 2) + 1,
        _ => null,
    };

    /// <summary>
    /// Whether the plan learns the property: always for the library's own, since its code may read them; for one it inherits
    /// from Avalonia, when a part of its template reads it where no slot does or, read by no part, when nothing follows it.
    /// </summary>
    private static bool Learns(PlanRecorder plan, Control control, AvaloniaProperty property)
    {
        if (FromLibrary(property.OwnerType))
        {
            return true;
        }

        var (read, slotted) = plan.TemplateReads(control, property);
        return read ? !slotted : !plan.IsFollowed(control, property);
    }

    /// <summary>
    /// Whether a control a template presents is the app's own content, a logical child of its templated parent (an
    /// <c>Expander</c>'s header), rather than a part of the template, whose values its template gives it.
    /// </summary>
    private static bool Presented(Control control) =>
        control.TemplatedParent is global::Avalonia.LogicalTree.ILogical parent && parent.LogicalChildren.Contains(control);

    private static bool InBoundList(Control control) =>
        control.GetSelfAndVisualAncestors().OfType<Control>()
            .Any(container => ItemsControl.ItemsControlFromItemContainer(container) is { } owner && LocalBindings.HasBinding(owner, ItemsControl.ItemsSourceProperty));

    private static bool Learnable(Control control, AvaloniaProperty property) =>
        BrowserFlags.Property(control) != property && LocalBindings.Of(control, property) is { } binding && BindingFormats.Converter(binding) is null && (LibraryCommands.Is(control, property) || (Keyed(control.GetValue(property)) && !LibraryCommands.IsParameter(control, property)));

    private static ImmutableArray<IPlanNode> Learned(PlanRecorder plan, Control control, AvaloniaProperty property, ImmutableArray<IPlanNode> body)
    {
        if (plan.PathOf(control, property) is not { } path)
        {
            return body;
        }

        if (path.StartsWith('^'))
        {
            plan.Fail($"{control.GetType().Name}.{property.Name} is bound to '{path}', outside the current scope, which the plan cannot learn by value");
            return body;
        }

        if (LibraryCommands.Is(control, property))
        {
            return LibraryCommands.Learned(plan, control, property, path, body);
        }

        var value = PropertyPaths.Getter(path)(plan.Scope);
        if (!Keyed(value))
        {
            plan.Fail($"{control.GetType().Name}.{property.Name} is bound to '{path}', whose {value!.GetType().Name} has no key the plan can learn it by");
            return body;
        }

        var source = SlotSpec.Bound(path);
        var key = LearnedNode.KeyOf(value);
        return FreshValues.TryRead(control, plan.Scope, path, out var fresh) && Keyed(fresh) && LearnedNode.KeyOf(fresh) is var before && before != key
            ? [new LearnedNode(Slots.Getter(source), [key, before], [body, body]) { Source = source }]
            : [new LearnedNode(Slots.Getter(source), [key], [body]) { Source = source }];
    }

    /// <summary>Whether a type comes from a library or the app, not from Avalonia or Aioros.</summary>
    public static bool FromLibrary(Type type) =>
        type.Assembly.GetName().Name is { } name && !name.StartsWith("Avalonia", StringComparison.Ordinal) && name != typeof(LibraryValues).Assembly.GetName().Name;

    /// <summary>Whether a value's key names it alone: nothing, a text, a number, a date or time, an enum, or a list of such values.</summary>
    private static bool Keyed(object? value) => value switch
    {
        null or string or Enum or Guid or TimeSpan or DateTime or DateTimeOffset or DateOnly or TimeOnly or decimal or System.Net.IPAddress => true,
        _ when value.GetType().IsPrimitive => true,
        IEnumerable items => items.Cast<object?>().All(Keyed),
        _ => false,
    };
}
