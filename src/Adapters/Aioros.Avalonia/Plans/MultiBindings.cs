using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Data.Core;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// A <c>MultiBinding</c> in a plan: the values of its bindings, by their paths, through its multi-value converter (a
/// <c>StringFormat</c> is Avalonia's <c>StringFormatMultiValueConverter</c> around the converter), as Avalonia
/// publishes them.
/// </summary>
internal static class MultiBindings
{
    private static ImmutableDictionary<string, IMultiValueConverter> _resolved = ImmutableDictionary<string, IMultiValueConverter>.Empty;

    public static bool Has(AvaloniaObject target, AvaloniaProperty property) =>
        BindingOperations.GetBindingExpressionBase(target, property) is { } expression && expression.GetType().Name == "MultiBindingExpression";

    public static (ValueGetter Getter, SlotSpec? Spec)? Of(PlanRecorder plan, AvaloniaObject target, AvaloniaProperty property)
    {
        if (BindingOperations.GetBindingExpressionBase(target, property) is not { } expression || expression.GetType().Name != "MultiBindingExpression")
        {
            return null;
        }

        if (Constant(expression, target))
        {
            plan.Follow(target, property);
            return null;
        }

        if (BindingSources.Children(expression) is { } children && children.Any(child => BindingSources.Explicit(child, target, out _)))
        {
            plan.Fail($"{target.GetType().Name}.{property.Name} is bound with a MultiBinding that reads some bindings through Source and others from the data context, which the plan cannot follow");
            return null;
        }

        var type = expression.GetType();
        var converter = Member(type, "Converter")?.GetValue(expression) as IMultiValueConverter;
        var parameter = Member(type, "ConverterParameter")?.GetValue(expression);
        var paths = (type.GetRuntimeFields().FirstOrDefault(field => field.Name == "_expressions")?.GetValue(expression) as Array)?.Cast<object?>()
            .Select(item => (item as IDescription)?.Description).ToList();
        if (converter is null || paths is null || paths.Any(path => path is null || !ElementBindings.IsSimplePath(path)) || !plan.InScope(target))
        {
            plan.Fail($"{target.GetType().Name}.{property.Name} is bound with a MultiBinding the plan cannot follow");
            return null;
        }

        plan.Follow(target, property);
        var getter = Getter([.. paths.Select(path => PropertyPaths.Getter(path!))], converter, parameter, property.PropertyType);
        var reference = Reference(converter);
        var spec = reference is null || parameter is not (null or string)
            ? null
            : new SlotSpec("multi", string.Join('|', paths), reference) { Values = [parameter as string ?? string.Empty, parameter is null ? "null" : "text", property.PropertyType.AssemblyQualifiedName!] };
        return (getter, spec);
    }

    /// <summary>Whether every render gives the binding the value it has now: its sources are app state or constants of its templated control.</summary>
    private static bool Constant(BindingExpressionBase expression, AvaloniaObject target) =>
        BindingSources.IsConstant(expression, target) || TemplateBindings.ReadsConstantTemplate(target, expression);

    private static PropertyInfo? Member(Type type, string name) => type.GetRuntimeProperties().FirstOrDefault(property => property.Name == name);

    public static ValueGetter Getter(SlotSpec spec, Runtime.Slots.MemberPaths paths)
    {
        var converter = ImmutableInterlocked.GetOrAdd(ref _resolved, spec.Argument!, Resolve);
        return Getter([.. spec.Path!.Split('|').Select(paths.Getter)], converter, spec.Values[1] == "null" ? null : spec.Values[0], Type.GetType(spec.Values[2], throwOnError: true)!);
    }

    private static ValueGetter Getter(ValueGetter[] sources, IMultiValueConverter converter, object? parameter, Type target) => scope =>
    {
        var value = BindingNotification.ExtractValue(converter.Convert([.. sources.Select(source => source(scope))], target, parameter, CultureInfo.CurrentCulture));
        return value == AvaloniaProperty.UnsetValue || value == BindingOperations.DoNothing ? null : value;
    };

    private static string? Reference(IMultiValueConverter converter)
    {
        if (converter is StringFormatMultiValueConverter format)
        {
            var inner = format.Inner is null ? string.Empty : BoundConverters.Reference(format.Inner);
            return inner is null ? null : "format:" + inner + "\n" + format.Format;
        }

        return BoundConverters.Reference(converter);
    }

    private static IMultiValueConverter Resolve(string reference)
    {
        if (!reference.StartsWith("format:", StringComparison.Ordinal))
        {
            return (IMultiValueConverter)BoundConverters.Rebuild(reference);
        }

        var line = reference.IndexOf('\n', StringComparison.Ordinal);
        var inner = reference[7..line];
        return new StringFormatMultiValueConverter(reference[(line + 1)..], inner.Length == 0 ? null : (IMultiValueConverter)BoundConverters.Rebuild(inner));
    }
}
