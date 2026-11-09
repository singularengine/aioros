using System.Collections.Immutable;
using System.Reflection;
using Avalonia;
using Avalonia.Data;
using Avalonia.LogicalTree;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The explicit <c>Source</c> of a binding. Avalonia resolves it once, when the binding is created, and reads its path
/// from that object instead of the target's data context; a binding to the data context, to an element
/// (<c>ElementName</c>, <c>#name</c>) or to a relative source starts at a source node, from the target or the control
/// it is anchored to (a transform's), and <c>RelativeSource Self</c> keeps the target itself as its source.
/// </summary>
internal static class BindingSources
{
    private static ImmutableDictionary<Type, FieldInfo?> _sources = ImmutableDictionary<Type, FieldInfo?>.Empty;
    private static ImmutableDictionary<Type, FieldInfo?> _children = ImmutableDictionary<Type, FieldInfo?>.Empty;
    private static ImmutableDictionary<Type, FieldInfo?> _nodes = ImmutableDictionary<Type, FieldInfo?>.Empty;

    /// <summary>Whether the binding reads an explicit <c>Source</c>, and that source (null when it is set to null).</summary>
    public static bool Explicit(BindingExpressionBase expression, AvaloniaObject target, out object? source)
    {
        source = null;
        var field = ImmutableInterlocked.GetOrAdd(ref _sources, expression.GetType(), static type => type.GetRuntimeFields().FirstOrDefault(item => item.Name == "_source" && item.FieldType == typeof(WeakReference<object?>)));
        if (field?.GetValue(expression) is not WeakReference<object?> reference)
        {
            return false;
        }

        reference.TryGetTarget(out source);
        return !ReferenceEquals(source, target) && !StartsAtSourceNode(expression);
    }

    /// <summary>Whether the binding reads the target itself (<c>$self</c>, <c>RelativeSource Self</c>), not its data context.</summary>
    public static bool ReadsSelf(BindingExpressionBase expression, AvaloniaObject target)
    {
        var field = ImmutableInterlocked.GetOrAdd(ref _sources, expression.GetType(), static type => type.GetRuntimeFields().FirstOrDefault(item => item.Name == "_source" && item.FieldType == typeof(WeakReference<object?>)));
        return field?.GetValue(expression) is WeakReference<object?> reference && reference.TryGetTarget(out var source) && ReferenceEquals(source, target) && !StartsAtSourceNode(expression);
    }

    private static bool StartsAtSourceNode(BindingExpressionBase expression)
    {
        var field = ImmutableInterlocked.GetOrAdd(ref _nodes, expression.GetType(), static type => type.GetRuntimeFields().FirstOrDefault(item => item.Name == "_nodes"));
        if (field?.GetValue(expression) is not System.Collections.IList { Count: > 0 } nodes)
        {
            return false;
        }

        for (var type = nodes[0]?.GetType(); type is not null; type = type.BaseType)
        {
            if (type.Name == "SourceNode")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the binding reads only explicit sources that are not the page's data: neither an element nor the data
    /// context of the target or of one of its ancestors. Such a source (an <c>x:Static</c> member, a resource, an object a
    /// markup extension returns) is app state, which the plan writes as rendered, as it writes resources.
    /// </summary>
    public static bool IsConstant(BindingExpressionBase expression, AvaloniaObject target)
    {
        if (Children(expression) is { } children)
        {
            return children.Length > 0 && children.All(child => IsConstant(child, target));
        }

        return Explicit(expression, target, out var source) && source is not AvaloniaObject && !IsDataContext(source, target);
    }

    /// <summary>The bindings of a <c>MultiBinding</c>, or nothing for any other binding.</summary>
    public static ImmutableArray<BindingExpressionBase>? Children(BindingExpressionBase expression)
    {
        var field = ImmutableInterlocked.GetOrAdd(ref _children, expression.GetType(), static type => type.Name == "MultiBindingExpression" ? type.GetRuntimeFields().FirstOrDefault(item => item.Name == "_expressions") : null);
        return field?.GetValue(expression) is Array items ? [.. items.OfType<BindingExpressionBase>()] : null;
    }

    private static bool IsDataContext(object? source, AvaloniaObject target) =>
        source is not null && target is StyledElement element
        && element.GetSelfAndLogicalAncestors().OfType<StyledElement>().Any(ancestor => ReferenceEquals(ancestor.DataContext, source));
}
