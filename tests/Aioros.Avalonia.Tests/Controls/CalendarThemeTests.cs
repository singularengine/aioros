using System.Globalization;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed partial class CalendarThemeTests(EngineFixture fixture)
{
    private const string Resources = """<StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/CalendarTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>""";

    private const string June = "2025-06-12|Month||";

    private static string Themed(string calendar) => $"""<StackPanel>{Resources}{calendar}</StackPanel>""";

    private static string Caption(int month) => new DateTime(2025, month, 1, 0, 0, 0, DateTimeKind.Unspecified).ToString("Y", CultureInfo.CurrentCulture);

    private static string Rule(string css, string classes) =>
        string.Concat(classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(name => Regex.Match(css, "\\." + name + "\\{([^}]*)\\}").Groups[1].Value));

    private static List<Match> Days(string html, int day) => [.. DayPattern().Matches(html).Where(match => match.Groups["day"].Value == day.ToString(CultureInfo.InvariantCulture))];

    private static string Field(string html) => FieldPattern().Match(html).Groups[1].Value;

    [Fact]
    [Trait("AC", "RANGES-17")]
    public async Task ThemedCalendarsAreWrittenThroughTheirTemplateAsync()
    {
        var page = await fixture.CompileAsync(Themed("""<Calendar DisplayDate="2025-06-12" SelectedDate="2025-06-12" />"""));
        var html = EngineFixture.Body(page);
        var titles = Regex.Matches(html, "<th scope=\"col\" class=\"([^\"]+)\">(.*?)</th>");
        var title = Regex.Match(titles[0].Groups[2].Value, "class=\"([^\"]+)\"").Groups[1].Value;

        Assert.Matches("<div data-aioros-calendar class=\"[^\"]+\"><input type=\"hidden\" name=\"f0\"", html);
        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"0\\|\"[^>]*>.*?‹.*?</button>", html);
        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"1\\|\"[^>]*>.*?" + Regex.Escape(Caption(6)) + ".*?</button>", html);
        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"2\\|\"[^>]*>.*?›.*?</button>", html);
        Assert.Matches("<table role=\"grid\" class=\"[^\"]+\"><thead><tr><th scope=\"col\"", html);
        Assert.Equal(7, titles.Count);
        Assert.Contains(CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames[0], titles[0].Groups[2].Value, StringComparison.Ordinal);
        Assert.Contains("width:32px;", Rule(page.Css, titles[0].Groups[1].Value), StringComparison.Ordinal);
        Assert.Contains("font-size:12.8px;", Rule(page.Css, title), StringComparison.Ordinal);
        Assert.Contains("color:#737373;", Rule(page.Css, title), StringComparison.Ordinal);
        Assert.Matches("\\{[^}]*background:#ffffff;[^}]*padding:12px 12px 12px 12px;", page.Css);
    }

    [Fact]
    [Trait("AC", "RANGES-34")]
    public async Task WeekTextsAreRowHeadersBeforeTheirDaysAsync()
    {
        const string Shown = """<StackPanel><StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/CalendarTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources><Calendar DisplayDate="2025-06-12" SelectedDate="2025-06-12" m:CalendarWeeks.Shows="True" /></StackPanel>""";
        var page = await fixture.CompileAsync(Shown);
        var hidden = EngineFixture.Body(await fixture.CompileAsync(Themed("""<Calendar DisplayDate="2025-06-12" SelectedDate="2025-06-12" m:CalendarWeeks.Shows="False" />""")));
        var html = EngineFixture.Body(page);
        var heads = Regex.Matches(html, "<tr><th scope=\"row\"[^>]*>(.*?)</th>(.*?)</tr>");
        var desktop = Rows((await fixture.CaptureInAsync(Shown, new CaptureOptions("light", 400, 500))).Texts);
        var web = Rows(await BrowserLayout.TextsAsync(page, 400, 500));

        Assert.Matches("<thead><tr><th>(<div[^>]*></div>)?</th><th scope=\"col\"", html);
        Assert.Equal(5, heads.Count);
        Assert.Contains("23", heads[0].Groups[1].Value, StringComparison.Ordinal);
        Assert.All(heads, head => Assert.Equal(7, Regex.Count(head.Groups[2].Value, "<td role=\"gridcell\"")));
        Assert.DoesNotContain("scope=\"row\"", hidden, StringComparison.Ordinal);
        Assert.Matches("<thead><tr><th scope=\"col\"", hidden);
        Assert.Equal(["23", "24", "25", "26", "27"], desktop.Select(week => week.Text));
        Assert.Equal(desktop.Select(week => week.Text), web.Select(week => week.Text));
        Assert.All(desktop.Zip(web), pair => Assert.InRange(pair.Second.Center, pair.First.Center - 1, pair.First.Center + 1));
        Assert.All(desktop.Zip(web), pair => Assert.InRange(pair.Second.Dy, pair.First.Dy - 1, pair.First.Dy + 1));
    }

    /// <summary>
    /// Each week number with its center, which its column's alignment fixes (its left edge follows the font's glyph widths, a
    /// pixel apart between Chromium builds), and its height against the days of its row, which a target's own header spacing
    /// does not move.
    /// </summary>
    private static List<(string Text, double Center, long Dy)> Rows(System.Collections.Immutable.ImmutableArray<LaidOutText> texts)
    {
        var days = texts.Where(text => text.X > 40 && int.TryParse(text.Text, CultureInfo.InvariantCulture, out _)).ToList();
        return [.. texts.Where(text => text.X < 40 && int.TryParse(text.Text, CultureInfo.InvariantCulture, out _)).OrderBy(text => text.Y)
            .Select(week => (week.Text, week.X + (week.Width / 2), (long)Math.Round(week.Y - days.MinBy(day => Math.Abs(day.Y - week.Y)).Y)))];
    }

    [Fact]
    [Trait("AC", "RANGES-18")]
    public async Task ThemedDaysTakeTheLookOfTheirStatesAsync()
    {
        var page = await fixture.CompileAsync($"""
            <StackPanel>
              {Themed("""
                <Calendar DisplayDate="2025-06-12" SelectedDate="2025-06-12" DisplayDateStart="2025-05-01" DisplayDateEnd="2025-07-03">
                  <Calendar.BlackoutDates><CalendarDateRange><x:Arguments><sys:DateTime>2025-06-01</sys:DateTime><sys:DateTime>2025-06-10</sys:DateTime></x:Arguments></CalendarDateRange></Calendar.BlackoutDates>
                </Calendar>
                """)}
              <Calendar DisplayDate="2025-06-12" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        var css = page.Css;
        var selected = Days(html, 12)[0];
        var inactive = Days(html, 2)[1];
        var blackout = Days(html, 5)[0];
        var beyond = Days(html, 4)[1];

        Assert.Equal(42, Regex.Count(html, "<td role=\"gridcell\""));
        Assert.True(selected.Groups["selected"].Success);
        Assert.Contains("background:#171717;", Rule(css, selected.Groups["root"].Value), StringComparison.Ordinal);
        Assert.Contains("color:#fafafa;", Rule(css, selected.Groups["root"].Value), StringComparison.Ordinal);
        Assert.Contains("margin:8px 0px 0px 0px;", Rule(css, selected.Groups["band"].Value), StringComparison.Ordinal);
        Assert.Contains("width:32px;height:32px;", Rule(css, selected.Groups["band"].Value), StringComparison.Ordinal);
        Assert.False(inactive.Groups["selected"].Success);
        Assert.Contains("color:#737373;", Rule(css, inactive.Groups["root"].Value), StringComparison.Ordinal);
        Assert.Contains("color:#737373;", Rule(css, blackout.Groups["root"].Value), StringComparison.Ordinal);
        Assert.Contains(" disabled", beyond.Groups["attributes"].Value, StringComparison.Ordinal);
        Assert.Contains("opacity:0;", Rule(css, beyond.Groups["band"].Value), StringComparison.Ordinal);
        Assert.Matches(":not\\(:disabled\\):hover\\{[^}]*background:#f5f5f5!important;", css);
        Assert.Contains("<div class=\"ch\"><span>", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-19")]
    public async Task RangesKeepTheMarksTheirLibraryGivesTheirDaysAsync()
    {
        var page = await fixture.CompileAsync(Themed("""
            <Calendar DisplayDate="2025-06-12" SelectionMode="SingleRange" m:CalendarPicks.RangeStart="2025-06-09" m:CalendarPicks.RangeEnd="2025-06-26" />
            """));
        var html = EngineFixture.Body(page);
        var css = page.Css;
        string Root(int day) => Rule(css, Days(html, day)[0].Groups["root"].Value);
        string Band(int day) => Rule(css, Days(html, day)[0].Groups["band"].Value);

        Assert.Contains("background:#171717;", Root(9), StringComparison.Ordinal);
        Assert.Contains("background:#f5f5f5;", Band(9), StringComparison.Ordinal);
        Assert.Contains("border-radius:8px 0px 0px 8px;", Band(9), StringComparison.Ordinal);
        Assert.Contains("background:#f5f5f5;", Root(10), StringComparison.Ordinal);
        Assert.DoesNotContain("border-radius", Root(10), StringComparison.Ordinal);
        Assert.Contains("border-radius:0px 8px 8px 0px;", Root(14), StringComparison.Ordinal);
        Assert.Contains("border-radius:8px 0px 0px 8px;", Root(15), StringComparison.Ordinal);
        Assert.Contains("background:#171717;", Root(26), StringComparison.Ordinal);
        Assert.Contains("border-radius:0px 8px 8px 0px;", Band(26), StringComparison.Ordinal);
        Assert.Contains("<table role=\"grid\" aria-multiselectable=\"true\"", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-20")]
    public async Task HiddenWeeksAndOutsideDaysKeepTheirButtonsAsync()
    {
        var page = await fixture.CompileAsync(Themed("""<Calendar DisplayDate="2025-06-12" SelectedDate="2025-06-12" Classes="outside-hidden" />"""));
        var html = EngineFixture.Body(page);
        var hidden = Regex.Matches(html, "<tr hidden>(.*?)</tr>");
        var outside = Days(html, 1)[1];

        Assert.Single(hidden);
        Assert.Equal(7, Regex.Count(hidden[0].Groups[1].Value, "<button type=\"submit\" name=\"_btn\""));
        Assert.Equal(" tabindex=\"-1\" aria-hidden=\"true\"", outside.Groups["attributes"].Value);
        Assert.Contains("opacity:0;", Rule(page.Css, outside.Groups["band"].Value), StringComparison.Ordinal);
        Assert.Contains("pointer-events:none;", Rule(page.Css, outside.Groups["band"].Value), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-21")]
    public async Task OneDayIsTheTabStopAsync()
    {
        var selected = EngineFixture.Body(await fixture.CompileAsync(Themed("""<Calendar DisplayDate="2025-06-12" SelectedDate="2025-06-12" />""")));
        var elsewhere = EngineFixture.Body(await fixture.CompileAsync(Themed("""<Calendar DisplayDate="2025-06-12" SelectedDate="2025-05-20" />""")));
        var today = EngineFixture.Body(await fixture.CompileAsync(Themed("<Calendar />")));
        static string Stop(string html) => DayPattern().Matches(html).Single(match => match.Groups["attributes"].Value.Contains("tabindex=\"0\"", StringComparison.Ordinal)).Groups["day"].Value;

        Assert.Equal("12", Stop(selected));
        Assert.Equal("1", Stop(elsewhere));
        Assert.Equal(DateTime.Today.Day.ToString(CultureInfo.InvariantCulture), Stop(today));
        Assert.Equal(41, Regex.Count(selected, "<td role=\"gridcell\"[^>]*>(?:<div[^>]*>)?<button [^>]*tabindex=\"-1\""));
    }

    [Fact]
    [Trait("AC", "RANGES-22")]
    public async Task YearAndDecadeViewsAreTablesOfTheirButtonsAsync()
    {
        var year = await fixture.CompileAsync(Themed("""<Calendar DisplayMode="Year" DisplayDate="2025-06-12" SelectedDate="2025-06-12" />"""));
        var decade = EngineFixture.Body(await fixture.CompileAsync(Themed("""<Calendar DisplayMode="Decade" DisplayDate="2025-06-12" SelectedDate="2025-06-12" />""")));
        var month = EngineFixture.Body(await fixture.CompileAsync(Themed("""<Calendar DisplayDate="2025-06-12" SelectedDate="2025-06-12" />""")));
        var html = EngineFixture.Body(year);
        var months = Regex.Match(html, "<table role=\"grid\" class=\"[^\"]+\"><tbody>(.*?)</tbody></table>").Groups[1].Value;
        var june = Regex.Match(months, "<button [^>]* class=\"([^\"]+)\">" + CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames[5] + "</button>");

        Assert.Equal(3, Regex.Count(months, "<tr>"));
        Assert.Equal(12, Regex.Count(months, "<button type=\"submit\" name=\"_btn\""));
        Assert.Contains("background:#171717;", Rule(year.Css, june.Groups[1].Value), StringComparison.Ordinal);
        Assert.Matches("<table hidden><tbody>(<tr>(<td><button [^>]*></button></td>){7}</tr>){6}</tbody></table>", html);
        Assert.Single(Regex.Matches(html, "<table role=\"grid\""));
        Assert.Single(Regex.Matches(month, "<table role=\"grid\""));
        Assert.Contains(">2025</button>", decade, StringComparison.Ordinal);
        Assert.Equal(57, Regex.Count(html, "<button type=\"submit\" name=\"_btn\""));
        Assert.Equal(57, Regex.Count(decade, "<button type=\"submit\" name=\"_btn\""));
        Assert.Equal(57, Regex.Count(month, "<button type=\"submit\" name=\"_btn\""));
    }

    [Fact]
    [Trait("AC", "RANGES-23")]
    public async Task CalendarsKeepWhatTheyShowInAHiddenFieldAsync()
    {
        var page = await fixture.CompileAsync(Themed("""<Calendar DisplayDate="2025-06-12" SelectedDate="2025-06-12" />"""), new Agenda());
        var selected = await fixture.CompileAsync(Themed("""<Calendar SelectedDate="{Binding Day}" />"""), new Agenda());
        var range = await fixture.CompileAsync(Themed("""<Calendar SelectionMode="SingleRange" m:CalendarPicks.RangeStart="{Binding From}" />"""), new Agenda { From = new DateTime(2025, 6, 9, 0, 0, 0, DateTimeKind.Unspecified) });
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Assert.Contains($"<input type=\"hidden\" name=\"f0\" value=\"{June}|2025-06-12\">", EngineFixture.Body(page, new Agenda()), StringComparison.Ordinal);
        Assert.True(page.Reusable, page.Failure);
        Assert.False(PlanForms.Binds(page.Plan, [new Agenda()], new AdapterSlotResolver()));
        Assert.True(selected.Reusable, selected.Failure);
        Assert.Contains($"value=\"{today}|Month|||2025-06-12\">", EngineFixture.Body(selected, new Agenda()), StringComparison.Ordinal);
        Assert.Contains($"value=\"{today}|Month|||2025-06-03\">", EngineFixture.Body(range, new Agenda { From = new DateTime(2025, 6, 3, 0, 0, 0, DateTimeKind.Unspecified) }), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-245")]
    public async Task CalendarsKeepTheirMonthAcrossPostsAsync()
    {
        var body = Themed("""<Calendar DisplayDate="2025-06-12" /><Button Content="Touch" Command="{Binding TouchCommand}" />""");
        var agenda = new Agenda();

        var next = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", June), ("_btn", "2|"))).Page!, agenda);
        var touched = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", Field(next)), ("_btn", "57|"))).Page!, agenda);
        var back = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", Field(touched)), ("_btn", "0|"))).Page!, agenda);

        Assert.Contains(Caption(7), next, StringComparison.Ordinal);
        Assert.Equal("2025-07-01|Month|||", Field(next));
        Assert.Equal(1, agenda.Touches);
        Assert.Contains(Caption(7), touched, StringComparison.Ordinal);
        Assert.Contains(Caption(6), back, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-246")]
    public async Task ClickedDaysAreSelectedAsOnTheDesktopAsync()
    {
        var body = Themed("""
            <Calendar DisplayDate="2025-06-12" SelectedDate="{Binding Day}">
              <Calendar.BlackoutDates><CalendarDateRange><x:Arguments><sys:DateTime>2025-07-21</sys:DateTime><sys:DateTime>2025-07-21</sys:DateTime></x:Arguments></CalendarDateRange></Calendar.BlackoutDates>
            </Calendar>
            """);
        var agenda = new Agenda();
        const string July = "2025-07-12|Month||";
        var shown = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", July))).Page!, agenda);
        string Id(int day, int index) => Days(shown, day)[index].Groups["id"].Value + "|";

        await fixture.PostAsync(body, agenda, ("f0", July), ("_btn", Id(21, 0)));
        var blackout = agenda.Day;
        await fixture.PostAsync(body, agenda, ("f0", July), ("_btn", Id(18, 0)));
        var clicked = agenda.Day;
        var next = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", July), ("_btn", Id(1, 1)))).Page!, agenda);

        Assert.Equal(new DateTime(2025, 6, 12, 0, 0, 0, DateTimeKind.Unspecified), blackout);
        Assert.Equal(new DateTime(2025, 7, 18, 0, 0, 0, DateTimeKind.Unspecified), clicked);
        Assert.Equal(new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Unspecified), agenda.Day);
        Assert.Contains(Caption(8), next, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-247")]
    public async Task TappedRangesWaitForTheirLastDayAcrossPostsAsync()
    {
        var body = Themed("""
            <Calendar DisplayDate="2025-06-12" SelectionMode="SingleRange" AllowTapRangeSelection="True"
                      m:CalendarPicks.RangeStart="{Binding From}" m:CalendarPicks.RangeEnd="{Binding To}" />
            """);
        var agenda = new Agenda { From = new DateTime(2025, 6, 2, 0, 0, 0, DateTimeKind.Unspecified), To = new DateTime(2025, 6, 3, 0, 0, 0, DateTimeKind.Unspecified) };
        var html = EngineFixture.Body(await fixture.CompileAsync(body, agenda), agenda);
        string Id(int day) => Days(html, day)[0].Groups["id"].Value + "|";

        var first = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", June), ("_btn", Id(9)))).Page!, agenda);
        var started = (agenda.From, agenda.To);
        var last = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", Field(first)), ("_btn", Id(26)))).Page!, agenda);

        Assert.Equal((new DateTime(2025, 6, 9, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 6, 9, 0, 0, 0, DateTimeKind.Unspecified)), started);
        Assert.Equal("2025-06-12|Month|2025-06-09||2025-06-09", Field(first));
        Assert.Equal((new DateTime(2025, 6, 9, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 6, 26, 0, 0, 0, DateTimeKind.Unspecified)), (agenda.From, agenda.To));
        Assert.Equal(June + "|2025-06-09~2025-06-26", Field(last));
    }

    [Fact]
    [Trait("AC", "ENGINE-248")]
    public async Task ControlClicksAddDaysAsync()
    {
        var body = Themed("""<Calendar DisplayDate="2025-06-12" SelectionMode="MultipleRange" m:CalendarPicks.Dates="{Binding Days}" />""");
        static DateTime On(int day) => new(2025, 6, day, 0, 0, 0, DateTimeKind.Unspecified);
        var agenda = new Agenda { Days = [On(3), On(12)] };
        var html = EngineFixture.Body(await fixture.CompileAsync(body, agenda), agenda);
        string Id(int day) => Days(html, day)[0].Groups["id"].Value + "|";

        await fixture.PostAsync(body, agenda, ("f0", June + "c"), ("_btn", Id(24)));
        var added = agenda.Days;
        await fixture.PostAsync(body, agenda, ("f0", June), ("_btn", Id(13)));

        Assert.Equal([On(3), On(12), On(24)], added);
        Assert.Equal([On(13)], agenda.Days);
    }

    [Fact]
    [Trait("AC", "ENGINE-277")]
    public async Task ACalendarKeepsItsSelectionAcrossPostsAsync()
    {
        static string Ctrl(string field)
        {
            var parts = field.Split('|').ToList();
            parts[3] = "c";
            return string.Join('|', parts);
        }

        static string Selected(string html) => string.Join(",", DayPattern().Matches(html).Where(match => match.Groups["selected"].Success).Select(match => match.Groups["day"].Value));

        var several = Themed("""<Calendar DisplayDate="2025-06-12" SelectionMode="MultipleRange" AllowTapRangeSelection="False"><m:CalendarPicks.Dates><m:PickedDays><sys:DateTime>2025-06-03</sys:DateTime><sys:DateTime>2025-06-12</sys:DateTime></m:PickedDays></m:CalendarPicks.Dates></Calendar>""");
        var agenda = new Agenda();
        var start = EngineFixture.Body(await fixture.CompileAsync(several, agenda), agenda);
        string Id(string html, int day) => DayPattern().Matches(html).First(match => match.Groups["day"].Value == day.ToString(CultureInfo.InvariantCulture) && !match.Groups["attributes"].Value.Contains("aria-hidden", StringComparison.Ordinal)).Groups["id"].Value + "|";
        var once = EngineFixture.Body((await fixture.PostAsync(several, agenda, ("f0", Ctrl(Field(start))), ("_btn", Id(start, 24)))).Page!, agenda);
        var twice = EngineFixture.Body((await fixture.PostAsync(several, agenda, ("f0", Ctrl(Field(once))), ("_btn", Id(once, 13)))).Page!, agenda);
        var older = EngineFixture.Body((await fixture.PostAsync(several, agenda, ("f0", June + "c"), ("_btn", Id(start, 24)))).Page!, agenda);

        var single = Themed("""<Calendar DisplayDate="2025-06-12" SelectedDate="2025-06-12" />""");
        var page = EngineFixture.Body(await fixture.CompileAsync(single, agenda), agenda);
        var picked = EngineFixture.Body((await fixture.PostAsync(single, agenda, ("f0", Field(page)), ("_btn", Id(page, 18)))).Page!, agenda);
        var next = EngineFixture.Body((await fixture.PostAsync(single, agenda, ("f0", Field(picked)), ("_btn", "2|"))).Page!, agenda);
        var back = EngineFixture.Body((await fixture.PostAsync(single, agenda, ("f0", Field(next)), ("_btn", "0|"))).Page!, agenda);

        Assert.EndsWith("|2025-06-03,2025-06-12,2025-06-24", Field(once), StringComparison.Ordinal);
        Assert.Equal(("3,12,13,24", "2025-06-03,2025-06-12~2025-06-13,2025-06-24"), (Selected(twice), Field(twice).Split('|')[4]));
        Assert.Equal("3,12,24", Selected(older));
        Assert.Equal("18", Selected(back));
    }

    [Fact]
    [Trait("AC", "ENGINE-249")]
    public async Task TheHeaderShowsTheYearAndAMonthShowsItsDaysAsync()
    {
        var body = Themed("""<Calendar DisplayDate="2025-06-12" />""");
        var agenda = new Agenda();

        var year = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", June), ("_btn", "1|"))).Page!, agenda);
        var september = Regex.Match(year, "value=\"([0-9]+)\\|\"[^>]*>" + CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames[8] + "</button>").Groups[1].Value;
        var month = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", Field(year)), ("_btn", september + "|"))).Page!, agenda);

        Assert.Matches("^2025-06-01\\|Year\\|", Field(year));
        Assert.Matches("<table role=\"grid\" class=\"[^\"]+\"><tbody><tr><td", year);
        Assert.Contains(Caption(9), month, StringComparison.Ordinal);
        Assert.Matches("^2025-09-01\\|Month\\|", Field(month));
    }

    [Fact]
    [Trait("AC", "ENGINE-253")]
    public async Task LiteralDatesGiveWayToTheMonthAPostShowsAsync()
    {
        var body = Themed("""<Calendar DisplayDate="2025-06-12" SelectedDate="2025-06-12" />""");
        var agenda = new Agenda();

        var july = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", June), ("_btn", "2|"))).Page!, agenda);
        var eighteen = Days(july, 18)[0].Groups["id"].Value + "|";
        var answer = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("f0", Field(july)), ("_btn", eighteen))).Page!, agenda);

        Assert.Contains(Caption(7), answer, StringComparison.Ordinal);
        Assert.Equal("2025-07-01|Month|||2025-07-18", Field(answer));
        Assert.True(Days(answer, 18)[0].Groups["selected"].Success);
        Assert.Single(DayPattern().Matches(answer), match => match.Groups["selected"].Success);
    }

    [Fact]
    [Trait("AC", "ENGINE-254")]
    public async Task PresenterRootedButtonsTakeTheirPresentersLookAsync()
    {
        static string Presented(string type, string focused) => $$"""
            <ControlTheme x:Key="{x:Type {{type}}}" TargetType="{{type}}">
              <Setter Property="Background" Value="Transparent" />
              <Setter Property="Foreground" Value="#1c1f23" />
              <Setter Property="Padding" Value="4" />
              <Setter Property="CornerRadius" Value="3" />
              <Setter Property="BorderThickness" Value="1" />
              <Setter Property="BorderBrush" Value="Transparent" />
              <Setter Property="Width" Value="32" />
              <Setter Property="Height" Value="30" />
              <Setter Property="Template">
                <ControlTemplate>
                  <ContentPresenter Name="PART_ContentPresenter" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                                    BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}"
                                    Content="{TemplateBinding Content}" Foreground="{TemplateBinding Foreground}" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" />
                </ControlTemplate>
              </Setter>
              <Style Selector="^:pointerover"><Setter Property="Background" Value="#2e3238" /></Style>
              <Style Selector="^:pressed"><Setter Property="Background" Value="#41464c" /></Style>
              <Style Selector="^:{{focused}}"><Setter Property="BorderBrush" Value="#0064fa" /></Style>
              <Style Selector="^:focus-visible"><Setter Property="Background" Value="#ff0000" /></Style>
            </ControlTheme>
            """;
        string Page(string mode) => Themed($"""
            <Border>
              <Border.Resources>{Presented("CalendarDayButton", "dayfocused")}{Presented("CalendarButton", "btnfocused")}</Border.Resources>
              <Calendar DisplayMode="{mode}" DisplayDate="2025-06-12" SelectedDate="2025-06-12" />
            </Border>
            """);

        var month = await fixture.CompileAsync(Page("Month"));
        var year = await fixture.CompileAsync(Page("Year"));
        var day = Rule(month.Css, Days(EngineFixture.Body(month), 12)[0].Groups["root"].Value);
        var june = Regex.Match(EngineFixture.Body(year), "<button type=\"submit\" name=\"_btn\" value=\"[0-9]+\\|\"[^>]* class=\"([^\"]+)\">" + CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames[5] + "</button>");

        Assert.Contains("padding:4px 4px 4px 4px;", day, StringComparison.Ordinal);
        Assert.Contains("border-radius:3px 3px 3px 3px;", day, StringComparison.Ordinal);
        Assert.Contains("outline:none;", day, StringComparison.Ordinal);
        Assert.Matches(":not\\(:disabled\\):hover\\{[^}]*background:#2e3238!important;", month.Css);
        Assert.Matches(":not\\(:disabled\\):active\\{[^}]*background:#41464c!important;", month.Css);
        Assert.Matches(":not\\(:disabled\\):focus-visible\\{[^}]*border-color:#0064fa!important;", month.Css);
        Assert.DoesNotContain("#ff0000", month.Css, StringComparison.Ordinal);
        Assert.True(june.Success);
        Assert.Contains("padding:4px 4px 4px 4px;", Rule(year.Css, june.Groups[1].Value), StringComparison.Ordinal);
        Assert.Matches(":not\\(:disabled\\):focus-visible\\{[^}]*border-color:#0064fa!important;", year.Css);
        Assert.DoesNotContain("#ff0000", year.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-255")]
    public async Task StarColumnsShareTheMonthsWidthAsync()
    {
        var page = await fixture.CompileAsync(Themed("""
            <Border>
              <Border.Resources>
                <ControlTheme x:Key="{x:Type CalendarItem}" TargetType="CalendarItem">
                  <Setter Property="MinWidth" Value="240" />
                  <Setter Property="DayTitleTemplate"><Template x:DataType="x:String"><TextBlock Text="{Binding}" HorizontalAlignment="Center" VerticalAlignment="Center" /></Template></Setter>
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Grid RowDefinitions="Auto,Auto">
                        <Grid ColumnDefinitions="Auto,*,Auto">
                          <Button Name="PART_PreviousButton" Content="‹" />
                          <Button Name="PART_HeaderButton" Grid.Column="1" />
                          <Button Name="PART_NextButton" Grid.Column="2" Content="›" />
                        </Grid>
                        <Grid Name="PART_MonthView" Grid.Row="1" IsVisible="False" RowDefinitions="32,Auto,Auto,Auto,Auto,Auto,Auto" ColumnDefinitions="*,*,*,*,*,*,*" />
                        <Grid Name="PART_YearView" Grid.Row="1" IsVisible="False" RowDefinitions="Auto,Auto,Auto" ColumnDefinitions="*,*,*,*" />
                      </Grid>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </Border.Resources>
              <Calendar DisplayDate="2025-06-12" />
            </Border>
            """));
        var html = EngineFixture.Body(page);
        var table = Regex.Match(html, "<table role=\"grid\" class=\"([^\"]+)\">").Groups[1].Value;
        var title = Regex.Match(html, "<th scope=\"col\" class=\"([^\"]+)\">").Groups[1].Value;

        Assert.Contains("min-width:100%;", Rule(page.Css, table), StringComparison.Ordinal);
        Assert.Contains("width:14.286%;", Rule(page.Css, title), StringComparison.Ordinal);
        Assert.Contains("height:32px;", Rule(page.Css, title), StringComparison.Ordinal);
    }

    [GeneratedRegex("<td role=\"gridcell\"(?<selected> aria-selected=\"true\")?><div class=\"(?<band>[^\"]+)\"><button type=\"submit\" name=\"_btn\" value=\"(?<id>[0-9]+)\\|\"(?<attributes>[^>]*?) class=\"(?<root>[^\"]+)\">(?<day>[0-9]+)</button></div></td>")]
    private static partial Regex DayPattern();

    [GeneratedRegex("<input type=\"hidden\" name=\"f0\" value=\"([^\"]*)\">")]
    private static partial Regex FieldPattern();
}
