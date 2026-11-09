using Avalonia;
using Avalonia.Data;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The local bindings a plan reads: a property's own binding to its data context, kept visible while a variant sets a
/// value above it (<see cref="Override"/>).
/// </summary>
internal static class LocalBindings
{
    [ThreadStatic]
    private static Dictionary<(AvaloniaObject, AvaloniaProperty), BindingExpressionBase>? _held;

    [ThreadStatic]
    private static Dictionary<(AvaloniaObject, AvaloniaProperty), BindingExpressionBase>? _probed;

    public static BindingExpressionBase? Of(AvaloniaObject target, AvaloniaProperty property) =>
        _held?.GetValueOrDefault((target, property)) is { } held ? held : Own(target, property);

    /// <summary>The property's own local binding, not one held while a value is set above it (<see cref="Override"/>).</summary>
    public static BindingExpressionBase? Own(AvaloniaObject target, AvaloniaProperty property) =>
        _held?.ContainsKey((target, property)) == true ? null
        : _probed?.GetValueOrDefault((target, property)) is { } probed ? probed
        : BindingOperations.GetBindingExpressionBase(target, property) is { } expression
        && expression.GetType().Name is "BindingExpression"
        && BindingFormats.Priority(expression) == BindingPriority.LocalValue
        && !BindingSources.IsConstant(expression, target)
            ? expression
            : null;

    public static bool IsBound(AvaloniaObject target, AvaloniaProperty property) => Of(target, property) is not null;

    /// <summary>Whether the property has a binding at all, including one that has not produced a value.</summary>
    public static bool HasBinding(AvaloniaObject target, AvaloniaProperty property) => BindingOperations.GetBindingExpressionBase(target, property) is not null;

    /// <summary>
    /// Sets a value above the property's binding while a variant is written, and keeps the binding visible to the plan,
    /// which Avalonia hides under a higher priority value.
    /// </summary>
    /// <remarks>
    /// A binding that writes to its source would write the value set above it into the model; the model gets its value
    /// back when the override ends, written through the same binding.
    /// </remarks>
    public static IDisposable? Override(AvaloniaObject target, AvaloniaProperty property, object? value)
    {
        var binding = Of(target, property);
        var original = BindingOperations.GetBindingExpressionBase(target, property) is { } any && BindingModes.WritesToSource(any) ? new Original(target.GetValue(property)) : null;
        var lease = target.SetValue(property, value, BindingPriority.Animation);
        if (binding is null && original is null)
        {
            return lease;
        }

        if (binding is not null)
        {
            (_held ??= [])[(target, property)] = binding;
        }

        return new HeldBinding(target, property, lease, original);
    }

    /// <summary>
    /// Sets a value above the property's binding while the plan checks that its slots hold for another value, the binding
    /// still the property's own, so the parts that show it still read it as a slot; a binding that writes to its source
    /// gets the model's value back when the probe ends.
    /// </summary>
    public static IDisposable Probe(AvaloniaObject target, AvaloniaProperty property, object? value)
    {
        var binding = Own(target, property);
        var original = BindingOperations.GetBindingExpressionBase(target, property) is { } any && BindingModes.WritesToSource(any) ? new Original(target.GetValue(property)) : null;
        var lease = target.SetValue(property, value, BindingPriority.Animation);
        if (binding is not null)
        {
            (_probed ??= [])[(target, property)] = binding;
        }

        return new Probed(target, property, lease, original);
    }

    private sealed record Original(object? Value);

    private sealed class Probed(AvaloniaObject target, AvaloniaProperty property, IDisposable? lease, Original? original) : IDisposable
    {
        public void Dispose()
        {
            _probed?.Remove((target, property));
            lease?.Dispose();
            if (original is not null && !Equals(target.GetValue(property), original.Value))
            {
                target.SetValue(property, original.Value, BindingPriority.Animation)?.Dispose();
            }
        }
    }

    private sealed class HeldBinding(AvaloniaObject target, AvaloniaProperty property, IDisposable? lease, Original? original) : IDisposable
    {
        public void Dispose()
        {
            _held?.Remove((target, property));
            lease?.Dispose();
            if (original is not null && !Equals(target.GetValue(property), original.Value))
            {
                target.SetValue(property, original.Value, BindingPriority.Animation)?.Dispose();
            }
        }
    }
}
