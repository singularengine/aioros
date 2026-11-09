using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Models;
using Aioros.Avalonia.Tests.Views;
using Aioros.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class ViewFormTests(EngineFixture fixture)
{
    private static readonly PageDefinition Twice = EngineFixture.Page("Twice") with
    {
        Views =
        [
            new ViewInstance("Pages/Twice/Page.axaml:0.2", 0, new ViewDefinition("Views/Counter.axaml", _ => null) { Type = static () => typeof(Counter) }),
            new ViewInstance("Pages/Twice/Page.axaml:0.3", 0, new ViewDefinition("Views/Counter.axaml", _ => null) { Type = static () => typeof(Counter) }),
        ],
    };

    [Fact]
    [Trait("AC", "ENGINE-51")]
    public async Task FieldsAreNamedPerViewInstanceAsync()
    {
        var page = await fixture.RenderAsync(Twice, "light", new Ledger(), new Ledger(), new Ledger());

        var body = EngineFixture.Body(page, new Ledger(), new Ledger(), new Ledger());

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(["f0", "f1", "f1.0", "f1.1", "f2.0", "f2.1"], Names(body));
        Assert.Equal(["1.0|current", "2.0|current", "0|current"], Buttons(body));
    }

    [Fact]
    [Trait("AC", "ENGINE-52")]
    public async Task APostBindsOnlyItsViewsFieldsAsync()
    {
        var compiled = await fixture.RenderAsync(Twice, "light", new Ledger(), new Ledger(), new Ledger());
        var everyField = Form(("f1", "Page text"), ("f1.1", "One text"), ("f2.0", "on"), ("f2.1", "Two text"));
        foreach (var post in new Func<ImmutableArray<object?>, ImmutableDictionary<string, string>, Task<PostStatus?>>[]
        {
            async (models, form) => await PlanForms.ApplyAsync(compiled.Plan, models, form, new AdapterSlotResolver()),
            async (models, form) => (await fixture.Engine.PostAsync(Twice, models, form, [], "light", TestContext.Current.CancellationToken)).Status,
        })
        {
            var (host, first, second) = (new Ledger { Flag = true }, new Ledger { Flag = true }, new Ledger());
            var viewStatus = await post([host, first, second], everyField.Add(PlanForms.ButtonField, "2.0|current"));
            var (pageHost, pageView) = (new Ledger { Flag = true }, new Ledger());
            var pageStatus = await post([pageHost, new Ledger(), pageView], everyField.Add("f0", "on").Add(PlanForms.ButtonField, "0|current"));
            var stale = new Ledger();
            var staleStatus = await post([new Ledger(), new Ledger(), stale], everyField.Add(PlanForms.ButtonField, "2.9|current"));

            Assert.Equal(PostStatus.Rendered, viewStatus);
            Assert.Equal((true, "Two text", 1), (second.Flag, second.Name, second.Saved));
            Assert.Equal((true, string.Empty, 0), (first.Flag, first.Name, first.Saved));
            Assert.Equal((true, string.Empty, 0), (host.Flag, host.Name, host.Saved));
            Assert.Equal(PostStatus.Rendered, pageStatus);
            Assert.Equal((true, "Page text", 1), (pageHost.Flag, pageHost.Name, pageHost.Saved));
            Assert.Equal((false, string.Empty, 0), (pageView.Flag, pageView.Name, pageView.Saved));
            Assert.Equal(PostStatus.Conflict, staleStatus);
            Assert.Equal((false, string.Empty, 0), (stale.Flag, stale.Name, stale.Saved));
        }
    }

    [Fact]
    [Trait("AC", "ENGINE-207")]
    public async Task AButtonWithAnotherParameterIsRefusedBeforeBindingAsync()
    {
        var compiled = await fixture.RenderAsync(Twice, "light", new Ledger(), new Ledger(), new Ledger());
        var forged = Form(("f1", "Page text"), ("f1.1", "One text"), ("f2.0", "on"), ("f2.1", "Two text"), (PlanForms.ButtonField, "2.0|forged"));
        var planned = new Ledger();
        var engine = new Ledger();

        var plannedStatus = await PlanForms.ApplyAsync(compiled.Plan, [new Ledger(), new Ledger(), planned], forged, new AdapterSlotResolver());
        var engineStatus = (await fixture.Engine.PostAsync(Twice, [new Ledger(), new Ledger(), engine], forged, [], "light", TestContext.Current.CancellationToken)).Status;

        Assert.Equal((PostStatus.Conflict, PostStatus.Conflict), (plannedStatus, engineStatus));
        Assert.Equal((false, string.Empty, 0), (planned.Flag, planned.Name, planned.Saved));
        Assert.Equal((false, string.Empty, 0), (engine.Flag, engine.Name, engine.Saved));
    }

    private static ImmutableDictionary<string, string> Form(params (string Key, string Value)[] fields) =>
        fields.ToImmutableDictionary(field => field.Key, field => field.Value);

    private static List<string> Names(string body) =>
        [.. System.Text.RegularExpressions.Regex.Matches(body, "name=\"(f[^\"]*)\"").Select(match => match.Groups[1].Value)];

    private static List<string> Buttons(string body) =>
        [.. System.Text.RegularExpressions.Regex.Matches(body, "name=\"_btn\" value=\"([^\"]*)\"").Select(match => match.Groups[1].Value)];
}
