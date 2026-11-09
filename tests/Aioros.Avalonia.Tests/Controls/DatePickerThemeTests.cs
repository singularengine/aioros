using System.Globalization;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed partial class DatePickerThemeTests(EngineFixture fixture)
{
    private const string Resources = """<StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/DatePickerTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>""";

    private static string Themed(string picker) => $"<StackPanel>{Resources}{picker}</StackPanel>";

    private static string Rule(string css, string classes) =>
        string.Concat(classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(name => Regex.Match(css, "\\." + name + "\\{([^}]*)\\}").Groups[1].Value));

    private static DateTime On(int month, int day) => new(2025, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    private static string Day(string html, int day) =>
        Regex.Match(html, "<td role=\"gridcell\"[^>]*><div[^>]*><button type=\"submit\" name=\"_btn\" value=\"([0-9]+)\\|\"[^>]*>" + day.ToString(CultureInfo.InvariantCulture) + "</button>").Groups[1].Value + "|";

    private static string Field(string html, string name) => Regex.Match(html, "name=\"(f[0-9]+)\"[^>]*value=\"" + Regex.Escape(name)).Groups[1].Value;

    [Fact]
    [Trait("AC", "RANGES-24")]
    public async Task ThemedPickersAreWrittenThroughTheirTemplateAsync()
    {
        var page = await fixture.CompileAsync($"""
            <StackPanel>
              {Themed("""<CalendarDatePicker Width="240" DisplayDate="2025-06-12" SelectedDate="2025-06-12" />""")}
              <CalendarDatePicker SelectedDate="2025-06-12" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        var popover = Regex.Match(html, "<button type=\"button\" popovertarget=\"([^\"]+)\"");
        var id = popover.Groups[1].Value;

        Assert.True(popover.Success, html);
        Assert.Matches("<div data-aioros-picker class=\"[^\"]+\" style=\"anchor-name:--" + id + "\">", html);
        Assert.Contains("value=\"June 12, 2025\"", html, StringComparison.Ordinal);
        Assert.Matches("<div popover id=\"" + id + "\" role=\"dialog\" class=\"fly\" style=\"position-anchor:--" + id + ";[^\"]*position-area:bottom span-right;[^\"]*margin-top:4px", html);
        Assert.Matches("<div popover id=\"" + id + "\"[^>]*>.*<div data-aioros-calendar[^>]*>.*<table role=\"grid\"", html);
        Assert.Contains("<input type=\"date\"", html, StringComparison.Ordinal);
        Assert.Contains("background:#ffffff;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-29")]
    public async Task ThemedPickersTakeTheirPartsLooksWhenPointedAtAsync()
    {
        var page = await fixture.CompileAsync(Themed("""<CalendarDatePicker Width="240" DisplayDate="2025-06-12" />"""));
        var html = EngineFixture.Body(page);
        var picker = Regex.Match(html, "<div data-aioros-picker class=\"(?<classes>[^\"]+)\"").Groups["classes"].Value.Split(' ');
        var hover = picker.Select(name => Regex.Matches(page.Css, "\\." + name + ":hover (?:\\.(?<part>[a-z0-9]+))?\\{(?<rule>[^}]*)\\}")).SelectMany(matches => matches).ToList();

        Assert.Contains(hover, rule => rule.Groups["rule"].Value.Contains("background:#f5f5f5", StringComparison.Ordinal));
        Assert.Contains(hover, rule => rule.Groups["rule"].Value.Contains("background:#2563eb", StringComparison.Ordinal));
        var parts = hover.Select(rule => rule.Groups["part"].Value).Where(part => part.Length > 0).ToList();
        var focus = picker.SelectMany(name => Regex.Matches(page.Css, "\\." + name + ":focus-within \\.(?<part>[a-z0-9]+)\\{[^}]*border-color:#a3a3a3").Select(match => match.Groups["part"].Value)).ToList();
        Assert.Contains(focus, part => html.Contains(part + " ", StringComparison.Ordinal) || html.Contains(part + "\"", StringComparison.Ordinal));
        Assert.All(parts, part => Assert.True(html.Contains(part + " ", StringComparison.Ordinal) || html.Contains(part + "\"", StringComparison.Ordinal), part));
        Assert.DoesNotContain("#2563eb;}", Regex.Replace(page.Css, ":hover[^{]*\\{[^}]*\\}", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-144")]
    public async Task ATemplateBoundSelectionPostsAsItChangesAsync()
    {
        var html = await fixture.HtmlAsync(Themed("""<CalendarDatePicker Theme="{StaticResource ChoicesPicker}" DisplayDate="2025-06-12" Tag="-1" />"""), new Agenda());

        Assert.Matches("<select name=\"f[0-9]+\"[^>]*data-change", html);
    }

    [Fact]
    [Trait("AC", "RANGES-35")]
    public async Task APostFromAPickersSelectKeepsItOpenAsync()
    {
        var body = Themed("""<CalendarDatePicker Theme="{StaticResource ChoicesPicker}" DisplayDate="2025-06-12" Tag="-1" />""");
        var html = await fixture.HtmlAsync(body, new Agenda());
        var select = System.Text.RegularExpressions.Regex.Match(html, "<select name=\"f([0-9]+)\"").Groups[1].Value;
        var day = System.Text.RegularExpressions.Regex.Match(html, "value=\"([0-9]+)\\|\"[^>]*>18</button>").Groups[1].Value + "|";
        async Task<bool> OpenAsync(params (string, string)[] form)
        {
            var agenda = new Agenda();
            var outcome = await fixture.PostAsync(body, agenda, form);
            return EngineFixture.Body(outcome.Page!, agenda).Contains("data-aioros-open", StringComparison.Ordinal);
        }

        Assert.NotEmpty(select);
        Assert.Equal((true, false), (await OpenAsync(("f" + select, "1"), ("_chg", select)), await OpenAsync(("_btn", day))));
    }

    [Fact]
    [Trait("AC", "RANGES-25")]
    public async Task EmptyPickersShowTheirPlaceholderAsync()
    {
        var page = await fixture.CompileAsync(Themed("""<CalendarDatePicker Width="240" DisplayDate="2025-06-12" />"""));
        var html = EngineFixture.Body(page);

        Assert.Contains("placeholder=\"Pick a date\"", html, StringComparison.Ordinal);
        Assert.Contains("#737373", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-26")]
    public async Task InputPickersAreFieldsWithAnEndButtonAsync()
    {
        var page = await fixture.CompileAsync(Themed("""<CalendarDatePicker Classes="input" Width="240" DisplayDate="2025-06-01" SelectedDate="2025-06-01" />"""));
        var html = EngineFixture.Body(page);
        var id = Regex.Match(html, "<button type=\"button\" popovertarget=\"([^\"]+)\"[^>]*class=\"([^\"]+)\"");

        Assert.NotEmpty(Field(html, "June 01, 2025"));
        Assert.Matches("<input [^>]*name=\"f[0-9]+\"[^>]* data-change[^>]*value=\"June 01, 2025\"|<input [^>]*data-change[^>]*name=\"f[0-9]+\"[^>]*value=\"June 01, 2025\"|<input [^>]*name=\"f[0-9]+\"[^>]*value=\"June 01, 2025\"[^>]* data-change", html);
        Assert.True(id.Success, html);
        Assert.Contains("width:24px;height:24px;", Rule(page.Css, id.Groups[2].Value), StringComparison.Ordinal);
        Assert.Matches("<div popover id=\"" + id.Groups[1].Value + "\"[^>]*style=\"[^\"]*position-area:bottom span-left;[^\"]*margin-top:10px", html);
    }

    [Fact]
    [Trait("AC", "RANGES-27")]
    public async Task BoundPickersRenderLiterallyAsync()
    {
        var page = await fixture.CompileAsync(Themed("""<CalendarDatePicker SelectedDate="{Binding Day}" />"""), new Agenda());

        Assert.Contains("CalendarDatePicker", page.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-256")]
    public async Task ChosenDaysSetThePickersDateAsync()
    {
        var body = Themed("""<CalendarDatePicker Width="240" DisplayDate="2025-06-12" SelectedDate="{Binding Day}" />""");
        var agenda = new Agenda();
        var html = EngineFixture.Body(await fixture.CompileAsync(body, agenda), agenda);

        var answer = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("_btn", Day(html, 18)))).Page!, agenda);

        Assert.Equal(On(6, 18), agenda.Day);
        Assert.Contains("value=\"June 18, 2025\"", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("data-aioros-open", answer, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-257")]
    public async Task MovingThroughMonthsKeepsThePopoverOpenAsync()
    {
        var body = Themed("""<CalendarDatePicker Width="240" DisplayDate="2025-06-12" SelectedDate="{Binding Day}" />""");
        var agenda = new Agenda();
        var html = EngineFixture.Body(await fixture.CompileAsync(body, agenda), agenda);
        var next = Regex.Match(html, "value=\"([0-9]+)\\|\"[^>]*>(?:(?!</button>).)*›").Groups[1].Value + "|";

        var answer = EngineFixture.Body((await fixture.PostAsync(body, agenda, ("_btn", next))).Page!, agenda);

        Assert.Matches("<div popover id=\"[^\"]+\" role=\"dialog\" data-aioros-open", answer);
        Assert.Contains(new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Unspecified).ToString("Y", CultureInfo.CurrentCulture), answer, StringComparison.Ordinal);
        Assert.Equal(On(6, 12), agenda.Day);
    }

    [Fact]
    [Trait("AC", "ENGINE-258")]
    public async Task TypedDatesAreParsedInTheCurrentCultureAsync()
    {
        var body = Themed("""<CalendarDatePicker Classes="input" SelectedDateFormat="Short" Width="240" DisplayDate="2025-06-12" SelectedDate="{Binding Day}" />""");
        var agenda = new Agenda();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        var field = Field(EngineFixture.Body(await fixture.CompileAsync(body, agenda), agenda), "6/12/2025");

        await fixture.PostAsync(body, agenda, (field, "6/20/2025"));
        var english = agenda.Day;
        agenda.Day = On(6, 12);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        await fixture.PostAsync(body, agenda, (field, "20/06/2025"));
        var french = agenda.Day;
        await fixture.PostAsync(body, agenda, (field, "soon"));

        Assert.NotEmpty(field);
        Assert.Equal(On(6, 20), english);
        Assert.Equal(On(6, 20), french);
        Assert.Equal(On(6, 20), agenda.Day);
    }

    [Fact]
    [Trait("AC", "RANGES-28")]
    public async Task LiteralPickersArePlannedAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Themed("""
            <CalendarDatePicker Width="240" DisplayDate="2025-06-12" SelectedDate="2025-06-12" />
            <CalendarDatePicker Classes="input" Width="240" DisplayDate="2025-06-01" />
            """), new Agenda());

        Assert.Contains("<div data-aioros-calendar", EngineFixture.Body(page, new Agenda()), StringComparison.Ordinal);
    }
}
