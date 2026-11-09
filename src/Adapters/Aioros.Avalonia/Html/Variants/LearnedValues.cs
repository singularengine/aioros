using System.Collections.Immutable;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>
/// Bound values no slot can write, learned per value as Avalonia renders each, and each render picks the case of its value
/// (<see cref="LearnedNode"/>): a geometry bound through a converter (Ursa's demo turns each menu item's name into a
/// <c>StreamGeometry</c>; no public API gives a geometry's figures and a geometry is thread-affine), written for each value
/// of the binding's source the compiled page holds with the geometry Avalonia's binding gave the control that shows it; and
/// a bound <c>ThemeVariant</c> (a <c>ThemeVariantScope</c>'s), written for each variant the app declares.
/// </summary>
internal static class LearnedValues
{
    /// <summary>Whether the property is a geometry bound through a converter, which the plan learns per value.</summary>
    public static bool Learns(AvaloniaObject control, AvaloniaProperty property) =>
        LocalBindings.Of(control, property) is { } binding
        && (typeof(Geometry).IsAssignableFrom(property.PropertyType) ? BindingFormats.Converter(binding) is not null : property.PropertyType == typeof(ThemeVariant) && BindingFormats.Converter(binding) is null);

    public static AvaloniaProperty? Next(Control control, HashSet<(Control, AvaloniaProperty)> expanding) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(control).FirstOrDefault(property => !expanding.Contains((control, property)) && Learns(control, property));

    public static void WriteVariants(PlanRecorder plan, Control control, AvaloniaProperty property, HashSet<(Control, AvaloniaProperty)> expanding, Action write)
    {
        expanding.Add((control, property));
        try
        {
            if (plan.BoundPath(control, property) is not { } path || path.StartsWith('^'))
            {
                plan.Fail($"{control.GetType().Name}.{property.Name} is bound through a converter to a value outside the item's scope, which the plan cannot learn");
                return;
            }

            var keys = ImmutableArray.CreateBuilder<string>();
            var bodies = ImmutableArray.CreateBuilder<ImmutableArray<IPlanNode>>();
            using (plan.Learning(control, property))
            {
                foreach (var (key, value) in property.PropertyType == typeof(ThemeVariant) ? Variants() : Values(control, property, path))
                {
                    using (LocalBindings.Override(control, property, value))
                    {
                        Settled.Rebuilt(control);
                        plan.Begin(plan.Scope);
                        write();
                        keys.Add(key);
                        bodies.Add(plan.End());
                    }
                }
            }

            Settled.Rebuilt(control);
            var source = SlotSpec.Bound(path);
            plan.Add(new LearnedNode(Slots.Getter(source), keys.ToImmutable(), bodies.ToImmutable()) { Source = source });
        }
        finally
        {
            expanding.Remove((control, property));
        }
    }

    /// <summary>
    /// The variants the app can show: none, Avalonia's own, and every variant a theme dictionary of the application's
    /// resources and styles is keyed by (Semi's <c>Aquatic</c>, <c>Desert</c>...).
    /// </summary>
    private static List<(string Key, object? Value)> Variants()
    {
        var variants = new List<ThemeVariant?> { null, ThemeVariant.Default, ThemeVariant.Light, ThemeVariant.Dark };
        if (Application.Current is { } application)
        {
            Collect(application.Resources, variants);
            foreach (var style in application.Styles)
            {
                Collect(style, variants);
            }
        }

        return [.. variants.Distinct().Select(variant => (LearnedNode.KeyOf(variant), (object?)variant))];
    }

    private static void Collect(object? node, List<ThemeVariant?> variants)
    {
        switch (node)
        {
            case IResourceDictionary dictionary:
                variants.AddRange(dictionary.ThemeDictionaries.Keys);
                foreach (var merged in dictionary.MergedDictionaries)
                {
                    Collect(merged, variants);
                }

                break;
            case Styles styles:
                Collect(styles.Resources, variants);
                foreach (var style in styles)
                {
                    Collect(style, variants);
                }

                break;
            case StyleBase style:
                Collect(style.Resources, variants);
                foreach (var child in style.Children)
                {
                    Collect(child, variants);
                }

                break;
        }
    }

    /// <summary>
    /// The distinct values of the binding's source among the page's controls of the same type that bind the property the same
    /// way (the same path and converter), each with the value Avalonia's binding gave that control.
    /// </summary>
    private static List<(string Key, object? Value)> Values(Control control, AvaloniaProperty property, string path)
    {
        var binding = LocalBindings.Of(control, property)!;
        var converter = BindingFormats.Converter(binding)!.GetType();
        var description = (binding as IDescription)?.Description;
        var read = PropertyPaths.Getter(path);
        var found = new List<(string Key, object? Value)>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var root = (Visual?)TopLevel.GetTopLevel(control) ?? control;
        foreach (var instance in root.GetVisualDescendants().OfType<Control>().Prepend(control).Where(candidate => candidate.GetType() == control.GetType()))
        {
            if (LocalBindings.Of(instance, property) is { } other && BindingFormats.Converter(other)?.GetType() == converter
                && (other as IDescription)?.Description == description && LearnedNode.KeyOf(read(instance.DataContext)) is var key && keys.Add(key))
            {
                found.Add((key, instance.GetValue(property)));
            }
        }

        return found;
    }
}
