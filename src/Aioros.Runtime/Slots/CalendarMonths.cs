using System.Collections;
using System.Collections.Frozen;
using System.Globalization;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Slots;

/// <summary>
/// The month a calendar shows, computed for a render with date arithmetic as Avalonia's <c>CalendarItem</c> lays it out:
/// six weeks from the culture's first day of the week (a whole week of the previous month when the month starts on it),
/// each day's state (another month's, today, selected, its place in a range, its column, the tab stop) named by a key the
/// plan learned the look of, each week shown or hidden by the rule the plan learned, with its number in the rule the plan
/// learned; the caption, the dropdowns' month and years and the calendar's hidden field. The days shown and selected come
/// from the model's members the calendar binds, or from the values the compiled page held.
/// </summary>
public static class CalendarMonths
{
    private const string Day = "yyyy-MM-dd";

    /// <summary>The getter of a calendar slot: its part (<see cref="SlotSpec.Argument"/>) of the month, or of a week or a day of it.</summary>
    public static ValueGetter Getter(SlotSpec spec, MemberPaths paths)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(paths);
        switch (spec.Argument)
        {
            case "shown":
                return static week => ((CalendarWeek)week!).Shown ? "shown" : "hidden";
            case "days":
                return static week => ((CalendarWeek)week!).Days;
            case "number":
                return static week => ((CalendarWeek)week!).Number;
            case "key":
                return static day => ((CalendarDay)day!).Key;
            case "text":
                return static day => ((CalendarDay)day!).Text;
        }

