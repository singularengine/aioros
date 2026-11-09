using System.Collections.Immutable;
using System.Text;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Calendar = Avalonia.Controls.Calendar;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A calendar whose control theme is not Fluent's, written through its template as react-day-picker writes shadcn/ui's:
/// the template's parts as they are (its navigation buttons and caption posting their clicks), its day grid a
/// <c>&lt;table role="grid"&gt;</c> whose day buttons take the look their template's root takes in each of their states,
/// and a hidden field naming what it shows (<see cref="CalendarPosts"/>); what a library adds to the month view (a week's
/// number) is written in its row at its column, a row header in a week. Both views are always written, the one Avalonia
/// hides <c>hidden</c>, and so is every week, so the buttons are numbered alike whatever month or mode a post shows.
/// </summary>
internal static class ThemedCalendarWriters
{
    private const string Table = "cg";

    private const string Presenter = "PART_ContentPresenter";

    /// <summary>The children Avalonia's <see cref="CalendarItem"/> gives its month view: seven day titles and 42 days; any after them are a library's, written in their row (react-day-picker's week numbers).</summary>
    public const int Own = 49;

    /// <summary>
    /// A day's states: pointed at and pressed as a button's, and Avalonia's focused day (<c>:dayfocused</c>, the day the
    /// calendar's keyboard focus is on) as the button with the focus; a day is never focused itself in Avalonia.
    /// </summary>
    private static readonly (string Pseudo, string Selector)[] DayStates = [(":pointerover", "hover"), (":pressed", "active"), (":dayfocused", "focus-visible")];

    /// <summary>A month's or a year's states, its focused one the year view's <c>:btnfocused</c>.</summary>
    private static readonly (string Pseudo, string Selector)[] CellStates = [(":pointerover", "hover"), (":pressed", "active"), (":btnfocused", "focus-visible")];

    public static bool Writes(Calendar calendar) => !FluentThemes.Owns(calendar, typeof(Calendar)) && Item(calendar) is not null;

    public static void Write(HtmlContext context, Calendar calendar, ParentKind parent)
    {
        var item = Item(calendar)!;
        var (month, year) = (RangeParts.Part<Grid>(item, "PART_MonthView"), RangeParts.Part<Grid>(item, "PART_YearView"));
        var planned = context.Plan.Planning && Bound(calendar) is { Count: > 0 } bound ? Planned(context, calendar, month, bound) : null;
        context.Sheet.AddRule(Table, $".{Table}{{border-collapse:collapse;border-spacing:0}}.{Table} th,.{Table} td{{padding:0;font-weight:inherit}}\n");
        var style = context.Box(calendar, parent);
        context.Plan.Literal("<div data-aioros-calendar");
        context.Root(calendar, "c", style);
        context.Plan.Literal("><input type=\"hidden\" name=\"f");
        context.Field(calendar);
        if (planned is null)
        {
            context.Plan.Literal($"\" value=\"{CssValues.Encode(CalendarPosts.State(calendar))}\">");
        }
        else
        {
            context.Plan.Literal("\" value=\"");
            context.Plan.Add(planned.Value("state"));
            context.Plan.Literal("\">");
        }

        var views = new[] { month, year }.OfType<Grid>().ToList();
        foreach (var view in views)
        {
            context.Substitutes[view] = at => Views(context, calendar, month, year, at, planned);
        }

        if (calendar.GetVisualChildren().OfType<Control>().FirstOrDefault() is { } root)
        {
            Template(context, calendar, root, planned);
        }

        views.ForEach(view => context.Substitutes.Remove(view));
        context.Plan.Literal("</div>");
    }

    /// <summary>The calendar's template; a planned one's caption and selects computed for each render (<see cref="PlannedCalendar.Captioned"/>).</summary>
    private static void Template(HtmlContext context, Calendar calendar, Control root, PlannedCalendar? planned)
    {
        if (planned is null)
        {
            context.Write(root, ParentKind.Single);
            return;
        }

        context.Plan.Begin(context.Plan.Scope);
        using (context.Plan.Computing(calendar))
        {
            context.Write(root, ParentKind.Single);
        }

        var nodes = context.Plan.End();
        if (planned.Captioned(nodes, calendar.DisplayDate.ToString("Y", System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat)) is { } captioned)
        {
            nodes = captioned;
        }
        else
        {
            context.Plan.Fail("a bound Calendar whose caption the plan cannot find in its template");
        }

        foreach (var node in nodes)
        {
            context.Plan.Add(node);
        }
    }

