using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Calendar = Avalonia.Controls.Calendar;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Agenda : ObservableObject
{
    [ObservableProperty]
    public partial DateTime? Day { get; set; } = new DateTime(2025, 6, 12, 0, 0, 0, DateTimeKind.Unspecified);

    [ObservableProperty]
    public partial DateTime? From { get; set; }

    [ObservableProperty]
    public partial DateTime? To { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<DateTime>? Days { get; set; }

    public int Touches { get; private set; }

    [RelayCommand]
    private void Touch() => Touches++;
}

/// <summary>What a planned calendar binds: the day it shows and its selection, as a day, a range's ends or several days.</summary>
public sealed partial class Planner(DateTime shown, DateTime? day = null) : ObservableObject
{
    [ObservableProperty]
    public partial DateTime Shown { get; set; } = shown;

    [ObservableProperty]
    public partial DateTime? Day { get; set; } = day;

    [ObservableProperty]
    public partial DateTime? From { get; set; }

    [ObservableProperty]
    public partial DateTime? To { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<DateTime>? Days { get; set; }
}

/// <summary>
/// A library's week numbers shaped as Aioros.Cosmo's <c>week-numbers</c> class: a text per week before its days and an
/// empty one over the weekdays, in a column the library inserts before the days, which move a column on.
/// </summary>
public static class CalendarWeeks
{
    public static readonly AttachedProperty<bool?> ShowsProperty = AvaloniaProperty.RegisterAttached<Calendar, bool?>("Shows", typeof(CalendarWeeks));

    static CalendarWeeks() => ShowsProperty.Changed.AddClassHandler<Calendar>((calendar, _) =>
    {
        calendar.TemplateApplied += (_, _) => Later(calendar);
        calendar.AttachedToVisualTree += (_, _) => Later(calendar);
        calendar.DisplayDateChanged += (_, _) => Later(calendar);
    });

    public static bool? GetShows(Calendar calendar) => calendar.GetValue(ShowsProperty);

    public static void SetShows(Calendar calendar, bool? value) => calendar.SetValue(ShowsProperty, value);

    private static void Later(Calendar calendar) => Dispatcher.UIThread.Post(() => Follow(calendar), DispatcherPriority.Loaded);

    private static void Follow(Calendar calendar)
    {
        if (calendar.GetVisualDescendants().OfType<Grid>().FirstOrDefault(grid => grid.Name == "PART_MonthView") is not { } view)
        {
            return;
        }

        var numbers = view.Children.OfType<TextBlock>().Where(text => text.Tag is "week").OrderBy(Grid.GetRow).ToList();
        if (numbers.Count == 0)
        {
            view.ColumnDefinitions.Insert(0, new ColumnDefinition(GridLength.Auto));
            foreach (var child in view.Children)
            {
                Grid.SetColumn(child, Grid.GetColumn(child) + 1);
            }

            numbers = [.. Enumerable.Range(0, 7).Select(row => new TextBlock { Tag = "week", Width = 32, FontSize = 12, TextAlignment = TextAlignment.Center, [Grid.RowProperty] = row })];
            view.Children.AddRange(numbers);
        }

        var weeks = view.Children.OfType<CalendarDayButton>().Where(day => day.DataContext is DateTime).GroupBy(Grid.GetRow).ToList();
        var days = weeks.ToDictionary(week => week.Key, week => (DateTime)week.OrderBy(Grid.GetColumn).First().DataContext!);
        var shown = weeks.Where(week => week.Any(day => !((IPseudoClasses)day.Classes).Contains(":inactive"))).Select(week => week.Key).Append(0).ToHashSet();
        foreach (var number in numbers)
        {
            number.IsVisible = GetShows(calendar) == true && shown.Contains(Grid.GetRow(number));
            var row = Grid.GetRow(number);
            number.Text = row > 0 && days.TryGetValue(row, out var first) ? CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(first, CalendarWeekRule.FirstDay, DayOfWeek.Sunday).ToString(CultureInfo.InvariantCulture) : null;
            number.Height = row > 0 ? 32 : double.NaN;
            number.Margin = row > 0 ? new Thickness(0, 8, 0, 0) : default;
        }
    }
}

/// <summary>A library's day grid shaped as Aioros.Cosmo's <c>CalendarDays</c>: weeks without a day of the month hidden, a range's days marked, its band rounded at the ends.</summary>
public static class CalendarMarks
{
    public static readonly AttachedProperty<bool> FollowsProperty = AvaloniaProperty.RegisterAttached<Calendar, bool>("Follows", typeof(CalendarMarks));

    private static readonly string[] Marks = ["range-start", "range-middle", "range-end", "outside-hidden"];

    private static readonly CornerRadius Radius = new(8);

    private static readonly CornerRadius Left = new(8, 0, 0, 8);

    private static readonly CornerRadius Right = new(0, 8, 8, 0);

    static CalendarMarks() => FollowsProperty.Changed.AddClassHandler<Calendar>((calendar, change) =>
    {
        if (change.GetNewValue<bool>())
        {
            calendar.SelectedDatesChanged += (_, _) => Later(calendar);
            calendar.DisplayDateChanged += (_, _) => Later(calendar);
            calendar.TemplateApplied += (_, _) => Later(calendar);
            calendar.AttachedToVisualTree += (_, _) => Later(calendar);
        }
    });

    public static bool GetFollows(Calendar calendar) => calendar.GetValue(FollowsProperty);

    public static void SetFollows(Calendar calendar, bool value) => calendar.SetValue(FollowsProperty, value);

    private static void Later(Calendar calendar) => Dispatcher.UIThread.Post(() => Follow(calendar), DispatcherPriority.Loaded);

    private static void Follow(Calendar calendar)
    {
        var days = calendar.GetVisualDescendants().OfType<CalendarDayButton>().Where(day => day.DataContext is DateTime).ToList();
        var range = calendar.SelectionMode == CalendarSelectionMode.SingleRange && calendar.SelectedDates.Count > 1
            ? (Start: calendar.SelectedDates.Min(), End: calendar.SelectedDates.Max())
            : ((DateTime Start, DateTime End)?)null;
        var hidden = calendar.Classes.Contains("outside-hidden");
        foreach (var week in days.GroupBy(Grid.GetRow))
        {
            var row = week.OrderBy(Grid.GetColumn).ToList();
            var shown = row.Exists(day => !((IPseudoClasses)day.Classes).Contains(":inactive"));
            for (var index = 0; index < row.Count; index++)
            {
                Mark(row[index], shown, range, hidden, (index == 0, index == row.Count - 1));
            }
        }
    }

    private static void Mark(CalendarDayButton day, bool shown, (DateTime Start, DateTime End)? range, bool hidden, (bool First, bool Last) edge)
    {
        day.IsVisible = shown;
        day.Classes.RemoveAll(Marks);
        var place = Place(((DateTime)day.DataContext!).Date, range);
        day.Classes.AddRange(hidden ? [.. place, "outside-hidden"] : place);
        if (place.Contains("range-middle"))
        {
            day.CornerRadius = edge.First ? Left : edge.Last ? Right : default;
        }
        else
        {
            day.ClearValue(TemplatedControl.CornerRadiusProperty);
        }

        if (day.GetVisualDescendants().OfType<Border>().FirstOrDefault(part => part.Name == "PART_Band") is { } band)
        {
            Band(band, place.FirstOrDefault(), edge);
        }
    }

    private static void Band(Border band, string? place, (bool First, bool Last) edge)
    {
        band.Background = place is "range-start" or "range-end" ? new SolidColorBrush(Color.Parse("#f5f5f5")) : null;
        band.CornerRadius = place switch
        {
            "range-start" => edge.Last ? Radius : Left,
            "range-end" => edge.First ? Radius : Right,
            _ => default,
        };
    }

    private static string[] Place(DateTime date, (DateTime Start, DateTime End)? range) => range switch
    {
        { } r when date == r.Start.Date => ["range-start"],
        { } r when date == r.End.Date => ["range-end"],
        { } r when date > r.Start.Date && date < r.End.Date => ["range-middle"],
        _ => [],
    };
}

/// <summary>A library's selection bound two way, shaped as Aioros.Cosmo's <c>CalendarSelection</c>: a range's ends or the selected days, following <see cref="Calendar.SelectedDatesChanged"/>.</summary>
public static class CalendarPicks
{
    public static readonly AttachedProperty<DateTime?> RangeStartProperty =
        AvaloniaProperty.RegisterAttached<Calendar, DateTime?>("RangeStart", typeof(CalendarPicks), defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<DateTime?> RangeEndProperty =
        AvaloniaProperty.RegisterAttached<Calendar, DateTime?>("RangeEnd", typeof(CalendarPicks), defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<IReadOnlyList<DateTime>?> DatesProperty =
        AvaloniaProperty.RegisterAttached<Calendar, IReadOnlyList<DateTime>?>("Dates", typeof(CalendarPicks), defaultBindingMode: BindingMode.TwoWay);

    private static readonly AttachedProperty<bool> SyncingProperty = AvaloniaProperty.RegisterAttached<Calendar, bool>("Syncing", typeof(CalendarPicks));

    private static readonly AttachedProperty<bool> FollowedProperty = AvaloniaProperty.RegisterAttached<Calendar, bool>("Followed", typeof(CalendarPicks));

    static CalendarPicks()
    {
        RangeStartProperty.Changed.AddClassHandler<Calendar>((calendar, _) => ToCalendar(calendar));
        RangeEndProperty.Changed.AddClassHandler<Calendar>((calendar, _) => ToCalendar(calendar));
        DatesProperty.Changed.AddClassHandler<Calendar>((calendar, _) => ToCalendar(calendar));
    }

    public static DateTime? GetRangeStart(Calendar calendar) => calendar.GetValue(RangeStartProperty);

    public static void SetRangeStart(Calendar calendar, DateTime? value) => calendar.SetValue(RangeStartProperty, value);

    public static DateTime? GetRangeEnd(Calendar calendar) => calendar.GetValue(RangeEndProperty);

    public static void SetRangeEnd(Calendar calendar, DateTime? value) => calendar.SetValue(RangeEndProperty, value);

    public static IReadOnlyList<DateTime>? GetDates(Calendar calendar) => calendar.GetValue(DatesProperty);

    public static void SetDates(Calendar calendar, IReadOnlyList<DateTime>? value) => calendar.SetValue(DatesProperty, value);

    private static void ToCalendar(Calendar calendar)
    {
        if (!calendar.GetValue(FollowedProperty))
        {
            calendar.SetValue(FollowedProperty, true);
            calendar.SelectedDatesChanged += (_, _) => FromCalendar(calendar);
        }

        if (calendar.GetValue(SyncingProperty))
        {
            return;
        }

        calendar.SetValue(SyncingProperty, true);
        try
        {
            calendar.SelectedDates.Clear();
            if (calendar.SelectionMode == CalendarSelectionMode.SingleRange && GetRangeStart(calendar) is { } start)
            {
                var end = GetRangeEnd(calendar) ?? start;
                calendar.SelectedDates.AddRange(start <= end ? start : end, start <= end ? end : start);
            }
            else if (GetDates(calendar) is { } dates)
            {
                foreach (var date in dates)
                {
                    calendar.SelectedDates.Add(date.Date);
                }
            }
        }
        finally
        {
            calendar.SetValue(SyncingProperty, false);
        }
    }

    private static void FromCalendar(Calendar calendar)
    {
        if (calendar.GetValue(SyncingProperty))
        {
            return;
        }

        calendar.SetValue(SyncingProperty, true);
        try
        {
            var selected = calendar.SelectedDates.Select(date => date.Date).Distinct().Order().ToList();
            if (calendar.SelectionMode == CalendarSelectionMode.SingleRange)
            {
                calendar.SetCurrentValue(RangeStartProperty, selected.Count > 0 ? selected[0] : null);
                calendar.SetCurrentValue(RangeEndProperty, selected.Count > 0 ? selected[^1] : null);
            }
            else
            {
                calendar.SetCurrentValue(DatesProperty, selected);
            }
        }
        finally
        {
            calendar.SetValue(SyncingProperty, false);
        }
    }
}

/// <summary>Days written in XAML for <see cref="CalendarPicks.DatesProperty"/>, as Aioros.Cosmo's <c>CalendarDates</c>.</summary>
public sealed class PickedDays : List<DateTime>;

/// <summary>
/// A library's month and year selects shaped as Aioros.Cosmo's <c>CalendarCaption</c>: the culture's abbreviated month
/// names and the years from a hundred before today to today, widened to the shown year, with the shown month and year as
/// indexes its template's combo boxes bind two way; choosing one shows that month.
/// </summary>
public static class CalendarCaptions
{
    public static readonly AttachedProperty<bool> FollowsProperty = AvaloniaProperty.RegisterAttached<Calendar, bool>("Follows", typeof(CalendarCaptions));

    public static readonly AttachedProperty<IReadOnlyList<string>?> MonthsProperty = AvaloniaProperty.RegisterAttached<Calendar, IReadOnlyList<string>?>("Months", typeof(CalendarCaptions));

    public static readonly AttachedProperty<int> MonthProperty = AvaloniaProperty.RegisterAttached<Calendar, int>("Month", typeof(CalendarCaptions), -1, defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<IReadOnlyList<string>?> YearsProperty = AvaloniaProperty.RegisterAttached<Calendar, IReadOnlyList<string>?>("Years", typeof(CalendarCaptions));

    public static readonly AttachedProperty<int> YearProperty = AvaloniaProperty.RegisterAttached<Calendar, int>("Year", typeof(CalendarCaptions), -1, defaultBindingMode: BindingMode.TwoWay);

    private static readonly AttachedProperty<int> FirstProperty = AvaloniaProperty.RegisterAttached<Calendar, int>("First", typeof(CalendarCaptions));

    private static readonly AttachedProperty<bool> SyncingProperty = AvaloniaProperty.RegisterAttached<Calendar, bool>("Syncing", typeof(CalendarCaptions));

    static CalendarCaptions()
    {
        FollowsProperty.Changed.AddClassHandler<Calendar>((calendar, change) =>
        {
            if (change.GetNewValue<bool>())
            {
                calendar.DisplayDateChanged += (_, _) => Shown(calendar);
                calendar.TemplateApplied += (_, _) => Dispatcher.UIThread.Post(() => Plain(calendar), DispatcherPriority.Loaded);
                Shown(calendar);
            }
        });
        MonthProperty.Changed.AddClassHandler<Calendar>((calendar, _) => Show(calendar));
        YearProperty.Changed.AddClassHandler<Calendar>((calendar, _) => Show(calendar));
    }

    public static bool GetFollows(Calendar calendar) => calendar.GetValue(FollowsProperty);

    public static void SetFollows(Calendar calendar, bool value) => calendar.SetValue(FollowsProperty, value);

    public static IReadOnlyList<string>? GetMonths(Calendar calendar) => calendar.GetValue(MonthsProperty);

    public static int GetMonth(Calendar calendar) => calendar.GetValue(MonthProperty);

    public static void SetMonth(Calendar calendar, int value) => calendar.SetValue(MonthProperty, value);

    public static IReadOnlyList<string>? GetYears(Calendar calendar) => calendar.GetValue(YearsProperty);

    public static int GetYear(Calendar calendar) => calendar.GetValue(YearProperty);

    public static void SetYear(Calendar calendar, int value) => calendar.SetValue(YearProperty, value);

    private static void Shown(Calendar calendar)
    {
        var shown = calendar.DisplayDate;
        var today = DateTime.Today;
        var first = Math.Min(today.Year - 100, shown.Year);
        var last = Math.Max(today.Year, shown.Year);
        var format = CultureInfo.CurrentCulture.DateTimeFormat;
        calendar.SetValue(SyncingProperty, true);
        try
        {
            calendar.SetValue(MonthsProperty, format.AbbreviatedMonthNames.Take(12).ToArray());
            calendar.SetValue(YearsProperty, Enumerable.Range(first, last - first + 1).Select(year => year.ToString(format)).ToArray());
            calendar.SetValue(FirstProperty, first);
            calendar.SetCurrentValue(MonthProperty, shown.Month - 1);
            calendar.SetCurrentValue(YearProperty, shown.Year - first);
        }
        finally
        {
            calendar.SetValue(SyncingProperty, false);
        }
    }

    /// <summary>The caption's label in place of its selects with the <c>plain</c> class, as Aioros.Cosmo's calendars without the <c>dropdown</c> class.</summary>
    private static void Plain(Calendar calendar)
    {
        foreach (var part in calendar.GetVisualDescendants().OfType<Control>().Where(part => part.Name is "PART_HeaderButton" or "PART_Dropdowns"))
        {
            part.IsVisible = (part.Name == "PART_HeaderButton") == calendar.Classes.Contains("plain");
        }
    }

    private static void Show(Calendar calendar)
    {
        var (month, year) = (GetMonth(calendar), GetYear(calendar));
        if (calendar.GetValue(SyncingProperty) || month is < 0 or > 11 || year < 0 || year >= (GetYears(calendar)?.Count ?? 0))
        {
            return;
        }

        var shown = new DateTime(calendar.GetValue(FirstProperty) + year, month + 1, 1, 0, 0, 0, calendar.DisplayDate.Kind);
        if (shown.Year != calendar.DisplayDate.Year || shown.Month != calendar.DisplayDate.Month)
        {
            calendar.SetCurrentValue(Calendar.DisplayDateProperty, shown);
        }
    }
}

/// <summary>A library that gives a calendar's days a look their state does not name: every even day of the month bold.</summary>
public static class CalendarParity
{
    public static readonly AttachedProperty<bool> BoldsProperty = AvaloniaProperty.RegisterAttached<Calendar, bool>("Bolds", typeof(CalendarParity));

    static CalendarParity() => BoldsProperty.Changed.AddClassHandler<Calendar>((calendar, _) =>
    {
        calendar.DisplayDateChanged += (_, _) => Later(calendar);
        calendar.TemplateApplied += (_, _) => Later(calendar);
        calendar.AttachedToVisualTree += (_, _) => Later(calendar);
    });

    public static bool GetBolds(Calendar calendar) => calendar.GetValue(BoldsProperty);

    public static void SetBolds(Calendar calendar, bool value) => calendar.SetValue(BoldsProperty, value);

    private static void Later(Calendar calendar) => Dispatcher.UIThread.Post(() =>
    {
        foreach (var day in calendar.GetVisualDescendants().OfType<CalendarDayButton>().Where(day => day.DataContext is DateTime))
        {
            day.FontWeight = ((DateTime)day.DataContext!).Day % 2 == 0 ? FontWeight.Bold : FontWeight.Normal;
        }
    }, DispatcherPriority.Loaded);
}
