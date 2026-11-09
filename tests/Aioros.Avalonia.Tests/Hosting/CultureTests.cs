using System.Globalization;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class CultureTests(EngineFixture fixture)
{
    private const string Calendar = """
        <StackPanel>
          <StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/CalendarTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>
          <Calendar DisplayDate="2025-06-12" />
          <TextBlock Text="{Binding When, StringFormat={}{0:D}}" />
        </StackPanel>
        """;

    private const string Resources = """<StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/CalendarTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>""";

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private static void Use(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    [Fact]
    [Trait("AC", "ENGINE-250")]
    public async Task PagesRenderInTheCultureOfTheirCallerAsync()
    {
        Use(French);
        var french = await fixture.HtmlAsync(Calendar, new Diary());
        Use(English);
        var english = await fixture.HtmlAsync(Calendar, new Diary());
        Use(CultureInfo.InvariantCulture);
        var invariant = await fixture.HtmlAsync(Calendar, new Diary());

        Assert.Contains("juin 2025", french, StringComparison.Ordinal);
        Assert.Contains(">jeudi 12 juin 2025<", french, StringComparison.Ordinal);
        Assert.Contains(">" + French.DateTimeFormat.ShortestDayNames[1] + "<", french, StringComparison.Ordinal);
        Assert.Contains("June 2025", english, StringComparison.Ordinal);
        Assert.Contains(">Thursday, June 12, 2025<", english, StringComparison.Ordinal);
        Assert.Contains("2025 June", invariant, StringComparison.Ordinal);
        Assert.Contains(">Thursday, 12 June 2025<", invariant, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-251")]
    public async Task PlansAreCompiledInTheCultureOfTheirCallerAsync()
    {
        Use(French);

        var page = await fixture.PlansLikeAvaloniaAsync(Calendar, new Diary());
        var html = EngineFixture.Body(page, new Diary());

        Assert.Contains(">jeudi 12 juin 2025<", html, StringComparison.Ordinal);
        Assert.Contains(">" + French.DateTimeFormat.ShortestDayNames[1] + "<", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-252")]
    public async Task PostsRunInTheCultureOfTheirCallerAsync()
    {
        var diary = new Diary();
        Use(French);

        var outcome = await fixture.PostAsync("""<StackPanel><Button Content="Note" Command="{Binding NoteCommand}" /><TextBlock Text="{Binding Noted}" /></StackPanel>""", diary, ("_btn", "0|"));

        Assert.Equal("fr-FR jeudi 12 juin 2025", diary.Noted);
        Assert.Contains(">fr-FR jeudi 12 juin 2025<", EngineFixture.Body(outcome.Page!, diary), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-223")]
    public async Task WeeksStartOnTheDayOfTheRenderedCultureAsync()
    {
        const string Weeks = "<StackPanel>" + Resources + """<Calendar DisplayDate="2025-06-12" /><Calendar DisplayDate="2025-06-12" FirstDayOfWeek="Wednesday" /></StackPanel>""";
        static string[] Firsts(string html) => [.. Regex.Matches(html, "<thead><tr><th scope=\"col\"[^>]*>(.*?)</th>").Select(match => Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty))];

        Use(French);
        var french = await fixture.HtmlAsync(Weeks, new Diary());
        Use(English);
        var english = await fixture.HtmlAsync(Weeks, new Diary());

        Assert.Equal([French.DateTimeFormat.ShortestDayNames[1], French.DateTimeFormat.ShortestDayNames[3]], Firsts(french));
        Assert.Equal(["Su", "We"], Firsts(english));
    }

    [Fact]
    [Trait("AC", "SSR-224")]
    public async Task NothingOfACultureIsKeptForTheNextAsync()
    {
        const string Page = "<StackPanel>" + Resources + """
            <TextBlock Text="{Binding When, StringFormat={}{0:D}}" />
            <TextBlock Text="{Binding Amount, StringFormat={}{0:N2}}" />
            <Calendar DisplayDate="2025-06-12" />
            <CalendarDatePicker SelectedDate="2025-06-12" />
            </StackPanel>
            """;

        Use(English);
        var before = await fixture.HtmlAsync(Page, new Diary());
        Use(French);
        var french = await fixture.HtmlAsync(Page, new Diary());
        Use(English);
        var after = await fixture.HtmlAsync(Page, new Diary());

        Assert.Equal(before, after);
        Assert.Contains(">Thursday, June 12, 2025<", after, StringComparison.Ordinal);
        Assert.Contains(">jeudi 12 juin 2025<", french, StringComparison.Ordinal);
        Assert.Contains(">" + 1234.5.ToString("N2", French) + "<", french, StringComparison.Ordinal);
    }
}