    /// <summary>The plan of a calendar whose month or selection is bound (<see cref="PlannedCalendars"/>), or none when it fails the plan.</summary>
    private static PlannedCalendar? Planned(HtmlContext context, Calendar calendar, Grid? month, List<AvaloniaProperty> bound)
    {
        if (bound.Except(PlannedCalendars.Bound(calendar)).FirstOrDefault() is { } other)
        {
            context.Plan.Fail($"a Calendar with a bound {other.Name}");
            return null;
        }

        if (month is null)
        {
            context.Plan.Fail("a bound Calendar without a month view");
            return null;
        }

        return PlannedCalendars.Learn(context, calendar, month, Days(month));
    }

    private static List<CalendarDayButton> Days(Grid view) => [.. view.Children.OfType<CalendarDayButton>().OrderBy(Grid.GetRow).ThenBy(Grid.GetColumn)];

    private static CalendarItem? Item(Calendar calendar) => calendar.GetVisualDescendants().OfType<CalendarItem>().FirstOrDefault();

    /// <summary>
    /// The calendar's properties bound to the model; none for the calendar of a date picker's template, whose bindings read
    /// the picker, whose writer answers for the model. Only those saying what it shows are planned (<see cref="PlannedCalendars"/>).
    /// </summary>
    private static List<AvaloniaProperty> Bound(Calendar calendar) => calendar.TemplatedParent is CalendarDatePicker
        ? []
        : [.. AvaloniaPropertyRegistry.Instance.GetRegistered(calendar).Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(calendar.GetType()))
            .Where(property => LocalBindings.IsBound(calendar, property))];

    private static void Views(HtmlContext context, Calendar calendar, Grid? month, Grid? year, ParentKind parent, PlannedCalendar? planned)
    {
        var shown = calendar.DisplayMode == CalendarMode.Month;
        if (month is not null)
        {
            Month(context, calendar, month, shown ? parent : null, planned);
        }

        if (year is not null)
        {
            Year(context, year, shown ? null : parent);
        }
    }

    private static void Month(HtmlContext context, Calendar calendar, Grid view, ParentKind? parent, PlannedCalendar? planned)
    {
        var days = Days(view);
        if (parent is not { } at)
        {
            Hidden(context, days, 7);
            return;
        }

        var multiple = calendar.SelectionMode is CalendarSelectionMode.SingleRange or CalendarSelectionMode.MultipleRange ? " aria-multiselectable=\"true\"" : string.Empty;
        context.Plan.Literal($"<table role=\"grid\"{multiple} class=\"{context.Classes(view, Table, Shared(context.Box(view, at), view))}\"><thead><tr>");
        var own = view.Children.Take(Own).ToHashSet();
        var extra = view.Children.Skip(Own).Where(child => child.IsVisible).ToLookup(Grid.GetRow);
        foreach (var title in view.Children.Where(child => child is not CalendarDayButton && Grid.GetRow(child) == 0 && (own.Contains(child) || child.IsVisible)).OrderBy(Grid.GetColumn))
        {
            var width = new StringBuilder(Width(view, Grid.GetColumn(title)) + Height(view, 0));
            context.Plan.Literal($"<th{(own.Contains(title) ? " scope=\"col\"" : string.Empty)}{Class(context, width)}>");
            context.Plan.WriteConstant(title, () => context.Write(title, ParentKind.Single));
            context.Plan.Literal("</th>");
        }

        context.Plan.Literal("</tr></thead><tbody>");
        if (planned is not null)
        {
            PlannedWeeks(context, view, days, extra, planned);
            context.Plan.Literal("</tbody></table>");
            return;
        }

        var stop = Stop(days);
        foreach (var week in days.GroupBy(Grid.GetRow))
        {
            if (!week.Any(day => day.IsVisible))
            {
                context.Plan.Literal("<tr hidden>");
                week.ToList().ForEach(day => Bare(context, day, "<td role=\"gridcell\">", "</td>"));
                context.Plan.Literal("</tr>");
                continue;
            }

            Week(context, view, [.. week.Cast<Control>().Concat(extra[week.Key]).OrderBy(Grid.GetColumn)], stop);
        }

        context.Plan.Literal("</tbody></table>");
    }

    /// <summary>
    /// A planned month's weeks (<see cref="PlannedCalendar.Weeks"/>), its day buttons numbered in their order as when each
    /// is written, and the number a library writes before or after a shown week's days a slot in the markup its rows share.
    /// </summary>
    private static void PlannedWeeks(HtmlContext context, Grid view, List<CalendarDayButton> days, ILookup<int, Control> extra, PlannedCalendar planned)
    {
        days.ForEach(day => context.Fields.Add(day, button: true));
        var columns = (First: days.Min(Grid.GetColumn), Last: days.Max(Grid.GetColumn));
        var heads = new HashSet<(bool Before, string Prefix, string Suffix)>();
        var bare = 0;
        foreach (var week in days.GroupBy(Grid.GetRow).Where(week => week.Any(day => day.IsVisible)))
        {
            var cells = extra[week.Key].ToList();
            if (cells.Count == 0)
            {
                bare++;
                continue;
            }

            if (cells is not [TextBlock { Text: { } number } cell] || Grid.GetColumn(cell) is var column && column > columns.First && column < columns.Last)
            {
                context.Plan.Fail("a bound Calendar whose weeks hold more than a number before or after their days");
                return;
            }

            if (Head(context, view, cell, number) is not { } markup)
            {
                context.Plan.Fail("a bound Calendar whose week numbers the plan cannot find in their markup");
                return;
            }

            heads.Add((Grid.GetColumn(cell) < columns.First, markup.Prefix, markup.Suffix));
        }

        if (heads.Count > 1 || (heads.Count == 1 && bare > 0))
        {
            context.Plan.Fail("a bound Calendar whose weeks are not all written alike");
            return;
        }

        ImmutableArray<IPlanNode> head = heads.Count == 0 ? [] : [new LiteralNode(heads.Single().Prefix), PlannedCalendar.ItemValue("number"), new LiteralNode(heads.Single().Suffix)];
        context.Plan.Add(planned.Weeks(heads.SingleOrDefault().Before ? head : [], heads.SingleOrDefault().Before ? [] : head));
    }

    /// <summary>A week's number written as a row header, as the markup before its text and after it; none when its text is not found once.</summary>
    private static (string Prefix, string Suffix)? Head(HtmlContext context, Grid view, TextBlock cell, string number)
    {
        context.Plan.Begin(context.Plan.Scope);
        context.Plan.Literal($"<th scope=\"row\"{Class(context, new StringBuilder(Width(view, Grid.GetColumn(cell))))}>");
        context.Write(cell, ParentKind.Single);
        context.Plan.Literal("</th>");
        var nodes = context.Plan.End();
        var text = ">" + CssValues.Encode(number) + "<";
        var markup = string.Concat(nodes.OfType<LiteralNode>().Select(static node => node.Text));
        var at = markup.IndexOf(text, StringComparison.Ordinal);
        return nodes.Any(static node => node is not LiteralNode) || at < 0 || at != markup.LastIndexOf(text, StringComparison.Ordinal)
            ? null
            : (markup[..(at + 1)], markup[(at + text.Length - 1)..]);
    }

    private static string Class(HtmlContext context, StringBuilder style) => context.Sheet.Class(null, style) is { Length: > 0 } name ? $" class=\"{name}\"" : string.Empty;

    /// <summary>A shown week: its days, and what a library adds to its row as row headers, in their columns' order.</summary>
    private static void Week(HtmlContext context, Grid view, List<Control> cells, CalendarDayButton? stop)
    {
        context.Plan.Literal("<tr>");
        foreach (var cell in cells)
        {
            if (cell is CalendarDayButton day)
            {
                Day(context, day, day == stop);
                continue;
            }

            context.Plan.Literal($"<th scope=\"row\"{Class(context, new StringBuilder(Width(view, Grid.GetColumn(cell))))}>");
            context.Write(cell, ParentKind.Single);
            context.Plan.Literal("</th>");
        }

        context.Plan.Literal("</tr>");
    }

    private static void Year(HtmlContext context, Grid view, ParentKind? parent)
    {
        var cells = view.Children.OfType<CalendarButton>().OrderBy(Grid.GetRow).ThenBy(Grid.GetColumn).ToList();
        if (parent is not { } at)
        {
            Hidden(context, cells, Math.Max(1, view.ColumnDefinitions.Count));
            return;
        }

        context.Plan.Literal($"<table role=\"grid\" class=\"{context.Classes(view, Table, Shared(context.Box(view, at), view))}\"><tbody>");
        foreach (var row in cells.GroupBy(Grid.GetRow))
        {
            context.Plan.Literal("<tr>");
            foreach (var cell in row)
            {
                context.Plan.Literal($"<td role=\"gridcell\"{(Is(cell, ":selected") ? " aria-selected=\"true\"" : string.Empty)}>");
                Cell(context, cell);
                context.Plan.Literal("</td>");
            }

            context.Plan.Literal("</tr>");
        }

        context.Plan.Literal("</tbody></table>");
    }

    /// <summary>A view Avalonia hides, written <c>hidden</c> with a bare button per cell so the buttons keep their numbers.</summary>
    private static void Hidden(HtmlContext context, IReadOnlyList<Button> cells, int columns)
    {
        context.Plan.Literal("<table hidden><tbody>");
        foreach (var row in cells.Chunk(columns))
        {
            context.Plan.Literal("<tr>");
            foreach (var cell in row)
            {
                Bare(context, cell, "<td>", "</td>");
            }

            context.Plan.Literal("</tr>");
        }

        context.Plan.Literal("</tbody></table>");
    }

    private static void Bare(HtmlContext context, Button button, string open, string close)
    {
        context.Plan.Literal($"{open}<button type=\"submit\" name=\"_btn\" value=\"");
        context.Button(button);
        context.Plan.Literal($"|\" tabindex=\"-1\"></button>{close}");
    }

    /// <summary>
    /// The width of a column as the grid sizes it: its pixels, or its share of the table for a star column, the table then
    /// as wide as its box with fixed columns, as the grid shares its width among them; nothing for an automatic one.
    /// </summary>
    private static string Width(Grid view, int column) => view.ColumnDefinitions.ElementAtOrDefault(column)?.Width switch
    {
        { IsAbsolute: true } width => $"width:{CssValues.Px(width.Value)};",
        { IsStar: true } width => $"width:{CssValues.Num(width.Value / Stars(view) * 100)}%;",
        _ => string.Empty,
    };

    private static string Height(Grid view, int row) =>
        view.RowDefinitions.ElementAtOrDefault(row)?.Height is { IsAbsolute: true } height ? $"height:{CssValues.Px(height.Value)};" : string.Empty;

    private static double Stars(Grid view) => view.ColumnDefinitions.Where(static column => column.Width.IsStar).Sum(static column => column.Width.Value);

    private static StringBuilder Shared(StringBuilder style, Grid view) =>
        Stars(view) > 0 ? style.Append("min-width:100%;") : style;

    /// <summary>The day in the tab sequence, as the WAI-ARIA grid's roving tab stop: the selected day of the month shown, else today, else its first day.</summary>
    public static CalendarDayButton? Stop(List<CalendarDayButton> days)
    {
        var shown = days.Where(day => day.IsVisible && day.IsEffectivelyEnabled && day.IsHitTestVisible && !Is(day, ":inactive")).ToList();
        return shown.Find(day => Is(day, ":selected")) ?? shown.Find(day => Is(day, ":today")) ?? shown.FirstOrDefault();
    }

    /// <summary>
    /// A day: an element in the box of the template's band (<c>PART_Band</c>, filled where a library fills it) holding the
    /// button, in the look the template's root (<c>PART_Root</c>, else its content presenter) takes in the day's states,
    /// with the day's number as its own text; without the browser's outline, as Avalonia draws no focus adorner on a day.
    /// </summary>
    private static void Day(HtmlContext context, CalendarDayButton day, bool stop)
    {
        var (open, middle) = DayParts(context, day, stop);
        context.Plan.Literal(open);
        context.Button(day);
        context.Plan.Literal($"{middle}{CssValues.Encode(day.Content?.ToString())}{PlannedCalendars.Close}");
    }

    /// <summary>A day's markup before its button's number and after it up to its text.</summary>
    public static (string Open, string Middle) DayParts(HtmlContext context, CalendarDayButton day, bool stop)
    {
        ThemeLooks.ExpandTemplates(day);
        var parts = day.GetVisualChildren().OfType<Control>().FirstOrDefault();
        var (band, root) = parts is Border { Name.Length: > 0, Child: Border { Name.Length: > 0 } inner } outer ? (outer, inner) : (null, parts as Border);
        var style = context.Box(day, ParentKind.Single);
        var opacity = band?.Opacity ?? 1;
        if (opacity < 1)
        {
            style.Append("opacity:").Append(CssValues.Num(opacity * day.Opacity)).Append(';');
        }

        if (band is not null)
        {
            BoxStyles.Fill(style, band.Background, band.BorderBrush, band.BorderThickness, band.CornerRadius, default);
        }

        style.Append(Hit(day) ? string.Empty : "pointer-events:none;");
        var look = ThemeLooks.Of(day, root is { Name.Length: > 0 } named ? named.Name : Presenter, DayStates);
        var inside = new StringBuilder();
        var presenter = day.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault();
        BoxStyles.Font(inside, day.FontSize, day.FontWeight, presenter?.Foreground ?? day.Foreground);
        inside.Append(look.Css);
        inside.Append("display:flex;align-items:center;justify-content:center;width:100%;height:100%;outline:none;");
        inside.Append(presenter is not null && presenter.GetValue(TextBlock.LineHeightProperty) is var line and > 0 ? $"line-height:{CssValues.Px(line)};" : string.Empty);
        var attributes = Hit(day) && stop ? " tabindex=\"0\"" : " tabindex=\"-1\"";
        attributes += RangeParts.Disabled(day) + (Hit(day) ? string.Empty : " aria-hidden=\"true\"");
        return ($"<td role=\"gridcell\"{(Is(day, ":selected") ? " aria-selected=\"true\"" : string.Empty)}><div class=\"{context.Sheet.Class(null, style)}\"><button type=\"submit\" name=\"_btn\" value=\"",
            $"|\"{attributes} class=\"{context.Classes(day, context.Sheet.States(look), inside)}\">");
    }

    /// <summary>
    /// A month or a year of the year view: its button in the look of its template's root (a named border, else its content
    /// presenter), with its text as its own.
    /// </summary>
    private static void Cell(HtmlContext context, CalendarButton cell)
    {
        ThemeLooks.ExpandTemplates(cell);
        var root = cell.GetVisualChildren().OfType<Border>().FirstOrDefault(part => part.Name is { Length: > 0 });
        var look = ThemeLooks.Of(cell, root?.Name ?? Presenter, CellStates);
        var style = context.Box(cell, ParentKind.Single);
        var presenter = cell.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault();
        BoxStyles.Font(style, cell.FontSize, cell.FontWeight, presenter?.Foreground ?? cell.Foreground);
        style.Append(look.Css);
        style.Append("display:flex;align-items:center;justify-content:center;outline:none;");
        context.Plan.Literal("<button type=\"submit\" name=\"_btn\" value=\"");
        context.Button(cell);
        context.Plan.Literal($"|\"{(Is(cell, ":selected") ? string.Empty : " tabindex=\"-1\"")}{RangeParts.Disabled(cell)} class=\"{context.Classes(cell, context.Sheet.States(look), style)}\">{CssValues.Encode(cell.Content?.ToString())}</button>");
    }

    private static bool Hit(Control control) => control.IsHitTestVisible;

    private static bool Is(Control control, string pseudo) => control.Classes.Contains(pseudo);
}
