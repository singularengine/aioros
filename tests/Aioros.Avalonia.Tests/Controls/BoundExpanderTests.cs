using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class BoundExpanderTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "NAVIGATION-97")]
    public async Task TwoWayExpandersArePostedAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/CollapsibleTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
              </StackPanel.Resources>
              <Expander Header="Fluent" IsExpanded="{Binding Open, Mode=TwoWay}"><TextBlock Text="Fluent content" /></Expander>
              <Expander Theme="{StaticResource CosmoCollapsible}" Width="350" IsExpanded="{Binding Open, Mode=TwoWay}">
                <Expander.Header><TextBlock Text="Themed" /></Expander.Header>
                <TextBlock Text="Themed content" />
              </Expander>
              <Expander Header="One way" IsExpanded="{Binding Open, Mode=OneWay}"><TextBlock Text="One way content" /></Expander>
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new ShellModel(true), new ShellModel(false));
        var open = EngineFixture.Body(page, new ShellModel(true));
        var closed = EngineFixture.Body(page, new ShellModel(false));
        var fields = Regex.Matches(open, "<details[^>]* data-change[^>]*><summary[^>]*>.*?</summary><input type=\"checkbox\" hidden name=\"(?<name>f[0-9]+)\" checked>").Select(match => match.Groups["name"].Value).ToList();
        var (planned, plannedOff) = (new ShellModel(false), new ShellModel(true));
        await PlanForms.ApplyAsync(page.Plan, [planned], ImmutableDictionary<string, string>.Empty.Add(fields[0], "on").Add(fields[1], "on"), new AdapterSlotResolver());
        await PlanForms.ApplyAsync(page.Plan, [plannedOff], ImmutableDictionary<string, string>.Empty.Add("_chg", fields[0][1..]), new AdapterSlotResolver());
        var (engine, engineOff) = (new ShellModel(false), new ShellModel(true));
        await fixture.PostAsync(Body, engine, (fields[0], "on"), (fields[1], "on"));
        await fixture.PostAsync(Body, engineOff, ("_chg", fields[1][1..]));

        Assert.Equal(2, fields.Count);
        Assert.Equal(2, Regex.Count(closed, "<input type=\"checkbox\" hidden name=\"f[0-9]+\">"));
        Assert.Equal(2, Regex.Count(open, "data-change"));
        Assert.Equal((true, false), (planned.Open, plannedOff.Open));
        Assert.Equal((true, false, "engine"), (engine.Open, engineOff.Open, "engine"));
    }
}
