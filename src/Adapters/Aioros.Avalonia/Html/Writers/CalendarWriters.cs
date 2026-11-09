using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Calendar = Avalonia.Controls.Calendar;

namespace Aioros.Avalonia.Html.Writers;

internal static class CalendarWriters
{
    private const string Chevrons =
        "<svg viewBox=\"0 0 42 40\" width=\"42\" height=\"40\"><path d=\"M12 24 21 15 30 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\"/></svg>"
        + "<svg viewBox=\"0 0 42 40\" width=\"42\" height=\"40\"><path d=\"M12 15 21 24 30 15\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\"/></svg>";

    private static readonly (string Pseudo, string Class)[] CellStates = [(":inactive", "o"), (":today", "t"), (":selected", "s"), (":blackout", "b")];

    public static void WriteCalendar(HtmlContext context, Calendar calendar, ParentKind parent)
    {
        if (ThemedCalendarWriters.Writes(calendar))
        {
            ThemedCalendarWriters.Write(context, calendar, parent);
            return;
        }

        if (context.Plan.Planning && (LocalBindings.IsBound(calendar, Calendar.SelectedDateProperty) || LocalBindings.IsBound(calendar, Calendar.DisplayDateProperty)))
        {
            context.Plan.Fail("a Calendar with a bound SelectedDate or DisplayDate");
        }

        var month = calendar.DisplayMode == CalendarMode.Month;
        if (calendar.GetVisualDescendants().OfType<CalendarItem>().FirstOrDefault() is not { } item
            || RangeParts.Part<Grid>(item, month ? "PART_MonthView" : "PART_YearView") is not { } view)
        {
            return;
        }

        var header = RangeParts.Part<Button>(item, "PART_HeaderButton");
        var style = context.Box(calendar, parent);
        BoxStyles.Fill(style, calendar.Background, calendar.BorderBrush, calendar.BorderThickness, calendar.CornerRadius, calendar.Padding);
        style.Append("overflow:hidden;");
        var classes = CssValues.Join(CalendarClass(context.Sheet, calendar, item, month, header?.IsEffectivelyEnabled ?? true), context.Extra(calendar));
        var html = new StringBuilder();
        html.Append(CultureInfo.InvariantCulture, $"<div class=\"{context.Sheet.Class(classes, style)}\"><div class=\"ch\"><span>{CssValues.Encode(header?.Content?.ToString())}</span>");
        html.Append(CultureInfo.InvariantCulture, $"<span class=\"cn\" aria-hidden=\"true\">{Chevrons}</span></div><table>");
        if (month)
        {
            Titles(html, view);
        }

        Cells(html, view);
        html.Append("</table></div>");
        context.Plan.Literal(html.ToString());
    }

    private static void Titles(StringBuilder html, Grid view)
    {
        html.Append("<thead><tr>");
        foreach (var title in view.Children.Where(child => child is not Button && Grid.GetRow(child) == 0).OrderBy(Grid.GetColumn))
        {
            var text = title switch
            {
                TextBlock block => block.Text,
                ContentControl control => control.Content?.ToString(),
                _ => title.DataContext?.ToString(),
            };
            html.Append(CultureInfo.InvariantCulture, $"<th>{CssValues.Encode(text)}</th>");
        }

        html.Append("</tr></thead>");
    }

    private static void Cells(StringBuilder html, Grid view)
    {
        html.Append("<tbody>");
        foreach (var row in view.Children.OfType<Button>().GroupBy(Grid.GetRow).OrderBy(row => row.Key))
        {
            html.Append("<tr>");
            foreach (var cell in row.OrderBy(Grid.GetColumn))
            {
                var states = string.Join(' ', CellStates.Where(state => cell.Classes.Contains(state.Pseudo)).Select(state => state.Class));
                var classes = states.Length == 0 ? string.Empty : $" class=\"{states}\"";
                var selected = cell.Classes.Contains(":selected") ? " aria-selected=\"true\"" : string.Empty;
                html.Append(CultureInfo.InvariantCulture, $"<td{classes}{selected}>{CssValues.Encode(cell.Content?.ToString())}</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</tbody>");
    }

    private static string CalendarClass(StyleSheet sheet, Calendar calendar, CalendarItem item, bool month, bool headerEnabled)
    {
        string Resource(string key, string fallback) => RangeParts.Resource(calendar, key, fallback);
        var foreground = RangeParts.Color(calendar.Foreground);
        var accent = Resource("SystemAccentColor", "#0078d7");
        var today = Resource("CalendarViewTodayForeground", "#ffffff");
        var size = CssValues.Px(calendar.FontSize);
        var header = headerEnabled ? foreground : Resource("CalendarViewWeekDayForegroundDisabled", foreground);
        var layer = RangeParts.Color(RangeParts.Part<Border>(item, "BackgroundLayer")?.Background);
        var css = new StringBuilder();
        css.Append(CultureInfo.InvariantCulture, $"{{color:{foreground}}}$ .ch{{display:flex;align-items:center;height:40px;padding-left:12px;font-size:{size};color:{header}}}");
        css.Append("$ .ch>span:first-child{flex:1}$ .cn{display:flex;opacity:.8}$ table{border-collapse:collapse;border-spacing:0;table-layout:fixed}");
        css.Append(CultureInfo.InvariantCulture, $"$ th{{height:38px;padding:0;font-size:12px;font-weight:400;text-align:center}}$ tbody{{background:{layer}}}");
        css.Append(CultureInfo.InvariantCulture, $"$ td{{width:{(month ? "42px" : "73.5px")};height:{(month ? "42px" : "96.67px")};padding:1px;background-clip:content-box;text-align:center;font-size:{size};color:{Resource("CalendarViewCalendarItemForeground", foreground)}}}");
        css.Append(CultureInfo.InvariantCulture, $"$ td.o{{background-color:{Resource("CalendarViewOutOfScopeBackground", "transparent")};color:{Resource("CalendarViewOutOfScopeForeground", foreground)}}}");
        css.Append(CultureInfo.InvariantCulture, $"$ td.b{{color:{Resource("CalendarViewBlackoutForeground", foreground)};text-decoration:line-through}}");
        css.Append(CultureInfo.InvariantCulture, $"$ td.t{{background-color:{accent};color:{today};font-weight:600}}");
        css.Append(month
            ? $"$ td.s{{outline:2px solid {Resource("CalendarViewSelectedBorderBrush", accent)};outline-offset:-3px}}"
            : $"$ td.s{{background-color:{accent};color:{today};font-weight:600}}");
        var name = "ca" + CssValues.Hash(css.ToString());
        sheet.AddRule(name, "." + name + css.ToString().Replace("$", "." + name, StringComparison.Ordinal) + "\n");
        return name;
    }
}
