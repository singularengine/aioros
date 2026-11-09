using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Calendar = Avalonia.Controls.Calendar;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A themed calendar whose shown month or selection is bound, planned as react-day-picker computes its month for each
/// render: the plan computes the six weeks with date arithmetic (<see cref="CalendarMonths"/>) and writes each day in the
/// look Avalonia gives a day in its state, learned when the page is compiled by showing the calendar months and selections
/// that put its days in every state its mode can give (another month's, selected, its place in a range, its column), today
/// forced on a day as Avalonia marks it, and the tab stop. Which weeks are shown and how they are numbered is the rule the
/// library's code follows, learned from those months; the selection the model binds is read through the members the
/// calendar's own properties and a library's attached ones (found by setting each) bind. A day whose look its state does not
/// name, or a calendar the plan cannot follow (blackout days, a display range, another view), fails the plan with its reason.
/// </summary>
internal static class PlannedCalendars
{
    public const string Close = "</button></div></td>";

    /// <summary>The grid's days a probe selects, by index from its first shown day: each week's ends and middle, in and out of the month.</summary>
    private static readonly int[] Picks = [0, 3, 6, 14, 17, 20, 35, 38, 41];

    /// <summary>The ends of a probe's range, from before the grid to after it.</summary>
    private static readonly int[] Ends = [-3, 0, 3, 6, 14, 17, 20, 21, 35, 38, 41, 44];

