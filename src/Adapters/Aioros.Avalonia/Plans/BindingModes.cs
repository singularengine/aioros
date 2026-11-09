using System.Collections.Immutable;
using System.Reflection;
using Avalonia.Data;

namespace Aioros.Avalonia.Plans;

internal static class BindingModes
{
    private static ImmutableDictionary<Type, FieldInfo?> _modes = ImmutableDictionary<Type, FieldInfo?>.Empty;

    private static ImmutableDictionary<Type, PropertyInfo?> _delays = ImmutableDictionary<Type, PropertyInfo?>.Empty;

    public static bool WritesToSource(BindingExpressionBase expression)
    {
        var field = ImmutableInterlocked.GetOrAdd(ref _modes, expression.GetType(), Find);
        return field?.GetValue(expression) is BindingMode.TwoWay or BindingMode.OneWayToSource;
    }

    /// <summary>How long the binding waits after its target changes before it updates its source (its <c>Delay</c>), or zero.</summary>
    public static TimeSpan Delay(BindingExpressionBase expression)
    {
        var property = ImmutableInterlocked.GetOrAdd(ref _delays, expression.GetType(), static type => type.GetRuntimeProperty(nameof(Delay)) is { } found && found.PropertyType == typeof(TimeSpan) ? found : null);
        return property?.GetValue(expression) is TimeSpan delay ? delay : TimeSpan.Zero;
    }

    private static FieldInfo? Find(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetRuntimeFields().FirstOrDefault(field => field.FieldType == typeof(BindingMode)) is { } field)
            {
                return field;
            }
        }

        return null;
    }
}
