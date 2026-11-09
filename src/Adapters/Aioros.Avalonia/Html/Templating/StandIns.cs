using System.Reflection;
using System.Runtime.CompilerServices;
using Aioros.Avalonia.Hosting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup.Xaml.Templates;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// Lets the plan learn the item template of a list that is empty when the page is compiled: one stand-in item of the
/// template's declared data type fills the list above its binding while the template is written, then goes away. The
/// slots come from the bindings' paths, so the stand-in's values never reach a page.
/// </summary>
internal static class StandIns
{
    public static IDisposable? Fill(ItemsControl items) =>
        ((items.ItemTemplate as DataTemplate)?.DataType ?? ItemType(items.ItemsSource?.GetType())) is { } type ? Fill(items, type) : null;

    /// <summary>Fills the list with one stand-in of the given type, or nothing when no stand-in can be made of it.</summary>
    public static IDisposable? Fill(ItemsControl items, Type type)
    {
        if ((type.IsAbstract || type.IsInterface ? Implementation(type) : type) is not { } concrete)
        {
            return null;
        }

        var item = concrete == typeof(string) ? string.Empty : concrete == typeof(object) ? new object() : RuntimeHelpers.GetUninitializedObject(concrete);
        return Fill(items, item);
    }

    /// <summary>Fills the list with the given item alone, above its binding, which the plan still reads.</summary>
    public static IDisposable? Fill(ItemsControl items, object item) => Fill(items, [item]);

    /// <summary>
    /// Fills the list with the given items, above its binding, which the plan still reads; a selection bound two way is
    /// held at its value meanwhile, so the stand-ins never clear the model's selection.
    /// </summary>
    public static IDisposable? Fill(ItemsControl items, object?[] list)
    {
        var held = items is SelectingItemsControl selecting ? Selection.Select(property => Hold(selecting, property)).OfType<IDisposable>().ToList() : [];
        var lease = Plans.LocalBindings.Override(items, ItemsControl.ItemsSourceProperty, list);
        Realization.Realize(items);
        TopLevel.GetTopLevel(items)?.UpdateLayout();
        return new Leases([lease, .. Enumerable.Reverse(held)]);
    }

    /// <summary>A concrete class of an interface or an abstract class, from the loaded assemblies that can name it, the first by name so a compile is repeatable.</summary>
    private static Type? Implementation(Type type)
    {
        var declaring = type.Assembly.GetName().Name;
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && (assembly == type.Assembly || assembly.GetReferencedAssemblies().Any(reference => reference.Name == declaring)))
            .SelectMany(Types)
            .Where(candidate => candidate is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false } && type.IsAssignableFrom(candidate))
            .OrderBy(static candidate => candidate.FullName, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException partial)
        {
            return partial.Types.OfType<Type>();
        }
    }

    private static readonly AvaloniaProperty[] Selection =
        [SelectingItemsControl.SelectedItemProperty, SelectingItemsControl.SelectedIndexProperty, SelectingItemsControl.SelectedValueProperty];

    private static IDisposable? Hold(SelectingItemsControl list, AvaloniaProperty property) =>
        BindingOperations.GetBindingExpressionBase(list, property) is { } binding && Plans.BindingModes.WritesToSource(binding)
            ? Plans.LocalBindings.Override(list, property, list.GetValue(property))
            : null;

    private sealed class Leases(IReadOnlyList<IDisposable?> leases) : IDisposable
    {
        public void Dispose()
        {
            foreach (var lease in leases)
            {
                lease?.Dispose();
            }
        }
    }

    /// <summary>The type of a list's items, from its <c>IEnumerable&lt;T&gt;</c>, or nothing for a list that does not say.</summary>
    public static Type? ItemType(Type? list) =>
        list?.GetInterfaces().Append(list).FirstOrDefault(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
}