    public static PlannedCalendar? Learn(HtmlContext context, Calendar calendar, Grid view, List<CalendarDayButton> days)
    {
        var plan = context.Plan;
        if (Unplanned(context, calendar) is { } reason)
        {
            plan.Fail(reason);
            return null;
        }

        var learning = new Learning(context, calendar, view, days);
        var sources = new List<(string Role, string Path)>();
        foreach (var property in Bound(calendar))
        {
            if (Role(calendar, property) is not { } role || plan.BoundPath(calendar, property) is not { } path || path.StartsWith('^'))
            {
                plan.Fail($"a Calendar with a bound {property.Name} the plan cannot follow");
                return null;
            }

            sources.Add((role, path));
        }

        var probed = Probe(learning);
        if (probed.Failure is { } failure)
        {
            plan.Fail(failure);
            return null;
        }

        var values = new List<string>
        {
            $"mode={calendar.SelectionMode}",
            $"first={(int)calendar.FirstDayOfWeek}",
            $"today={(calendar.IsTodayHighlighted ? 1 : 0)}",
            $"rows={probed.Rows}",
            $"view={calendar.DisplayMode}",
            $"roles={string.Join(',', sources.Select(source => source.Role))}",
        };
        if (probed.Weeks is { } weeks)
        {
            values.Add("weeks=" + weeks);
        }

        values.AddRange(probed.Caption);

        if (!sources.Exists(source => source.Role == "display") && calendar.DisplayDate.Date != DateTime.Today)
        {
            values.Add("display=" + calendar.DisplayDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (!sources.Exists(source => source.Role is "date" or "start" or "end" or "dates"))
        {
            values.Add("selected=" + CalendarMonths.Runs(calendar.SelectedDates));
        }

        var planned = new PlannedCalendar(string.Join('|', sources.Select(source => source.Path)), [.. values], learning.Looks, FieldSpecs.Of(plan, days[0]));
        return Same(planned, calendar, plan.Scope) ? planned : Failed(plan, "a bound Calendar whose month or selection the plan computes unlike Avalonia");
    }

    private static PlannedCalendar? Failed(PlanRecorder plan, string reason)
    {
        plan.Fail(reason);
        return null;
    }

    /// <summary>Whether the plan computes from the compiled model the month and the selection the calendar shows.</summary>
    private static bool Same(PlannedCalendar planned, Calendar calendar, object? scope) =>
        Slots.Getter(planned.Spec("state"))(scope) as string == CalendarPosts.State(calendar);

    private static string? Unplanned(HtmlContext context, Calendar calendar) =>
        calendar.DisplayMode != CalendarMode.Month ? "a bound Calendar shown in its year or decade view"
        : calendar.BlackoutDates.Count > 0 || calendar.DisplayDateStart is not null || calendar.DisplayDateEnd is not null ? "a bound Calendar with blackout days or a display range"
        : CultureInfo.CurrentCulture.Calendar is not GregorianCalendar ? "a bound Calendar in a culture whose calendar is not Gregorian"
        : context.Flags.Framed ? "a bound Calendar inside a container query or a bound class"
        : null;

    /// <summary>The calendar's properties bound to the model that say what it shows: its own and a library's attached ones.</summary>
    public static List<AvaloniaProperty> Bound(Calendar calendar) =>
        [.. AvaloniaPropertyRegistry.Instance.GetRegistered(calendar).Where(static property => property.OwnerType == typeof(Calendar))
            .Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(calendar.GetType()).Where(static property => property.OwnerType.Assembly != typeof(Calendar).Assembly))
            .Where(property => LocalBindings.IsBound(calendar, property))];

    private static string? Role(Calendar calendar, AvaloniaProperty property) =>
        property == Calendar.DisplayDateProperty ? "display"
        : property == Calendar.SelectedDateProperty ? "date"
        : property.OwnerType == typeof(Calendar) ? null
        : Selection(calendar, property);

    /// <summary>
    /// What a library's attached property bound to the model selects, found by setting it while the calendar is detached from
    /// the model: the days of a list it is set to (<c>dates</c>), the day it is set to alone (a range's <c>start</c>), or
    /// nothing alone and the range's end after a start (<c>end</c>).
    /// </summary>
    private static string? Selection(Calendar calendar, AvaloniaProperty property)
    {
        var first = CalendarMonths.FirstShown(calendar.DisplayDate, calendar.FirstDayOfWeek).AddDays(16);
        var (start, end) = (first, first.AddDays(3));
        using var detached = Detached.From(calendar);
        if (property.PropertyType.IsAssignableFrom(typeof(List<DateTime>)))
        {
            return Selects(calendar, property, new List<DateTime> { start, end }, [start, end]) ? "dates" : null;
        }

        if (property.PropertyType != typeof(DateTime?) && property.PropertyType != typeof(DateTime))
        {
            return null;
        }

        var others = Bound(calendar).Where(other => other != property && other.PropertyType == typeof(DateTime?) && other.OwnerType != typeof(Calendar)).ToList();
        others.ForEach(other => calendar.SetCurrentValue(other, null));
        if (Selects(calendar, property, start, [start]))
        {
            return "start";
        }

        return calendar.SelectedDates.Count == 0
            && others.Exists(other => Selects(calendar, other, start, [start]) && Selects(calendar, property, end, [.. Enumerable.Range(0, 4).Select(day => start.AddDays(day))]))
            ? "end"
            : null;
    }

    private static bool Selects(Calendar calendar, AvaloniaProperty property, object value, List<DateTime> expected)
    {
        calendar.SetCurrentValue(property, value);
        Settled.Rebuilt(calendar);
        return calendar.SelectedDates.Select(static date => date.Date).Order().SequenceEqual(expected);
    }

    /// <summary>
    /// Learns the looks of the calendar's days: from what it shows, then, detached from the model, from the months and
    /// selections that give its days the states its mode can give, and checks that it shows what it showed once given back.
    /// </summary>
    private static Probed Probe(Learning learning)
    {
        var calendar = learning.Calendar;
        learning.Rows = calendar.GetVisualDescendants().OfType<CalendarDayButton>().Any(static day => !day.IsVisible) ? "month" : "all";
        var shown = learning.Harvest();
        if (learning.Failure is null)
        {
            var display = new DateTime(calendar.DisplayDate.Year, calendar.DisplayDate.Month, 1, 0, 0, 0, calendar.DisplayDate.Kind);
            using var detached = Detached.From(calendar);
            var months = Enumerable.Range(0, 14).Select(display.AddMonths).ToList();
            var full = months.First(month => month.DayOfWeek == calendar.FirstDayOfWeek);
            learning.Show(full, []);
            learning.Rows = learning.Calendar.GetVisualDescendants().OfType<CalendarDayButton>().Any(static day => !day.IsVisible) ? "month" : "all";
            learning.Harvest();
            foreach (var (month, selection) in Probes(calendar, months).Where(learning.Adds))
            {
                learning.Show(month, selection);
                learning.Harvest();
            }

            learning.WeekRule();
            learning.CaptionRule();
        }

        if (learning.Failure is null && learning.Harvest(check: true) != shown)
        {
            learning.Failure = "a bound Calendar that does not show the same days once its probes are undone";
        }

        return new Probed(learning.Failure, learning.Rows, learning.Weeks, learning.Caption);
    }

    /// <summary>The months and selections a probe may show, in order: none, then the calendar's mode's picks of each month.</summary>
    private static IEnumerable<(DateTime Month, List<DateTime> Selection)> Probes(Calendar calendar, List<DateTime> months)
    {
        foreach (var month in months)
        {
            var first = CalendarMonths.FirstShown(month, calendar.FirstDayOfWeek);
            yield return (month, []);
            var picks = calendar.SelectionMode switch
            {
                CalendarSelectionMode.None => [],
                CalendarSelectionMode.SingleDate => Picks.Select(pick => new List<DateTime> { first.AddDays(pick) }),
                CalendarSelectionMode.MultipleRange => Picks.Select(pick => new List<DateTime> { first.AddDays(pick) })
                    .Append([.. Enumerable.Range(0, 42).Select(day => first.AddDays(day))])
                    .Append([.. Enumerable.Range(0, 21).Select(day => first.AddDays(day * 2))]),
                _ => Ends.SelectMany((from, index) => Ends.Skip(index).Select(to => Enumerable.Range(from, to - from + 1).Select(day => first.AddDays(day)).ToList())),
            };
            foreach (var pick in picks)
            {
                yield return (month, pick);
            }
        }
    }

    private sealed record Probed(string? Failure, string Rows, string? Weeks, List<string> Caption);

    /// <summary>The calendar detached from the model while probes show it other months and days, its values and selection put back afterwards.</summary>
    private sealed class Detached : IDisposable
    {
        private readonly Calendar _calendar;
        private readonly object? _context;
        private readonly bool _local;
        private readonly DateTime _display;
        private readonly DateTime? _selected;
        private readonly List<DateTime> _dates;

        private Detached(Calendar calendar)
        {
            _calendar = calendar;
            _local = calendar.IsSet(StyledElement.DataContextProperty);
            _context = calendar.DataContext;
            _display = calendar.DisplayDate;
            _selected = calendar.SelectedDate;
            _dates = [.. calendar.SelectedDates];
            calendar.DataContext = null;
            Settled.Rebuilt(calendar);
        }

        public static Detached From(Calendar calendar) => new(calendar);

        public void Dispose()
        {
            if (_local)
            {
                _calendar.DataContext = _context;
            }
            else
            {
                _calendar.ClearValue(StyledElement.DataContextProperty);
            }

            _calendar.SetCurrentValue(Calendar.DisplayDateProperty, _display);
            if (!_calendar.SelectedDates.OrderBy(static date => date).SequenceEqual(_dates.OrderBy(static date => date)))
            {
                Select(_calendar, _dates, _selected);
            }

            Settled.Rebuilt(_calendar);
        }
    }

    public static void Select(Calendar calendar, List<DateTime> dates, DateTime? single = null)
    {
        switch (calendar.SelectionMode)
        {
            case CalendarSelectionMode.SingleDate:
                calendar.SetCurrentValue(Calendar.SelectedDateProperty, single ?? (dates.Count > 0 ? dates[0] : null));
                break;
            case CalendarSelectionMode.SingleRange:
                calendar.SelectedDates.Clear();
                if (dates.Count > 0)
                {
                    calendar.SelectedDates.AddRange(dates.Min(), dates.Max());
                }

                break;
            case CalendarSelectionMode.MultipleRange:
                calendar.SelectedDates.Clear();
                dates.ForEach(calendar.SelectedDates.Add);
                break;
        }
    }

    /// <summary>What probing learns: each day key's look, the weeks' rule and numbers, and why it failed.</summary>
    private sealed class Learning(HtmlContext context, Calendar calendar, Grid view, List<CalendarDayButton> days)
    {
        private readonly List<(DateTime First, string Text)> _numbers = [];

        public Calendar Calendar => calendar;

        public Dictionary<string, (string Open, string Middle)> Looks { get; } = new(StringComparer.Ordinal);

        public string? Failure { get; set; }

        public string Rows { get; set; } = "all";

        public string? Weeks { get; private set; }

        public List<string> Caption { get; } = [];

        public void Show(DateTime month, List<DateTime> selection)
        {
            calendar.SetCurrentValue(Calendar.DisplayDateProperty, month);
            Select(calendar, selection);
            Settled.Rebuilt(calendar);
        }

        /// <summary>Whether a probe would put a day of a shown week in a state no look was learned for.</summary>
        public bool Adds((DateTime Month, List<DateTime> Selection) probe) =>
            Failure is null && Month(probe.Month, probe.Selection).Where(week => week.Shown).SelectMany(static week => week.Days).Any(day => !Looks.ContainsKey(day.Key));

        private List<CalendarWeek> Month(DateTime month, List<DateTime> selection)
        {
            var spec = new SlotSpec("calendar", null, "weeks")
            {
                Values = [$"mode={calendar.SelectionMode}", $"first={(int)calendar.FirstDayOfWeek}", $"today={(calendar.IsTodayHighlighted ? 1 : 0)}", $"rows={Rows}",
                    "display=" + month.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "selected=" + CalendarMonths.Runs(selection)],
            };
            return (List<CalendarWeek>)Slots.Getter(spec)(null)!;
        }

        /// <summary>
        /// Learns the looks of the days the calendar shows now and checks the ones learned before; with <paramref name="check"/>
        /// only checks them. Answers the bodies of what it shows, to compare what it shows before and after the probes.
        /// </summary>
        public string Harvest(bool check = false)
        {
            var shown = new StringBuilder();
            var weeks = Month(calendar.DisplayDate, [.. calendar.SelectedDates]);
            var stop = ThemedCalendarWriters.Stop(days);
            foreach (var (week, row) in weeks.Zip(days.Chunk(7)))
            {
                if (week.Shown != row.Any(static day => day.IsVisible))
                {
                    Failure ??= "a bound Calendar whose shown weeks follow no rule the plan knows";
                    return string.Empty;
                }

                if (!week.Shown)
                {
                    continue;
                }

                Number(row[0]);
                foreach (var (day, button) in week.Days.Zip(row))
                {
                    if (day.Key[5] == 'k' != (button == stop))
                    {
                        Failure ??= "a bound Calendar whose tab stop is not the day the plan computes";
                        return string.Empty;
                    }

                    var look = Look(day.Key, button, check);
                    shown.Append(look.Open).Append(look.Middle);
                    if (!check)
                    {
                        Variants(day.Key, button);
                    }
                }
            }

            return shown.ToString();
        }

        /// <summary>The look of a day as a tab stop or not, and as today, as Avalonia marks it, when it is not today.</summary>
        private void Variants(string key, CalendarDayButton button)
        {
            if (key[0] == 'a')
            {
                Look(With(key, 5, key[5] == 'k' ? '-' : 'k'), button, stop: key[5] != 'k');
            }

            if (key[4] == 't' || !calendar.IsTodayHighlighted)
            {
                return;
            }

            var today = With(key, 4, 't');
            if (Looks.ContainsKey(today) && (key[0] == 'o' || Looks.ContainsKey(With(today, 5, key[5] == 'k' ? '-' : 'k'))))
            {
                return;
            }

            Settled.Change(button, () => ((IPseudoClasses)button.Classes).Set(":today", true));
            Look(today, button);
            if (key[0] == 'a')
            {
                Look(With(today, 5, key[5] == 'k' ? '-' : 'k'), button, stop: key[5] != 'k');
            }

            Settled.Change(button, () => ((IPseudoClasses)button.Classes).Set(":today", false));
        }

        private static string With(string key, int index, char mark) => string.Concat(key.AsSpan(0, index), mark.ToString(), key.AsSpan(index + 1));

        private (string Open, string Middle) Look(string key, CalendarDayButton button, bool check = false, bool? stop = null)
        {
            var look = ThemedCalendarWriters.DayParts(context, button, stop ?? key[5] == 'k');
            if (Looks.TryGetValue(key, out var learned))
            {
                if (learned != look)
                {
                    Failure ??= "a bound Calendar whose days' looks depend on more than their state (another month's, today, selected, a range's place, their column)";
                }

                return learned;
            }

            if (!check)
            {
                Looks[key] = look;
            }

            return look;
        }

        /// <summary>Notes the number a library wrote before a shown week's days, with the week's first day.</summary>
        private void Number(CalendarDayButton first)
        {
            var row = Grid.GetRow(first);
            var texts = view.Children.Skip(ThemedCalendarWriters.Own).Where(child => child.IsVisible && Grid.GetRow(child) == row).ToList();
            if (texts.Count == 1 && texts[0] is TextBlock { Text: { } text } && first.DataContext is DateTime date)
            {
                _numbers.Add((date.Date, text));
            }
            else if (texts.Count > 0)
            {
                Failure ??= "a bound Calendar whose weeks hold more than a number before or after their days";
            }
        }

        /// <summary>
        /// What the selects of a caption offer (react-day-picker's <c>captionLayout="dropdown"</c>): the culture's month
        /// names, abbreviated or not, and the years some before today to some after it, widened to the shown year, learned
        /// from the years they offer for this year, for a year long before and for one after.
        /// </summary>
        public void CaptionRule()
        {
            var selects = calendar.GetVisualDescendants().OfType<ComboBox>().Where(select => select.IsVisible && select.GetVisualAncestors().TakeWhile(ancestor => ancestor != calendar).All(static ancestor => ancestor.IsVisible)).ToList();
            var format = CultureInfo.CurrentCulture.DateTimeFormat;
            if (Failure is not null || selects.Count == 0)
            {
                return;
            }

            if (selects.Exists(select => Texts(select).SequenceEqual(format.AbbreviatedMonthNames.Take(12))))
            {
                Caption.Add("names=abbr");
            }
            else if (selects.Exists(select => Texts(select).SequenceEqual(format.MonthNames.Take(12))))
            {
                Caption.Add("names=full");
            }

            var today = DateTime.Today.Year;
            var spans = new[] { today, today - 150, today + 50 }.Select(year =>
            {
                Show(new DateTime(year, 6, 1, 0, 0, 0, DateTimeKind.Unspecified), []);
                return (Year: year, Span: selects.Select(Years).FirstOrDefault(span => span is not null));
            }).ToList();
            if (spans[0].Span is not { } span)
            {
                return;
            }

            var (before, after) = (today - span.First, span.Last - today);
            if (spans.Exists(probe => probe.Span != (Math.Min(today - before, probe.Year), Math.Max(today + after, probe.Year))))
            {
                Failure = "a bound Calendar whose caption offers years by no rule the plan knows";
                return;
            }

            Caption.Add(string.Create(CultureInfo.InvariantCulture, $"before={before}"));
            Caption.Add(string.Create(CultureInfo.InvariantCulture, $"after={after}"));
        }

        private static List<string?> Texts(ComboBox select) => [.. select.Items.Select(static item => item?.ToString())];

        /// <summary>The first and last years a select offers, when it offers consecutive years.</summary>
        private static (int First, int Last)? Years(ComboBox select)
        {
            var years = Texts(select).Select(static text => int.TryParse(text, NumberStyles.None, CultureInfo.CurrentCulture, out var year) ? year : (int?)null).ToList();
            return years.Count > 1 && years.TrueForAll(static year => year is not null) && years.Zip(years.Skip(1)).All(static pair => pair.Second == pair.First + 1)
                ? (years[0]!.Value, years[^1]!.Value)
                : null;
        }

        /// <summary>
        /// The rule the library numbers its weeks by, among ISO 8601's and every week rule and first day, learned from the
        /// weeks the probes showed and from each year's first and last months over eight years; none when it numbers none.
        /// </summary>
        public void WeekRule()
        {
            if (_numbers.Count == 0 || Failure is not null)
            {
                return;
            }

            var year = DateTime.Today.Year;
            foreach (var month in Enumerable.Range(year - 3, 8).SelectMany(at => new[] { new DateTime(at, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(at, 12, 1, 0, 0, 0, DateTimeKind.Unspecified) }))
            {
                Show(month, []);
                foreach (var row in days.Chunk(7).Where(static row => row.Any(static day => day.IsVisible)))
                {
                    Number(row[0]);
                }
            }

            var rules = Enum.GetValues<CalendarWeekRule>().SelectMany(rule => Enum.GetValues<DayOfWeek>().Select(day => $"{rule}:{day}")).Prepend("iso");
            Weeks = rules.FirstOrDefault(rule => _numbers.TrueForAll(number => CalendarMonths.WeekNumber(rule, number.First) == number.Text));
            if (Weeks is null)
            {
                Failure = "a bound Calendar whose weeks are numbered by no rule the plan knows";
            }
        }
    }
}
