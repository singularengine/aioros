using System.Globalization;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class CalendarPlanTests(EngineFixture fixture)
{
    private const string Resources = """<StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/CalendarTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>""";

    private static string Themed(string calendar) => $"""<StackPanel>{Resources}{calendar}</StackPanel>""";

    private static DateTime On(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    [Trait("AC", "RANGES-36")]
    public async Task BoundCalendarsArePlannedForEveryMonthAndDayAsync()
    {
        var today = DateTime.Today;
        await fixture.PlansLikeAvaloniaAsync(
            Themed("""<Calendar DisplayDate="{Binding Shown}" SelectedDate="{Binding Day}" />"""),
            new Planner(On(2025, 6, 12), On(2025, 6, 12)),
            new Planner(On(2026, 2, 10), On(2026, 1, 27)),
            new Planner(On(2024, 12, 1)),
            new Planner(today, today));
        var planned = await fixture.PlansLikeAvaloniaAsync(Themed("""<Calendar SelectedDate="{Binding Day}" />"""), new Planner(today, today.AddDays(-40)), new Planner(today, today));

        Assert.Contains(">" + today.ToString("Y", CultureInfo.CurrentCulture) + "<", EngineFixture.Body(planned, new Planner(today)), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-37")]
    public async Task BoundRangesAndDaysArePlannedWithTheirLibrarysMarksAsync()
    {
        static Planner Range(DateTime shown, DateTime? from, DateTime? to) => new(shown) { From = from, To = to };
        static Planner Days(DateTime shown, params DateTime[] days) => new(shown) { Days = days };

        await fixture.PlansLikeAvaloniaAsync(
            Themed("""<Calendar DisplayDate="{Binding Shown}" SelectionMode="SingleRange" m:CalendarPicks.RangeStart="{Binding From}" m:CalendarPicks.RangeEnd="{Binding To}" />"""),
            Range(On(2025, 6, 12), On(2025, 6, 9), On(2025, 6, 26)),
            Range(On(2025, 6, 12), On(2025, 6, 10), On(2025, 6, 12)),
            Range(On(2025, 6, 12), On(2025, 5, 20), On(2025, 7, 3)),
            Range(On(2025, 6, 12), On(2025, 6, 1), On(2025, 6, 14)),
            Range(On(2025, 6, 12), On(2025, 6, 15), On(2025, 6, 15)),
            Range(On(2025, 6, 12), On(2025, 6, 18), null),
            Range(On(2026, 3, 1), null, null),
            Range(On(2026, 3, 1), On(2026, 2, 27), On(2026, 3, 8)));
        await fixture.PlansLikeAvaloniaAsync(
            Themed("""<Calendar DisplayDate="{Binding Shown}" SelectionMode="MultipleRange" m:CalendarPicks.Dates="{Binding Days}" />"""),
            Days(On(2025, 6, 12), On(2025, 6, 3), On(2025, 6, 12)),
            Days(On(2025, 6, 12), On(2025, 6, 12), On(2025, 6, 13), On(2025, 6, 14), On(2025, 7, 1)),
            Days(On(2025, 6, 12)),
            Days(On(2025, 11, 2), On(2025, 10, 31), On(2025, 11, 30)));
    }

    [Fact]
    [Trait("AC", "RANGES-38")]
    public async Task BoundCalendarsArePlannedInEachCultureWithTheirWeekNumbersAsync()
    {
        var xaml = Themed("""<Calendar DisplayDate="{Binding Shown}" SelectedDate="{Binding Day}" m:CalendarWeeks.Shows="True" />""");
        var (culture, ui) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            foreach (var (name, caption) in new[] { ("en-US", "January 2026"), ("fr-FR", "janvier 2026") })
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                var planned = await fixture.PlansLikeAvaloniaAsync(
                    xaml,
                    new Planner(On(2025, 12, 10), On(2025, 12, 24)),
                    new Planner(On(2026, 1, 8)),
                    new Planner(On(2020, 12, 1)),
                    new Planner(On(2021, 1, 4), On(2021, 1, 4)));
                var january = new Planner(On(2026, 1, 8));
                var body = EngineFixture.Body(planned, january);
                var desktop = Numbers((await fixture.CaptureInAsync(xaml, new CaptureOptions("light", 400, 500), january)).Texts);
                var web = Numbers(await BrowserLayout.TextsAsync(planned, body, 400, 500));

                Assert.Contains(">" + caption + "<", body, StringComparison.Ordinal);
                Assert.Equal(desktop.Select(text => text.Text), web.Select(text => text.Text));
                Assert.All(desktop.Zip(web), pair => Assert.InRange(pair.Second.X, pair.First.X - 2, pair.First.X + 2));
            }
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, ui);
        }
    }

    /// <summary>The numbers laid out, days and weeks, row by row from the left, with their x.</summary>
    private static List<(string Text, double X)> Numbers(System.Collections.Immutable.ImmutableArray<LaidOutText> texts) =>
        [.. texts.Where(static text => int.TryParse(text.Text, CultureInfo.InvariantCulture, out _)).OrderBy(static text => Math.Round(text.Y / 8)).ThenBy(static text => text.X).Select(static text => (text.Text, text.X))];

    [Fact]
    [Trait("AC", "RANGES-39")]
    public async Task BoundCalendarsArePlannedWithTheirCaptionSelectsAsync()
    {
        var year = DateTime.Today.Year;
        await fixture.PlansLikeAvaloniaAsync(
            Themed("""<Calendar Theme="{StaticResource CaptionCalendar}" DisplayDate="{Binding Shown}" SelectedDate="{Binding Day}" />"""),
            new Planner(On(2025, 6, 12), On(2025, 6, 12)),
            new Planner(On(year, 1, 5)),
            new Planner(On(year - 120, 3, 9)),
            new Planner(On(year + 3, 11, 30)));
        await fixture.PlansLikeAvaloniaAsync(
            Themed("""<Calendar Theme="{StaticResource CaptionCalendar}" Classes="plain" DisplayDate="{Binding Shown}" SelectedDate="{Binding Day}" />"""),
            new Planner(On(2025, 6, 12), On(2025, 6, 12)),
            new Planner(On(year - 120, 3, 9)));
    }

    [Fact]
    [Trait("AC", "ENGINE-278")]
    public async Task BoundCalendarsThePlanCannotFollowRenderLiterallyAsync()
    {
        var model = new Planner(On(2025, 6, 12), On(2025, 6, 12));
        var parity = await fixture.CompileAsync(Themed("""<Calendar DisplayDate="{Binding Shown}" SelectedDate="{Binding Day}" m:CalendarParity.Bolds="True" />"""), model);
        var ranged = await fixture.CompileAsync(Themed("""<Calendar DisplayDate="{Binding Shown}" SelectedDate="{Binding Day}" DisplayDateStart="2025-01-01" />"""), model);

        Assert.False(parity.Reusable);
        Assert.StartsWith("a bound Calendar whose days' looks depend on more than their state", parity.Failure, StringComparison.Ordinal);
        Assert.Equal("a bound Calendar with blackout days or a display range", ranged.Failure);
        Assert.Contains(">13</button>", EngineFixture.Body(parity, model), StringComparison.Ordinal);
    }
}
