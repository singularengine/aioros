using Aioros.Markup;
using System.Globalization;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Variants;
using Aioros.Avalonia.Html.Writers;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Aioros.Avalonia.Forms;

internal static class FieldSpecs
{
    public static SlotSpec? Of(PlanRecorder plan, Control control) => plan.Planning ? Describe(plan, control) : null;

    private static SlotSpec? Describe(PlanRecorder plan, Control control) => control switch
    {
        _ when LibraryCommands.Inside(control) => null,
        _ when Html.Variants.BrowserModals.Property(control) is { } open => Written(plan, control, open) is { } path ? new SlotSpec("toggle", path, "on") : null,
        _ when Drag.IsTarget(control) => ActionFieldSpecs.Drop(plan, control),
        FilePicker => null,
        ColorPicker picker => Simple(plan, picker, ColorView.ColorProperty, "color"),
        CheckBox check => Check(plan, check),
        RadioButton radio => Radios(plan, radio),
        CommandBarToggleButton toggle => Toggle(plan, toggle, "on"),
        ToggleButton toggle => Toggle(plan, toggle, Enabled(toggle)),
        ToggleSplitButton toggle => Toggle(plan, toggle, Enabled(toggle)),
        MaskedTextBox => null,
        TextBox box => Text(plan, box, TextBox.TextProperty) ?? Kept(box, TextBox.TextProperty),
        AutoCompleteBox box => Text(plan, box, AutoCompleteBox.TextProperty) ?? Kept(box, AutoCompleteBox.TextProperty),
        NumericUpDown number => Number(plan, number) ?? Kept(number, NumericUpDown.ValueProperty),
        Slider slider => Slider(plan, slider),
        CalendarDatePicker picker => Simple(plan, picker, CalendarDatePicker.SelectedDateProperty, "date"),
        DatePicker picker => Simple(plan, picker, DatePicker.SelectedDateProperty, "date-offset"),
        TimePicker picker => Simple(plan, picker, TimePicker.SelectedTimeProperty, "time"),
        ComboBox combo => Combo(plan, combo) ?? Kept(combo, SelectingItemsControl.SelectedIndexProperty, SelectingItemsControl.SelectedItemProperty, SelectingItemsControl.SelectedValueProperty),
        PipsPager pager => Page(plan, pager),
        Panel panel when Html.Variants.BrowserFlags.Property(panel) is { } flag => Written(plan, panel, flag) is { } path ? new SlotSpec("toggle", path, "on") : null,
        Expander expander => Written(plan, expander, Expander.IsExpandedProperty) is { } open ? new SlotSpec("toggle", open, "on") : null,
        TabbedPage tabbed => Tabs(plan, tabbed),
        SelectingItemsControl list => Choice(plan, list),
        Button { TemplatedParent: PipsPager pager } button when PagerWriters.IsStep(button) => Step(plan, pager, button),
        Button button when !PagerWriters.IsStep(button) => ActionFieldSpecs.Command(plan, button),
        _ => null,
    };

    /// <summary>
    /// A radio group's field (<see cref="RadioFields"/>), written once for the group: each radio's checked state bound to
    /// write back, through its converter or not, or not bound at all; nothing when one is bound in a way the plan cannot
    /// write back (a converter it cannot rebuild).
    /// </summary>
    private static SlotSpec? Radios(PlanRecorder plan, RadioButton radio)
    {
        var radios = new List<SlotSpec>();
        foreach (var member in Html.Writers.ToggleWriters.RadioGroup(radio))
        {
            if (Radio(plan, member) is not { } spec)
            {
                return null;
            }

            radios.Add(spec);
        }

        return RadioFields.Spec(radios);
    }

    private static SlotSpec? Radio(PlanRecorder plan, RadioButton radio)
    {
        if (LocalBindings.Of(radio, ToggleButton.IsCheckedProperty) is not { } binding || !BindingModes.WritesToSource(binding))
        {
            return new SlotSpec("none");
        }

        return BoundConverters.Of(plan, radio, ToggleButton.IsCheckedProperty) is { } converted
            ? converted.Spec
            : Written(plan, radio, ToggleButton.IsCheckedProperty) is { } path ? new SlotSpec("check", path) : null;
    }

