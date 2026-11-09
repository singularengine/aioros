using System.Runtime.CompilerServices;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The converter and parameter of a <c>TemplateBinding</c> (<c>{TemplateBinding Title, Converter={x:Static
/// ObjectConverters.IsNotNull}}</c>), which Avalonia keeps on its template binding expression without exposing them as it
/// exposes a binding's: read through accessors the compiler binds, isolated here until Avalonia makes them public.
/// </summary>
internal static class TemplateConverters
{
    public static bool Is(BindingExpressionBase expression) => expression.GetType().FullName == "Avalonia.Data.TemplateBindingExpression";

    public static IValueConverter? Converter(BindingExpressionBase expression) => Is(expression) ? ConverterOf(expression) : null;

    public static object? Parameter(BindingExpressionBase expression) => Is(expression) ? ParameterOf(expression) : null;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_converter")]
    private static extern ref IValueConverter? ConverterOf([UnsafeAccessorType("Avalonia.Data.TemplateBindingExpression, Avalonia.Base")] object expression);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_converterParameter")]
    private static extern ref object? ParameterOf([UnsafeAccessorType("Avalonia.Data.TemplateBindingExpression, Avalonia.Base")] object expression);
}