        var definition = Definition.Parse(spec, paths);
        return spec.Argument switch
        {
            "weeks" => scope => definition.Month(scope).Weeks(definition),
            "state" => scope => definition.Month(scope).State(definition),
            "caption" => scope => definition.Month(scope).Display.ToString("Y", CultureInfo.CurrentCulture.DateTimeFormat),
            "month" => scope => Names(definition)[definition.Month(scope).Display.Month - 1],
            "year" => scope => definition.Month(scope).Display.Year.ToString(CultureInfo.CurrentCulture),
            "years" => scope => definition.Month(scope).Years(definition),
            _ => throw new InvalidOperationException($"Unknown calendar part '{spec.Argument}'."),
        };
    }

    /// <summary>The selected days as runs of consecutive days (<c>2025-06-03,2025-06-12~2025-06-13</c>), as a calendar's hidden field names them.</summary>
    public static string Runs(IEnumerable<DateTime> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        var days = dates.Select(date => date.Date).Distinct().Order().ToList();
        var runs = new List<string>();
        for (var index = 0; index < days.Count;)
        {
            var end = index;
            while (end + 1 < days.Count && days[end + 1] == days[end].AddDays(1))
            {
                end++;
            }

            runs.Add(end == index ? days[index].ToString(Day, CultureInfo.InvariantCulture) : $"{days[index].ToString(Day, CultureInfo.InvariantCulture)}~{days[end].ToString(Day, CultureInfo.InvariantCulture)}");
            index = end + 1;
        }

        return string.Join(',', runs);
    }

    /// <summary>The days the runs of <see cref="Runs"/> name, in order; none when a run is not a date.</summary>
    public static List<DateTime>? Days(string runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var days = new List<DateTime>();
        foreach (var run in runs.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var ends = run.Split('~');
            if (!DateTime.TryParseExact(ends[0], Day, CultureInfo.InvariantCulture, DateTimeStyles.None, out var first)
                || !DateTime.TryParseExact(ends[^1], Day, CultureInfo.InvariantCulture, DateTimeStyles.None, out var last))
            {
                return null;
            }

            for (var date = first; date <= last; date = date.AddDays(1))
            {
                days.Add(date);
            }
        }

        return days;
    }

    /// <summary>
    /// A day's key: <c>a</c> or <c>o</c> for a day of the month or of another, <c>s</c> or <c>u</c> selected or not, its place
    /// in a range of several days (<c>S</c>, <c>M</c>, <c>E</c>, else <c>-</c>), its column (<c>f</c>irst, <c>m</c>iddle,
    /// <c>l</c>ast), <c>t</c> for today and <c>k</c> for the tab stop (else <c>-</c>).
    /// </summary>
    public static string Key(bool outside, bool selected, char place, int column, bool today, bool stop) =>
        string.Concat(outside ? "o" : "a", selected ? "s" : "u", place.ToString(), column == 0 ? "f" : column == 6 ? "l" : "m", today ? "t" : "-", stop ? "k" : "-");

    /// <summary>The month's six weeks of seven days from its first shown day, as Avalonia lays them out.</summary>
    public static DateTime FirstShown(DateTime display, DayOfWeek firstDay)
    {
        var first = new DateTime(display.Year, display.Month, 1, 0, 0, 0, display.Kind);
        var offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
        return first.AddDays(-(offset == 0 ? 7 : offset));
    }

    /// <summary>A week's number in a rule (<c>iso</c>, or a week rule and the week's first day, <c>FirstFourDayWeek:Monday</c>) for its first day.</summary>
    public static string WeekNumber(string rule, DateTime first)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var number = rule == "iso"
            ? ISOWeek.GetWeekOfYear(first)
            : CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(first, Enum.Parse<CalendarWeekRule>(rule[..rule.IndexOf(':', StringComparison.Ordinal)]), Enum.Parse<DayOfWeek>(rule[(rule.IndexOf(':', StringComparison.Ordinal) + 1)..]));
        return number.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>The month names the month dropdown offers, abbreviated or not as the compiled page's.</summary>
    private static string[] Names(Definition definition) =>
        definition.Get("names") == "full" ? CultureInfo.CurrentCulture.DateTimeFormat.MonthNames : CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames;

    private sealed record Definition(FrozenDictionary<string, string> Values, (string Role, ValueGetter Get)[] Sources)
    {
        public string Mode => Values["mode"];

        public DayOfWeek FirstDay => (DayOfWeek)int.Parse(Values["first"], CultureInfo.InvariantCulture);

        public string? Get(string key) => Values.GetValueOrDefault(key);

        public static Definition Parse(SlotSpec spec, MemberPaths paths)
        {
            var values = spec.Values.Select(static value => value.Split('=', 2)).ToFrozenDictionary(static pair => pair[0], static pair => pair[1], StringComparer.Ordinal);
            var roles = values.GetValueOrDefault("roles", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
            var segments = (spec.Path ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries);
            return new Definition(values, [.. roles.Zip(segments, (role, path) => (role, paths.Getter(path)))]);
        }

        public Month Month(object? scope)
        {
            var display = Values.TryGetValue("display", out var literal)
                ? DateTime.ParseExact(literal, Day, CultureInfo.InvariantCulture)
                : DateTime.Today;
            var selected = Values.TryGetValue("selected", out var runs) ? Days(runs) ?? [] : [];
            DateTime? start = null;
            DateTime? end = null;
            foreach (var (role, get) in Sources)
            {
                var value = get(scope);
                switch (role)
                {
                    case "display":
                        display = value as DateTime? ?? throw new PlanMissException("a calendar's display date that is not a date");
                        break;
                    case "date":
                        selected = value is DateTime date ? [date.Date] : [];
                        break;
                    case "start":
                        start = value as DateTime?;
                        break;
                    case "end":
                        end = value as DateTime?;
                        break;
                    case "dates":
                        selected = value is IEnumerable dates ? [.. dates.OfType<DateTime>().Select(day => day.Date)] : [];
                        break;
                }
            }

            if (start is { } from)
            {
                var to = end ?? from;
                selected = [.. Enumerable.Range(0, Math.Abs((to.Date - from.Date).Days) + 1).Select(day => (from.Date < to.Date ? from.Date : to.Date).AddDays(day))];
            }

            return new Month(display, Mode switch
            {
                "None" => [],
                "SingleDate" => selected.Take(1).ToHashSet(),
                _ => selected.ToHashSet(),
            });
        }
    }

    private sealed record Month(DateTime Display, HashSet<DateTime> Selected)
    {
        public string State(Definition definition) =>
            $"{Display.ToString(Day, CultureInfo.InvariantCulture)}|{definition.Get("view")}|||{Runs(Selected)}";

        public List<string> Years(Definition definition)
        {
            var today = DateTime.Today.Year;
            var first = Math.Min(today - int.Parse(definition.Get("before") ?? "0", CultureInfo.InvariantCulture), Display.Year);
            var last = Math.Max(today + int.Parse(definition.Get("after") ?? "0", CultureInfo.InvariantCulture), Display.Year);
            return [.. Enumerable.Range(first, last - first + 1).Select(year => year.ToString(CultureInfo.CurrentCulture))];
        }

        public List<CalendarWeek> Weeks(Definition definition)
        {
            var start = FirstShown(Display, definition.FirstDay);
            var today = definition.Get("today") == "1" ? DateTime.Today : (DateTime?)null;
            var range = definition.Mode == "SingleRange" && Selected.Count > 1 ? (From: Selected.Min(), To: Selected.Max()) : ((DateTime From, DateTime To)?)null;
            var days = Enumerable.Range(0, 42).Select(index => start.AddDays(index)).ToList();
            var active = days.Where(Shows).ToList();
            var stop = active.FirstOrDefault(Selected.Contains, active.FirstOrDefault(day => day == today, active.FirstOrDefault()));
            var rule = definition.Get("weeks");
            return [.. days.Chunk(7).Select(week => new CalendarWeek(
                definition.Get("rows") == "all" || week.Any(Shows),
                rule is null ? string.Empty : WeekNumber(rule, week[0]),
                [.. week.Select((day, column) => new CalendarDay(
                    Key(!Shows(day), Selected.Contains(day), Place(day, range), column, day == today, day == stop),
                    day.Day.ToString(CultureInfo.CurrentCulture)))]))];
        }

        private bool Shows(DateTime day) => day.Year == Display.Year && day.Month == Display.Month;

        private static char Place(DateTime day, (DateTime From, DateTime To)? range) => range switch
        {
            { } r when day == r.From => 'S',
            { } r when day == r.To => 'E',
            { } r when day > r.From && day < r.To => 'M',
            _ => '-',
        };
    }
}

/// <summary>A week of a calendar's planned month: whether it is shown, its number and its days.</summary>
public sealed record CalendarWeek(bool Shown, string Number, List<CalendarDay> Days);

/// <summary>A day of a calendar's planned month: the key of its look and its text.</summary>
public sealed record CalendarDay(string Key, string Text);
