using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Slots;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Forms;

/// <summary>
/// The inputs whose value is the page's own state: Avalonia's own controls, none of their value properties bound, written
/// once in the page (inside no container a list generated for an item), of a value a post's text parses to without
/// Avalonia (<see cref="LocalPaths"/>), and observed by no code outside the ecosystem (<see cref="GuaranteedInputs"/>), each
/// marked when the guarantee does not cover it; a named one is read
/// by the bindings to it (<c>#name.IsChecked</c>) through the same path. Each is keyed in the logical tree's order, so the
/// plan reads and a post writes the same place.
/// </summary>
internal static class LocalInputs
{
    /// <summary>
    /// The page's local inputs, each keyed by its place among the candidates in the logical tree's order, so another
    /// realization of the page keys the same inputs alike, and whether the guarantee covers it.
    /// </summary>
    public static ImmutableDictionary<AvaloniaObject, LocalInput> Of(Control root)
    {
        var keys = ImmutableDictionary.CreateBuilder<AvaloniaObject, LocalInput>(ReferenceEqualityComparer.Instance);
        var read = GuaranteedInputs.ReadByLibraries(root);
        var writtenBack = WrittenBack(root);
        var key = 0;
        foreach (var control in Written(root))
        {
            if (Value(control) is { } property && LocalPaths.Holds(property.PropertyType) && Once(control) && GuaranteedInputs.Unobserved(control, read) && (control.Name is not { } name || !writtenBack.Contains(name)))
            {
                keys[control] = new LocalInput(key, GuaranteedInputs.Covers(control));
            }

            key += Value(control) is { } candidate && LocalPaths.Holds(candidate.PropertyType) && Once(control) ? 1 : 0;
        }

        return keys.ToImmutable();
    }

    /// <summary>
    /// The names of the inputs another element's binding writes back (<c>IsPaneOpen="{Binding #toggle.IsChecked, Mode=TwoWay}"</c>):
    /// their value is not the page's own state alone, since that element changes it.
    /// </summary>
    private static HashSet<string> WrittenBack(Control root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in root.GetVisualDescendants().Concat(root.GetLogicalDescendants()).Prepend(root).OfType<AvaloniaObject>().Distinct())
        {
            foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(element))
            {
                if (LocalBindings.Of(element, property) is { } binding && binding is IDescription { Description: ['#', .. var path] } && BindingModes.WritesToSource(binding))
                {
                    names.Add(path.Split('.')[0]);
                }
            }
        }

        return names;
    }

    /// <summary>
    /// The page's controls in the logical tree's order, then those only its visual tree holds that no template built (a
    /// page type's header and bar, which it shows without making them its logical children), as the page's writer meets them.
    /// </summary>
    private static IEnumerable<Control> Written(Control root)
    {
        var logical = root.GetLogicalDescendants().Prepend(root).OfType<Control>().ToList();
        var seen = logical.ToHashSet();
        return logical.Concat(root.GetVisualDescendants().OfType<Control>().Where(control => control.TemplatedParent is null && !seen.Contains(control)));
    }

    /// <summary>The value property of an input none of whose value properties is bound to write back, or nothing.</summary>
    public static AvaloniaProperty? Value(Control control) => control switch
    {
        _ when GuaranteedInputs.Foreign(control.GetType()) => null,
        RadioButton => null,
        ToggleButton toggle when toggle.Command is null => Unbound(control, ToggleButton.IsCheckedProperty),
        TextBox => Unbound(control, TextBox.TextProperty),
        AutoCompleteBox => Unbound(control, AutoCompleteBox.TextProperty, AutoCompleteBox.SelectedItemProperty),
        NumericUpDown => Unbound(control, NumericUpDown.ValueProperty),
        Slider => Unbound(control, RangeBase.ValueProperty),
        ToggleSplitButton toggle when toggle.Command is null => Unbound(control, ToggleSplitButton.IsCheckedProperty),
        PipsPager => Unbound(control, PipsPager.SelectedPageIndexProperty),
        CalendarDatePicker => Unbound(control, CalendarDatePicker.SelectedDateProperty),
        DatePicker => Unbound(control, DatePicker.SelectedDateProperty),
        TimePicker => Unbound(control, TimePicker.SelectedTimeProperty),
        MenuItem => null,
        ListBox { SelectionMode: var mode } when mode.HasFlag(SelectionMode.Multiple) => null,
        ItemsControl list when Repeats(list) => null,
        SelectingItemsControl => Unbound(control, SelectingItemsControl.SelectedIndexProperty, SelectingItemsControl.SelectedItemProperty, SelectingItemsControl.SelectedValueProperty),
        _ => null,
    };

    /// <summary>
    /// A value of the input's own domain other than <paramref name="current"/>: a flag flipped, a text changed, a number
    /// at the other end of its range, the next item, another date; nothing when the domain holds no other.
    /// </summary>
    public static object? Other(Control control, object? current) => control switch
    {
        ToggleButton or ToggleSplitButton => current is not true,
        PipsPager pager when pager.NumberOfPages > 1 => ((current as int? ?? 0) + 1) % pager.NumberOfPages,
        TextBox or AutoCompleteBox => (current as string) + "x",
        NumericUpDown number => (current as decimal? ?? number.Minimum) == number.Minimum ? number.Maximum : number.Minimum,
        RangeBase range => current is double value && value <= range.Minimum ? range.Maximum : range.Minimum,
        CalendarDatePicker => (current as DateTime? ?? new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Unspecified)).AddDays(1),
        DatePicker => (current as DateTimeOffset? ?? new DateTimeOffset(2026, 1, 14, 0, 0, 0, TimeSpan.Zero)).AddDays(1),
        TimePicker => (current as TimeSpan? ?? new TimeSpan(9, 30, 0)).Add(TimeSpan.FromHours(1)),
        SelectingItemsControl list when list.ItemCount > 1 => ((current as int? ?? -1) + 1) % list.ItemCount,
        _ => null,
    };

    /// <summary>Whether two of the list's items are equal, a choice among which Avalonia resolves to the first: an index would not stand for it.</summary>
    private static bool Repeats(ItemsControl list) =>
        list.Items.Cast<object?>().Where(static item => item is not Control).GroupBy(static item => item).Any(static group => group.Count() > 1);

    private static AvaloniaProperty? Unbound(Control control, params AvaloniaProperty[] properties) =>
        properties.All(property => LocalBindings.Of(control, property) is null) ? properties[0] : null;

    /// <summary>Whether the control is written once: inside no container a list generated for one of its items (a template's controls repeat for each item).</summary>
    private static bool Once(Control control) =>
        !control.GetLogicalAncestors().OfType<Control>().Any(static ancestor => ancestor.GetLogicalParent() is ItemsControl list && list.ItemFromContainer(ancestor) is { } item && !ReferenceEquals(item, ancestor));
}

/// <summary>A local input's key in the page's own state, and whether the guarantee covers it.</summary>
internal readonly record struct LocalInput(int Key, bool Guaranteed);
