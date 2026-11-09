using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed partial class LibraryCommandTests(EngineFixture fixture)
{
    private static string Themed(string body) => $"""
        <StackPanel>
          <StackPanel.Resources>
            <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/ConfirmTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
          </StackPanel.Resources>
          {body}
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ENGINE-191")]
    public async Task LibraryCommandsAreLearnedByTheirStateAsync()
    {
        var body = Themed("""<m:Confirm ConfirmCommand="{Binding Save}" ConfirmCommandParameter="{Binding Label}"><Button Content="Save" /></m:Confirm>""");
        var planned = await fixture.PlansLikeAvaloniaAsync(body, new Guarded("Ann", new Gate(true)), new Guarded("Bo", new Gate(true)));
        var self = await fixture.CompileAsync(Themed("""<m:Confirm ConfirmCommand="{Binding Save}" Tag="x" ConfirmCommandParameter="{Binding $self.Tag}" />"""), new Guarded("Ann", new Gate(true)));

        Assert.Contains(">ready<", EngineFixture.Body(planned, new Guarded("Bo", new Gate(true))), StringComparison.Ordinal);
        Assert.Throws<PlanMissException>(() => EngineFixture.Body(planned, new Guarded("Ann", new Gate(false))));
        Assert.Throws<PlanMissException>(() => EngineFixture.Body(planned, new Guarded("stop", new Gate(true))));
        Assert.Throws<PlanMissException>(() => EngineFixture.Body(planned, new Guarded("Ann", null)));
        Assert.False(self.Reusable);
        Assert.Contains("Confirm.ConfirmCommandParameter", self.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-192")]
    public async Task LibraryCommandsPostThroughTheEngineAsync()
    {
        var body = Themed("""<TextBox Text="{Binding Label}" /><m:Confirm ConfirmCommand="{Binding Save}" ConfirmCommandParameter="{Binding Label}"><Button Content="Save" /></m:Confirm>""");
        var compiled = new Guarded("Ann", new Gate(true));
        var page = await fixture.CompileAsync(body, compiled);
        var html = EngineFixture.Body(page, compiled);
        var name = TextField().Match(html).Groups[1].Value;
        var fields = PlanForms.Fields(page.Plan, [compiled]);
        var saved = new Guarded("Ann", new Gate(true));
        var confirmed = new Guarded("Ann", new Gate(true));

        var planPost = await PlanForms.ApplyAsync(page.Plan, [new Guarded("Ann", new Gate(true))], System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add(PlanForms.ButtonField, Button("Save").Match(html).Groups[1].Value), new AdapterSlotResolver());
        await fixture.PostAsync(body, saved, (name, "Cy"), (PlanForms.ButtonField, Button("Save").Match(html).Groups[1].Value));
        await fixture.PostAsync(body, confirmed, (name, "Di"), (PlanForms.ButtonField, Button("OK").Match(html).Groups[1].Value));

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains(fields.Fields, field => field.Button && field.Field is null);
        Assert.Single(fields.Fields, field => field.Field is not null);
        Assert.Null(planPost);
        Assert.Equal(["Cy"], saved.Save!.Ran);
        Assert.Equal(["Di"], confirmed.Save!.Ran);
    }

    private static Regex Button(string content) => new($"value=\"(\\d+\\|)\"[^>]*>{content}</button>");

    [GeneratedRegex("name=\"(f\\d+)\"")]
    private static partial Regex TextField();
}
