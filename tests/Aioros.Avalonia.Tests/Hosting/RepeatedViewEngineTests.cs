using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Models;
using Aioros.Avalonia.Tests.Views;
using Aioros.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class RepeatedViewEngineTests(EngineFixture fixture)
{
    private const string Key = "Pages/Cards/Page.axaml:0.0.0.0.0";

    private static readonly PageDefinition Cards = EngineFixture.Page("Cards") with
    {
        Views =
        [
            new ViewInstance(Key, 0, new ViewDefinition("Views/Counter.axaml", _ => null) { Type = static () => typeof(Counter) })
            {
                List = new ViewList(0, static model => ((Catalog)model!).Items) { Key = static item => ((Item)item!).Name },
            },
        ],
    };

    private static readonly Catalog Deck = new(new Item("a"), new Item("b"));

    [Fact]
    [Trait("AC", "PRODUCTION-30")]
    public async Task AnswersAreRenderedWithoutCompilingAPlanAsync()
    {
        var models = new object?[] { Deck, Items(new Ledger { Name = "Alpha" }, new Ledger { Name = "Beta" }) };
        var compiled = await fixture.RenderAsync(Cards, "light", models);
        var answered = await fixture.Engine.AnswerAsync(Cards, [.. models], "light", TestContext.Current.CancellationToken);

        Assert.True(compiled.Reusable, compiled.Failure);
        Assert.Equal((false, null), (answered.Reusable, answered.Failure));
        Assert.Equal(EngineFixture.Body(compiled, models), EngineFixture.Body(answered, models));
        Assert.Empty(answered.Css.Split('\n').Except(compiled.Css.Split('\n'), StringComparer.Ordinal));
        Assert.DoesNotContain(answered.Plan.Body, node => node is RepeatNode or ValueNode);
    }

    [Fact]
    [Trait("AC", "ENGINE-53")]
    public async Task EachItemOfAListTakesItsOwnViewModelAsync()
    {
        var page = await fixture.RenderAsync(Cards, "light", Deck, Items(new Ledger { Name = "Alpha" }, new Ledger { Name = "Beta" }));

        var body = EngineFixture.Body(page, Deck, Items(new Ledger { Name = "Gamma" }, new Ledger { Name = "Delta" }));

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains($"<div data-aioros-view=\"{Key}[a]\" style=\"display:contents\">", body, StringComparison.Ordinal);
        Assert.Contains($"<div data-aioros-view=\"{Key}[b]\" style=\"display:contents\">", body, StringComparison.Ordinal);
        Assert.True(body.IndexOf("Gamma", StringComparison.Ordinal) is var gamma && gamma > body.IndexOf($"{Key}[a]", StringComparison.Ordinal) && gamma < body.IndexOf($"{Key}[b]", StringComparison.Ordinal));
        Assert.Contains("Delta", body, StringComparison.Ordinal);
        Assert.Equal(2, body.Split("data-aioros-view=").Length - 1);
        Assert.Contains("Tag a", body, StringComparison.Ordinal);
        Assert.Contains("Tag b", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-54")]
    public async Task APostBindsOnlyItsItemsFieldsAsync()
    {
        var compiled = await fixture.RenderAsync(Cards, "light", Deck, Items(new Ledger(), new Ledger()));
        var everyField = Form(("f1-a.0", "on"), ("f1-a.1", "One text"), ("f1-b.0", "on"), ("f1-b.1", "Two text"));
        foreach (var post in new Func<ImmutableArray<object?>, ImmutableDictionary<string, string>, Task<PostStatus?>>[]
        {
            async (models, form) => await PlanForms.ApplyAsync(compiled.Plan, models, form, new AdapterSlotResolver()),
            async (models, form) => (await fixture.Engine.PostAsync(Cards, models, form, [], "light", TestContext.Current.CancellationToken)).Status,
        })
        {
            var (first, second) = (new Ledger { Flag = true }, new Ledger());
            var status = await post([Deck, Items(first, second)], everyField.Add(PlanForms.ButtonField, "1-b.0|current"));
            var stale = new Ledger();
            var staleStatus = await post([Deck, Items(new Ledger(), stale)], everyField.Add(PlanForms.ButtonField, "1-zz.0|current"));

            Assert.Equal(PostStatus.Rendered, status);
            Assert.Equal((true, "Two text", 1), (second.Flag, second.Name, second.Saved));
            Assert.Equal((true, string.Empty, 0), (first.Flag, first.Name, first.Saved));
            Assert.Equal(PostStatus.Conflict, staleStatus);
            Assert.Equal((false, string.Empty, 0), (stale.Flag, stale.Name, stale.Saved));
        }
    }

    private static ViewItems Items(Ledger first, Ledger second) =>
        new([new ViewItem("a", "a", first), new ViewItem("b", "b", second)], static item => ((Item)item!).Name);

    private static ImmutableDictionary<string, string> Form(params (string Key, string Value)[] fields) =>
        fields.ToImmutableDictionary(field => field.Key, field => field.Value);
}
