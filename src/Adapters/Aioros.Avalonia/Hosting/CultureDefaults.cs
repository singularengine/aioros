using System.Globalization;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Calendar = Avalonia.Controls.Calendar;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// What Avalonia takes from the culture once, when a control type is first used, given again from the culture the page
/// renders in: a calendar's and a date picker's first day of the week, which Avalonia reads from the culture of the
/// first render in the process, so pages of several cultures would otherwise all start their weeks on that one's day.
/// A calendar whose app or theme sets its first day keeps it.
/// </summary>
internal static class CultureDefaults
{
    public static void Apply(Window window)
    {
        var first = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        foreach (var control in window.GetVisualDescendants())
        {
            switch (control)
            {
                case Calendar calendar when !calendar.IsSet(Calendar.FirstDayOfWeekProperty) && calendar.FirstDayOfWeek != first:
                    calendar.SetCurrentValue(Calendar.FirstDayOfWeekProperty, first);
                    break;
                case CalendarDatePicker picker when !picker.IsSet(CalendarDatePicker.FirstDayOfWeekProperty) && picker.FirstDayOfWeek != first:
                    picker.SetCurrentValue(CalendarDatePicker.FirstDayOfWeekProperty, first);
                    break;
            }
        }
    }
}
