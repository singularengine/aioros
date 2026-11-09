using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Calendar = Avalonia.Controls.Calendar;

namespace Aioros.Avalonia.Forms;

/// <summary>
/// A date picker's posts as Avalonia's picker takes them: a button of its popup's calendar other than a day (the months'
/// navigation and caption) keeps the popup open, as the popup stays open on the desktop while the visitor moves through
/// months, where a day closes it; and the text posted into its text box is parsed in the current culture as the picker
/// parses it when Enter is pressed in it.
/// </summary>
internal static class DatePickerPosts
{
    private static readonly AttachedProperty<bool> KeptOpenProperty = AvaloniaProperty.RegisterAttached<CalendarDatePicker, bool>("KeptOpen", typeof(DatePickerPosts));

    public static bool KeptOpen(CalendarDatePicker picker) => picker.GetValue(KeptOpenProperty);

    /// <summary>
    /// Keeps a picker's popup open after a post from a control inside it that is not a day (the calendar's navigation, a
    /// select a theme puts above the calendar, as Aioros.Cosmo's presets), as the visitor could press it only while open.
    /// </summary>
    public static void Pressed(Control pressed)
    {
        if (pressed is CalendarDayButton or TextBox)
        {
            return;
        }

        var picker = pressed.GetVisualAncestors().OfType<Calendar>().FirstOrDefault()?.TemplatedParent as CalendarDatePicker ?? pressed.TemplatedParent as CalendarDatePicker;
        if (picker is not null && pressed.Name != "PART_Button")
        {
            picker.SetValue(KeptOpenProperty, true);
        }
    }

    public static void Typed(TextBox box)
    {
        if (box is { Name: "PART_TextBox", TemplatedParent: CalendarDatePicker })
        {
            box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = box, Key = Key.Enter });
        }
    }
}
