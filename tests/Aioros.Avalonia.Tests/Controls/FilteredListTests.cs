using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed partial class FilteredListTests(EngineFixture fixture)
{
    private const string Menu = """
        <m:Finder Classes="outline" Width="450">
          <m:FinderGroup Header="Suggestions">
            <m:FinderItem><StackPanel Orientation="Horizontal"><TextBlock Text="Calendar" /></StackPanel></m:FinderItem>
            <m:FinderItem Content="Search Emoji" />
            <m:FinderItem IsEnabled="False" Content="Calculator" />
          </m:FinderGroup>
          <m:FinderSeparator />
          <m:FinderGroup Header="Settings">
            <m:FinderItem Shortcut="P" TextSearch.Text="Profile settings" Content="Profile" />
            <m:FinderItem Content="Billing" />
          </m:FinderGroup>
        </m:Finder>
        """;

    private static string Themed(string body) => $"""
        <StackPanel Width="500">
          <StackPanel.Resources>
            <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/FinderTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
          </StackPanel.Resources>
          {body}
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ITEMS-110")]
    public async Task FilteredListsAreComboboxesOverAListboxAsync()
    {
        var page = await fixture.CompileAsync(Themed(Menu));
        var html = EngineFixture.Body(page);
        var options = Options().Matches(html).Select(match => match.Groups["class"].Value).ToList();
        var rest = Rule(page.Css, options[0]);

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains("data-aioros-filter role=\"combobox\" aria-autocomplete=\"list\" aria-expanded=\"true\"", html, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "role=\"listbox\""));
        Assert.Equal(2, Regex.Count(html, "role=\"group\""));
        Assert.Equal(5, Regex.Count(html, "role=\"option\""));
        Assert.Contains("role=\"option\" data-value=\"Profile settings\"", html, StringComparison.Ordinal);
        Assert.Matches("<hr class=\"[^\"]+\">", html);
        Assert.Matches("data-aioros-empty hidden class=\"[^\"]+\"><div class=\"[^\"]+\">No results found.</div>", html);
        Assert.Equal(4, options.Count(name => name == options[0]));
        Assert.Contains("color:#0a0a0a", rest, StringComparison.Ordinal);
        Assert.DoesNotContain("background:#f5f5f5", rest, StringComparison.Ordinal);
        Assert.Matches(@":not\(:disabled\):is\(\[aria-selected=true\]\)\{background:#f5f5f5!important;color:#171717!important;\}", page.Css);
        Assert.DoesNotContain("color", Rule(page.Css, Regex.Match(html, "<div class=\"(?<class>[^\"]+)\">Calendar</div>").Groups["class"].Value), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-126")]
    public async Task OptionsAreNotTabStopsAsync()
    {
        var page = await fixture.CompileAsync(Themed(Menu + "<Button Content=\"After\" />"));
        var html = EngineFixture.Body(page);

        Assert.Equal(5, Regex.Count(html, "tabindex=\"-1\" role=\"option\""));
        Assert.Equal(["After"], await BrowserLayout.TabsAsync(page, "[data-aioros-filter]", 1));
    }

    [Fact]
    [Trait("AC", "ITEMS-112")]
    public async Task ComboboxesListTheirItemsAndPostTheChoiceAsync()
    {
        var body = Themed("""<m:Picker PlaceholderText="Select framework..." Empty="No framework found." ItemsSource="{Binding Names}" SelectedItem="{Binding Chosen}" />""");
        var choice = new Choice();

        var page = await fixture.CompileAsync(body, choice);
        var html = EngineFixture.Body(page, choice);
        var popover = html[html.IndexOf("<div popover", StringComparison.Ordinal)..];
        var checks = Regex.Matches(popover, "role=\"option\" data-aioros-chooses=\"[0-9]+\" aria-checked=\"(?<checked>true|false)\" class=\"[^\"]+\"><div class=\"[^\"]+\"><svg viewBox=\"[^\"]+\" preserveAspectRatio=\"[^\"]+\" class=\"(?<check>[^\"]+)\"").Select(match => match.Groups["checked"].Value).ToList();
        await fixture.PostAsync(body, choice, ("f0", "sv"), ("_btn", "1|"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains("data-aioros-filter role=\"combobox\"", popover, StringComparison.Ordinal);
        Assert.Equal(["Next.js", "SvelteKit", "Remix"], Regex.Matches(popover, ">([^<>]+)</div></div></div></button>").Select(match => match.Groups[1].Value));
        Assert.Equal(3, checks.Count);
        Assert.Equal(["false", "false", "true"], checks);
        Assert.Equal("SvelteKit", choice.Chosen);
    }

    private const string Frameworks = """
        <m:Picker PlaceholderText="Select framework..." SelectedIndex="1">
          <sys:String>Next.js</sys:String>
          <sys:String>SvelteKit</sys:String>
          <sys:String>Remix</sys:String>
        </m:Picker>
        """;

    [Fact]
    [Trait("AC", "ITEMS-122")]
    public async Task ChoosingListsAreWrittenForTheBrowserAsync()
    {
        var page = await fixture.CompileAsync(Themed(Frameworks), new Choice());
        var html = EngineFixture.Body(page, new Choice());
        var bound = await fixture.HtmlAsync(Themed("""<m:Picker PlaceholderText="Select framework..." ItemsSource="{Binding Names}" SelectedItem="{Binding Chosen}" />"""), new Choice());
        var choice = Regex.Match(html, "<div data-aioros-choice data-aioros-toggle class=\"[^\"]+\">");
        var field = Regex.Match(html, "<input type=\"hidden\" name=\"f(?<id>[0-9]+)\" value=\"1\">");
        var templates = Regex.Matches(html, "<template data-aioros-value=\"(?<value>-?[0-9]+)\">(?<body>.*?)</template>").ToDictionary(match => match.Groups["value"].Value, match => match.Groups["body"].Value);
        var options = Regex.Matches(html, "role=\"option\" data-aioros-chooses=\"(?<index>[0-9])\" aria-checked=\"(?<checked>true|false)\" class=\"(?<class>[^\"]+)\"").ToList();
        var shown = Regex.Match(html, "<div data-aioros-shows class=\"[^\"]+\">(?:(?!</button>).)*").Value;

        Assert.True(page.Reusable, page.Failure);
        Assert.True(choice.Success, html);
        Assert.True(field.Success, html);
        Assert.Equal(["-1", "0", "1", "2"], templates.Keys);
        Assert.All(templates.Values, body => Assert.StartsWith("<div data-aioros-shows class=", body, StringComparison.Ordinal));
        Assert.Contains(">Select framework...<", templates["-1"], StringComparison.Ordinal);
        Assert.Equal(["Next.js", "SvelteKit", "Remix"], templates.Where(entry => entry.Key != "-1").Select(entry => Regex.Match(entry.Value, ">([^<>]+)<").Groups[1].Value));
        Assert.Contains(">SvelteKit<", shown, StringComparison.Ordinal);
        Assert.True(options.Count > 0, html);
        Assert.Equal(["0:false", "1:true", "2:false"], options.Select(option => option.Groups["index"].Value + ":" + option.Groups["checked"].Value));
        Assert.Single(options.Select(option => option.Groups["class"].Value).Distinct());
        Assert.Matches(@"\[aria-checked=true\] \.d[0-9a-f]+\{opacity:1!important;\}", page.Css);
        Assert.Matches("<input type=\"hidden\" name=\"f[0-9]+\" value=\"2\" disabled data-change>", bound);
    }

    [Fact]
    [Trait("AC", "ITEMS-123")]
    public async Task ChoosingListsBindTheIndexAPostCarriesAsync()
    {
        var html = await fixture.HtmlAsync(Themed(Frameworks), new Choice());
        var field = "f" + Regex.Match(html, "<input type=\"hidden\" name=\"f(?<id>[0-9]+)\" value=").Groups["id"].Value;
        var body = Themed("""<m:Picker PlaceholderText="Select framework..." ItemsSource="{Binding Names}" SelectedItem="{Binding Chosen}" />""");
        var boundField = "f" + Regex.Match(await fixture.HtmlAsync(body, new Choice()), "<input type=\"hidden\" name=\"f(?<id>[0-9]+)\" value=").Groups["id"].Value;
        var model = new Choice();

        var third = await fixture.PostAsync(Themed(Frameworks), new Choice(), (field, "2"), ("_chg", field[1..]));
        var none = await fixture.PostAsync(Themed(Frameworks), new Choice(), (field, "-1"), ("_chg", field[1..]));
        var wide = await fixture.PostAsync(Themed(Frameworks), new Choice(), (field, "7"), ("_chg", field[1..]));
        var text = await fixture.PostAsync(Themed(Frameworks), new Choice(), (field, "x"), ("_chg", field[1..]));
        await fixture.PostAsync(body, model, (boundField, "1"), ("_chg", boundField[1..]));

        Assert.Equal("Remix", Shown(third));
        Assert.Equal("Select framework...", Shown(none));
        Assert.Equal("SvelteKit", Shown(wide));
        Assert.Equal("SvelteKit", Shown(text));
        Assert.Equal("SvelteKit", model.Chosen);
    }

    private static string Shown(PostOutcome outcome) =>
        Regex.Match(EngineFixture.Body(outcome.Page!), "<div data-aioros-shows class=\"[^\"]+\">(?:<[^>]+>)*(?<text>[^<]+)<").Groups["text"].Value;

    private static string Rule(string css, string name) => Regex.Match(css, $@"\.{name}\{{(?<rule>[^}}]*)\}}").Groups["rule"].Value;

    [GeneratedRegex("<button type=\"submit\" name=\"_btn\" value=\"[0-9]+\\|\" tabindex=\"-1\" role=\"option\"(?: data-value=\"[^\"]*\")? class=\"(?:s[0-9a-f]+ )?(?<class>k[0-9a-f]+)\">")]
    private static partial Regex Options();
}