    private static SlotSpec? Check(PlanRecorder plan, CheckBox check) =>
        check.Command is null && Written(plan, check, ToggleButton.IsCheckedProperty) is { } path
            ? new SlotSpec("check", path, Enabled(check)) { Values = [check.IsThreeState ? "three" : "two"] }
            : null;

    private static SlotSpec? Number(PlanRecorder plan, NumericUpDown number) =>
        Written(plan, number, NumericUpDown.ValueProperty) is { } path
            ? new SlotSpec("number", path) { Values = [Invariant(number.Minimum), Invariant(number.Maximum), number.ClipValueToMinMax ? "clip" : "keep"] }
            : null;

    private static SlotSpec? Slider(PlanRecorder plan, Slider slider) =>
        Written(plan, slider, RangeBase.ValueProperty) is { } path ? new SlotSpec("slider", path) { Values = [Invariant(slider.Minimum), Invariant(slider.Maximum)] } : null;

    private static SlotSpec? Page(PlanRecorder plan, PipsPager pager) =>
        Written(plan, pager, PipsPager.SelectedPageIndexProperty) is { } path
            ? new SlotSpec("page", path, Slots.Operand(plan.PathOf(pager, PipsPager.NumberOfPagesProperty), pager.NumberOfPages))
            : null;

    private static SlotSpec? Step(PlanRecorder plan, PipsPager pager, Button button) =>
        Written(plan, pager, PipsPager.SelectedPageIndexProperty) is { } path
            ? new SlotSpec("page-step", path, Slots.Operand(plan.PathOf(pager, PipsPager.NumberOfPagesProperty), pager.NumberOfPages)) { Values = [PagerWriters.StepOf(button).ToString(CultureInfo.InvariantCulture)] }
            : null;

    private static SlotSpec? Tabs(PlanRecorder plan, TabbedPage tabbed) =>
        Written(plan, tabbed, TabbedPage.SelectedIndexProperty) is { } path
            ? new SlotSpec("choice-index", path) { Values = [(tabbed.Pages?.Cast<object>().Count() ?? 0).ToString(CultureInfo.InvariantCulture)] }
            : null;

    private static SlotSpec? Toggle(PlanRecorder plan, ToggleButton toggle, string enabled) =>
        toggle.Command is null && Written(plan, toggle, ToggleButton.IsCheckedProperty) is { } path ? new SlotSpec("toggle", path, enabled) : null;

    private static SlotSpec? Toggle(PlanRecorder plan, ToggleSplitButton toggle, string enabled) =>
        toggle.Command is null && Written(plan, toggle, ToggleSplitButton.IsCheckedProperty) is { } path ? new SlotSpec("toggle", path, enabled) : null;

    private static SlotSpec? Text(PlanRecorder plan, Control box, AvaloniaProperty property) => Simple(plan, box, property, "text");

    private static SlotSpec? Simple(PlanRecorder plan, Control control, AvaloniaProperty property, string kind) =>
        Written(plan, control, property) is { } path ? new SlotSpec(kind, path) : null;

    private static SlotSpec? Combo(PlanRecorder plan, ComboBox combo)
    {
        if (Written(plan, combo, SelectingItemsControl.SelectedIndexProperty) is { } index)
        {
            return new SlotSpec("choice-index", index, plan.PathOf(combo, ItemsControl.ItemsSourceProperty)) { Values = [combo.ItemCount.ToString(CultureInfo.InvariantCulture)] };
        }

        if (plan.PathOf(combo, SelectingItemsControl.SelectedIndexProperty) is not null || Written(plan, combo, SelectingItemsControl.SelectedItemProperty) is not { } path)
        {
            return null;
        }

        if (plan.PathOf(combo, ItemsControl.ItemsSourceProperty) is { } items)
        {
            return new SlotSpec("combo", path, items);
        }

        if (combo.Items.All(item => item is string))
        {
            return new SlotSpec("combo", path) { Values = [.. combo.Items.Cast<string>()] };
        }

        return combo.ItemsSource is { } list && BoundConverters.StaticMember(list, combo.Items.Select(item => item?.GetType()).OfType<Type>().Distinct()) is { } member
            ? new SlotSpec("combo", path, member)
            : null;
    }

