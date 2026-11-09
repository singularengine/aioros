using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The values a page's model held before the page was bound: each simple path a control of the page or the view binds
/// from that model, read just before the model becomes its data context. A library may write into a two-way binding while
/// the page renders (Aioros.Cosmo's <c>c:Command</c> selects its first item), so the model a page is compiled with no
/// longer holds what a fresh request's model holds; a plan learns a library value by both (ENGINE-268).
/// </summary>
internal static class FreshValues
{
    private static readonly AttachedProperty<FreshModel?> SnapshotProperty =
        AvaloniaProperty.RegisterAttached<StyledElement, FreshModel?>("FreshSnapshot", typeof(FreshValues));

    /// <summary>Reads, from the model the control is about to show, the value of each simple path its bindings read from it.</summary>
    public static void Snapshot(Control control, object? model)
    {
        if (model is null || control.GetValue(SnapshotProperty) is { } held && ReferenceEquals(held.Model, model))
        {
            return;
        }

        var values = ImmutableDictionary.CreateBuilder<string, object?>(StringComparer.Ordinal);
        Read(control, control, model, values);
        control.SetValue(SnapshotProperty, new FreshModel(model, values.ToImmutable()));
    }

    /// <summary>The value the path held in the scope before the page was bound, when a control between the target and the page's root read it then.</summary>
    public static bool TryRead(Control target, object? scope, string path, out object? value)
    {
        foreach (var element in target.GetSelfAndLogicalAncestors().OfType<Control>())
        {
            if (element.GetValue(SnapshotProperty) is { } fresh && ReferenceEquals(fresh.Model, scope) && fresh.Values.TryGetValue(path, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    private static void Read(Control root, StyledElement element, object model, ImmutableDictionary<string, object?>.Builder values)
    {
        if (!ReferenceEquals(element, root) && (element.IsSet(StyledElement.DataContextProperty) || LocalBindings.HasBinding(element, StyledElement.DataContextProperty)))
        {
            return;
        }

        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(element))
        {
            if (property != StyledElement.DataContextProperty && BindingOperations.GetBindingExpressionBase(element, property) is IDescription { Description: { } path } expression
                && !values.ContainsKey(path) && ElementBindings.IsSimplePath(path) && !BindingSources.Explicit((BindingExpressionBase)expression, element, out _)
                && Value(model, path) is var (read, value) && read)
            {
                values[path] = value;
            }
        }

        foreach (var child in ((ILogical)element).LogicalChildren.OfType<StyledElement>())
        {
            Read(root, child, model, values);
        }
    }

    private static (bool Read, object? Value) Value(object model, string path)
    {
        try
        {
            return (true, PropertyPaths.Getter(path)(model));
        }
        catch (Exception error) when (error is InvalidOperationException or NullReferenceException or ArgumentException)
        {
            return (false, null);
        }
    }

    private sealed record FreshModel(object Model, ImmutableDictionary<string, object?> Values);
}
