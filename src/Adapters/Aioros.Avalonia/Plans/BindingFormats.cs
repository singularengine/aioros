using System.Collections.Immutable;
using System.Reflection;
using Avalonia.Data;

namespace Aioros.Avalonia.Plans;

/// <summary>Reads a binding's <c>StringFormat</c>, which Avalonia keeps on its internal binding expression.</summary>
internal static class BindingFormats
{
    private static ImmutableDictionary<Type, PropertyInfo?> _formats = ImmutableDictionary<Type, PropertyInfo?>.Empty;
    private static ImmutableDictionary<Type, PropertyInfo?> _converters = ImmutableDictionary<Type, PropertyInfo?>.Empty;
    private static ImmutableDictionary<Type, PropertyInfo?> _parameters = ImmutableDictionary<Type, PropertyInfo?>.Empty;

    private static ImmutableDictionary<Type, PropertyInfo?> _priorities = ImmutableDictionary<Type, PropertyInfo?>.Empty;

    /// <summary>The priority the binding was set at, also before it has produced a value.</summary>
    public static BindingPriority Priority(BindingExpressionBase expression)
    {
        var property = ImmutableInterlocked.GetOrAdd(ref _priorities, expression.GetType(), static type => type.GetRuntimeProperties().FirstOrDefault(item => item.Name == "Priority" && item.PropertyType == typeof(BindingPriority)));
        return property?.GetValue(expression) is BindingPriority priority ? priority : BindingPriority.LocalValue;
    }

    public static object? Parameter(BindingExpressionBase expression)
    {
        if (TemplateConverters.Is(expression))
        {
            return TemplateConverters.Parameter(expression);
        }

        var property = ImmutableInterlocked.GetOrAdd(ref _parameters, expression.GetType(), static type => type.GetProperty("ConverterParameter", BindingFlags.Instance | BindingFlags.Public));
        return property?.GetValue(expression);
    }

    /// <summary>The converter of a binding, other than Avalonia's default one.</summary>
    public static global::Avalonia.Data.Converters.IValueConverter? Converter(BindingExpressionBase expression)
    {
        if (TemplateConverters.Is(expression))
        {
            return TemplateConverters.Converter(expression);
        }

        var property = ImmutableInterlocked.GetOrAdd(ref _converters, expression.GetType(), static type => type.GetProperty("Converter", BindingFlags.Instance | BindingFlags.Public));
        return property?.GetValue(expression) is global::Avalonia.Data.Converters.IValueConverter converter and not global::Avalonia.Data.Converters.DefaultValueConverter ? converter : null;
    }

    private static ImmutableDictionary<Type, PropertyInfo?> _nulls = ImmutableDictionary<Type, PropertyInfo?>.Empty;

    /// <summary>The binding's <c>TargetNullValue</c>, what the target gets for a null value, or nothing when it has none.</summary>
    public static object? TargetNullValue(BindingExpressionBase expression)
    {
        var property = ImmutableInterlocked.GetOrAdd(ref _nulls, expression.GetType(), static type => type.GetRuntimeProperties().FirstOrDefault(item => item.Name == "TargetNullValue"));
        var value = property?.GetValue(expression);
        return value == global::Avalonia.AvaloniaProperty.UnsetValue ? null : value;
    }

    public static string? StringFormat(BindingExpressionBase expression)
    {
        var property = ImmutableInterlocked.GetOrAdd(ref _formats, expression.GetType(), static type => type.GetProperty("StringFormat", BindingFlags.Instance | BindingFlags.Public));
        return property?.GetValue(expression) as string;
    }
}