    private static SlotSpec? Choice(PlanRecorder plan, SelectingItemsControl list)
    {
        if (list is MenuItem)
        {
            return ActionFieldSpecs.Flip(plan, list);
        }

        if (list is ListBox box && box.SelectionMode.HasFlag(SelectionMode.Multiple))
        {
            return null;
        }

        var items = plan.PathOf(list, ItemsControl.ItemsSourceProperty);
        if (Written(plan, list, SelectingItemsControl.SelectedIndexProperty) is { } index)
        {
            return new SlotSpec("choice-index", index, items) { Values = [list.ItemCount.ToString(CultureInfo.InvariantCulture)] };
        }

        return items is not null && Written(plan, list, SelectingItemsControl.SelectedItemProperty) is { } item ? new SlotSpec("choice-item", item, items) : null;
    }

    /// <summary>
    /// A kept field: a value input none of whose value properties is bound to write back, so a post
    /// without it changes nothing on the engine either (<see cref="PlanForms.KeptKind"/>); a check box is never one, since
    /// its absence from a post unchecks it.
    /// </summary>
    private static SlotSpec? Kept(Control control, params AvaloniaProperty[] properties) =>
        properties.All(property => LocalBindings.Of(control, property) is not { } binding || !BindingModes.WritesToSource(binding))
            ? new SlotSpec(PlanForms.KeptKind)
            : null;

    /// <summary>
    /// Whether a value input's value goes nowhere (SSR-292): no name another control may read it by, and none of its value
    /// properties bound to write back, as a showcase's controls are; the browser then does not count its edits as the
    /// visitor's unsaved changes or keep them as drafts.
    /// </summary>
    public static bool IsLocal(Control control) => string.IsNullOrEmpty(control.Name) && control switch
    {
        ToggleButton toggle => toggle.Command is null && Unwritten(toggle, ToggleButton.IsCheckedProperty),
        TextBox box => Unwritten(box, TextBox.TextProperty),
        AutoCompleteBox box => Unwritten(box, AutoCompleteBox.TextProperty, AutoCompleteBox.SelectedItemProperty),
        NumericUpDown number => Unwritten(number, NumericUpDown.ValueProperty),
        Slider slider => Unwritten(slider, RangeBase.ValueProperty),
        ComboBox combo => Unwritten(combo, SelectingItemsControl.SelectedIndexProperty, SelectingItemsControl.SelectedItemProperty, SelectingItemsControl.SelectedValueProperty),
        CalendarDatePicker picker => Unwritten(picker, CalendarDatePicker.SelectedDateProperty),
        DatePicker picker => Unwritten(picker, DatePicker.SelectedDateProperty),
        TimePicker picker => Unwritten(picker, TimePicker.SelectedTimeProperty),
        ColorPicker picker => Unwritten(picker, ColorView.ColorProperty),
        _ => false,
    };

    private static bool Unwritten(Control control, params AvaloniaProperty[] properties) =>
        properties.All(property => LocalBindings.Of(control, property) is not { } binding || !BindingModes.WritesToSource(binding));

    internal static string? Written(PlanRecorder plan, AvaloniaObject control, AvaloniaProperty property) =>
        plan.PathOf(control, property) is { } path && (LocalPaths.TryRead(path, out _) || (LocalBindings.Of(control, property) is { } binding && BindingModes.WritesToSource(binding))) ? path : null;

    private static string Enabled(Control control) => control.IsEffectivelyEnabled ? "on" : "off";

    private static string Invariant(IFormattable number) => number.ToString(null, CultureInfo.InvariantCulture);
}
