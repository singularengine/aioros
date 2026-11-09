using System.Collections.Immutable;
using System.Globalization;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Writers;
using Avalonia.Controls;
using Calendar = Avalonia.Controls.Calendar;

namespace Aioros.Avalonia.Html.Families;

internal static class RangesFamily
{
    public static ImmutableArray<IControlWriter> Writers { get; } =
    [
        new ControlWriter<Slider>(RangeWriters.WriteSlider) { Rooted = true },
        new ControlWriter<ProgressBar>(RangeWriters.WriteProgress) { Rooted = true },
        new ControlWriter<Calendar>(CalendarWriters.WriteCalendar),
        new ControlWriter<CalendarDatePicker>(DateWriters.WriteCalendarDatePicker) { Rooted = true },
        new ControlWriter<DatePicker>(DateWriters.WriteDatePicker) { Rooted = true },
        new ControlWriter<TimePicker>(DateWriters.WriteTimePicker) { Rooted = true },
    ];

    public static ImmutableArray<IFieldBinder> Binders { get; } =
    [
        new FieldBinder<Slider>(BindSlider),
        new FieldBinder<Calendar>(CalendarPosts.Bind),
        new FieldBinder<CalendarDatePicker>(BindCalendarDatePicker),
        new FieldBinder<DatePicker>(BindDatePicker),
        new FieldBinder<TimePicker>(BindTimePicker),
    ];

    private static void BindSlider(Slider slider, string? value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
        {
            slider.Value = number;
        }
    }

    private static void BindCalendarDatePicker(CalendarDatePicker picker, string? value)
    {
        if (DateWriters.TryDate(value, out var date))
        {
            picker.SelectedDate = date;
        }
    }

    private static void BindDatePicker(DatePicker picker, string? value)
    {
        if (DateWriters.TryDate(value, out var date))
        {
            picker.SelectedDate = date is { } day ? new DateTimeOffset(day, picker.SelectedDate?.Offset ?? TimeSpan.Zero) : null;
        }
    }

    private static void BindTimePicker(TimePicker picker, string? value)
    {
        if (DateWriters.TryTime(value, out var time))
        {
            picker.SelectedTime = time;
        }
    }
}
