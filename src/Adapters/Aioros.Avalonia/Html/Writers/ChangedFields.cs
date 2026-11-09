using System.Globalization;
using Aioros.Avalonia.Plans;
using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// The fields the framework's script posts as they change, as a two-way selection is (<c>data-change</c>): the fields of
/// a table bound two way (its row and header check boxes, TanStack Table's row selection), and a text box with
/// <c>a:Web.Delay</c> or bound two way with a <c>Delay</c>, posted that long after the visitor's last keystroke
/// (<c>data-delay</c>; LiveView's <c>phx-debounce</c>, WPF's and Avalonia's <c>Binding.Delay</c>).
/// </summary>
internal static class ChangedFields
{
    /// <summary><c> data-change</c> when a control of the container's own content (no template part) binds a property two way.</summary>
    public static string Posts(Control container) =>
        container.GetVisualDescendants().OfType<Control>().Any(control => control.TemplatedParent is null && TwoWay(control)) ? " data-change" : string.Empty;

    /// <summary>
    /// <c> data-change data-delay="…"</c>, in milliseconds, for a text box with <c>a:Web.Delay</c> above 0, or, without the
    /// hint, whose property is bound two way with a <c>Delay</c>; <c> data-change</c> for a date picker's text box, posted
    /// when it changes as the picker parses its text when it loses the focus; nothing for a hint of 0.
    /// </summary>
    public static string Delayed(Control control, AvaloniaProperty property)
    {
        var hint = Web.GetDelay(control);
        var delay = !double.IsNaN(hint) ? hint
            : LocalBindings.Of(control, property) is { } binding && BindingModes.WritesToSource(binding) ? BindingModes.Delay(binding).TotalMilliseconds
            : 0;
        return delay > 0 ? string.Create(CultureInfo.InvariantCulture, $" data-change data-delay=\"{(long)delay}\"")
            : control is TextBox { Name: "PART_TextBox", TemplatedParent: CalendarDatePicker } ? " data-change"
            : string.Empty;
    }

    private static bool TwoWay(Control control) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(control).Any(property => !property.IsReadOnly && LocalBindings.Of(control, property) is { } binding && BindingModes.WritesToSource(binding));
}
