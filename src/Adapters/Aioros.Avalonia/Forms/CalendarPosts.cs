using System.Globalization;
using Aioros.Runtime.Slots;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Calendar = Avalonia.Controls.Calendar;

namespace Aioros.Avalonia.Forms;

/// <summary>
/// What a calendar shows, kept across posts in its hidden field as ASP.NET Web Forms' calendar keeps its visible month in
/// the view state: the date it shows (<c>yyyy-MM-dd</c>), its display mode, the first day of a tapped range still waiting
/// for its last (Avalonia's <see cref="Calendar.AllowTapRangeSelection"/>), the modifier keys the script read on the
/// click (<c>c</c> for Ctrl) and the selected days, as Web Forms keeps <c>SelectedDates</c>. A post restores them before its button is pressed, and a day or a month is pressed as
/// Avalonia's pointer presses and releases it, so the calendar's own selection runs, with what follows it.
/// </summary>
internal static class CalendarPosts
{
    private const string Day = "yyyy-MM-dd";

    private static readonly AttachedProperty<DateTime?> WaitingProperty = AvaloniaProperty.RegisterAttached<Calendar, DateTime?>("Waiting", typeof(CalendarPosts));

    private static readonly AttachedProperty<KeyModifiers> HeldProperty = AvaloniaProperty.RegisterAttached<Calendar, KeyModifiers>("Held", typeof(CalendarPosts));

    public static string State(Calendar calendar) =>
        string.Create(CultureInfo.InvariantCulture, $"{Shown(calendar).ToString(Day, CultureInfo.InvariantCulture)}|{calendar.DisplayMode}|{calendar.GetValue(WaitingProperty)?.ToString(Day, CultureInfo.InvariantCulture)}||{CalendarMonths.Runs(calendar.SelectedDates)}");

    /// <summary>Selects the days a field's runs name, as the calendar's mode allows them.</summary>
    private static void Select(Calendar calendar, string runs)
    {
        if (CalendarMonths.Days(runs) is not { } days)
        {
            return;
        }

        if (calendar.SelectionMode == CalendarSelectionMode.SingleDate)
        {
            calendar.SetCurrentValue(Calendar.SelectedDateProperty, days.Count > 0 ? days[0] : null);
            return;
        }

        calendar.SelectedDates.Clear();
        if (calendar.SelectionMode == CalendarSelectionMode.SingleRange && days.Count > 0)
        {
            calendar.SelectedDates.AddRange(days[0], days[^1]);
            return;
        }

        if (calendar.SelectionMode == CalendarSelectionMode.MultipleRange)
        {
            days.ForEach(calendar.SelectedDates.Add);
        }
    }

    public static void Bind(Calendar calendar, string? value)
    {
        var parts = (value ?? string.Empty).Split('|');
        if (parts.Length < 2 || !Date(parts[0], out var shown) || !Enum.TryParse<CalendarMode>(parts[1], out var mode) || !Enum.IsDefined(mode))
        {
            return;
        }

        calendar.SetCurrentValue(Calendar.DisplayModeProperty, CalendarMode.Month);
        if (parts.Length > 4 && calendar.SelectionMode != CalendarSelectionMode.None)
        {
            Select(calendar, parts[4]);
            Dispatcher.UIThread.RunJobs();
        }

        if (parts.Length > 2 && Date(parts[2], out var waiting) && calendar.AllowTapRangeSelection)
        {
            calendar.SetCurrentValue(Calendar.DisplayDateProperty, waiting);
            Dispatcher.UIThread.RunJobs();
            if (Days(calendar).FirstOrDefault(day => day.DataContext is DateTime date && date.Date == waiting.Date && !day.Classes.Contains(":inactive")) is { } first)
            {
                Tap(first, KeyModifiers.None);
                calendar.SetValue(WaitingProperty, waiting.Date);
            }
        }

        calendar.SetCurrentValue(Calendar.DisplayDateProperty, shown);
        calendar.SetCurrentValue(Calendar.DisplayModeProperty, mode);
        calendar.SetValue(HeldProperty, parts.Length > 3 && parts[3].Contains('c', StringComparison.Ordinal) ? KeyModifiers.Control : KeyModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Presses a calendar's day or month button as Avalonia's pointer does, or returns false for any other control.</summary>
    public static bool Press(Control control)
    {
        if (control is not (CalendarDayButton or CalendarButton) || control.GetVisualAncestors().OfType<Calendar>().FirstOrDefault() is not { } calendar)
        {
            return false;
        }

        var waiting = calendar.GetValue(WaitingProperty);
        Tap((Button)control, calendar.GetValue(HeldProperty));
        if (control is CalendarDayButton { DataContext: DateTime date } && Taps(calendar, date))
        {
            calendar.SetValue(WaitingProperty, waiting is { } start && !calendar.BlackoutDates.ContainsAny(new CalendarDateRange(Min(start, date), Max(start, date))) ? null : date.Date);
        }

        Dispatcher.UIThread.RunJobs();
        return true;
    }

    private static DateTime Min(DateTime first, DateTime second) => first <= second ? first : second;

    private static DateTime Max(DateTime first, DateTime second) => first <= second ? second : first;

    /// <summary>Whether a press of this day goes to Avalonia's tap range selection, which waits for a second day.</summary>
    private static bool Taps(Calendar calendar, DateTime date) =>
        calendar.AllowTapRangeSelection
        && calendar.SelectionMode is CalendarSelectionMode.SingleRange or CalendarSelectionMode.MultipleRange
        && !calendar.BlackoutDates.Contains(date)
        && date >= calendar.DisplayDateStart.GetValueOrDefault(DateTime.MinValue).Date
        && date <= calendar.DisplayDateEnd.GetValueOrDefault(DateTime.MaxValue).Date;

    private static IEnumerable<CalendarDayButton> Days(Calendar calendar) => calendar.GetVisualDescendants().OfType<CalendarDayButton>();

    /// <summary>The date the calendar shows: its display date in month mode, else the year or decade its year view holds.</summary>
    private static DateTime Shown(Calendar calendar)
    {
        var display = calendar.DisplayDate;
        var cells = calendar.GetVisualDescendants().OfType<CalendarButton>().OrderBy(Grid.GetRow).ThenBy(Grid.GetColumn).Select(cell => cell.DataContext).OfType<DateTime>().ToList();
        return calendar.DisplayMode switch
        {
            CalendarMode.Year when cells.Count > 0 => new DateTime(cells[0].Year, display.Month, 1, 0, 0, 0, display.Kind),
            CalendarMode.Decade when cells.Count > 1 => new DateTime(cells[1].Year + (display.Year % 10), display.Month, 1, 0, 0, 0, display.Kind),
            _ => display,
        };
    }

    private static bool Date(string text, out DateTime date) => DateTime.TryParseExact(text, Day, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static void Tap(Button button, KeyModifiers modifiers)
    {
        var root = (Visual?)TopLevel.GetTopLevel(button) ?? button;
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), root) ?? default;
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        button.RaiseEvent(new PointerPressedEventArgs(button, pointer, root, point, 0, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), modifiers));
        button.RaiseEvent(new PointerReleasedEventArgs(button, pointer, root, point, 0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), modifiers, MouseButton.Left));
    }
}
