using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed partial class LocalProofTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "SSR-311")]
    public async Task AnInputOutsideTheGuaranteeNoVerificationProvedIsAnsweredByTheEngineAsync()
    {
        var engine = new CountingEngine(fixture.Engine);
        var page = PageServer.Page("/unproven", "LocalsUnproven", static _ => new ParityModel());
        await using var app = await PageServer.StartPlannedAsync(fixture, engine, page);
        using var client = Microsoft.AspNetCore.TestHost.HostBuilderTestServerExtensions.GetTestClient(app);

        var written = await client.GetStringAsync(new Uri("/unproven", UriKind.Relative), TestContext.Current.CancellationToken);
        var form = PostedForms.Of(written).SetItem("f0", "x").SetItem(PlanForms.ChangeField, "0");
        var before = engine.Posts;
        var answer = await SendAsync(client, form, "/unproven");
        var avalonia = EngineFixture.Body((await fixture.Engine.PostAsync(page, [new ParityModel()], form, [], "light", TestContext.Current.CancellationToken)).Page!, new ParityModel());

        Assert.Equal(1, engine.Posts - before);
        Assert.Equal(Input().Match(avalonia).Value, Input().Match(answer).Value);
    }

    [Fact]
    [Trait("AC", "SSR-312")]
    public async Task VerificationProvesTheInputsOutsideTheGuaranteeWhoseDomainsAgreeAsync()
    {
        var engine = new CountingEngine(fixture.Engine);
        var mixed = PageServer.Page("/mixed", "LocalsMixed", static _ => new ParityModel());
        var proven = PageServer.Page("/proven", "LocalsProven", static _ => new ParityModel());
        await using var app = await PageServer.StartPlannedAsync(fixture, engine, mixed, proven);
        using var scope = app.Services.CreateScope();
        var handler = app.Services.GetRequiredService<PageHandler>();

        var outcomes = await Warmup.RunAsync([mixed, proven], scope.ServiceProvider, handler, posts: false, verify: true, TestContext.Current.CancellationToken);
        var report = PrecompileReport.Of(outcomes, []).Text;
        var saved = PlansFile.Read(System.Text.Encoding.UTF8.GetBytes(PlansFile.Write([new PlannedPage("/proven", "light", outcomes[1].Entries[0].Entry.Page) { ProvenFields = [.. handler.Plans.ProvenFields("/proven")] }]).Json), new Plans.AdapterSlotResolver()).Single();
        using var client = Microsoft.AspNetCore.TestHost.HostBuilderTestServerExtensions.GetTestClient(app);
        var written = await client.GetStringAsync(new Uri("/proven", UriKind.Relative), TestContext.Current.CancellationToken);
        var before = engine.Posts;
        var on = PostedForms.Fields(written).Single(static field => field.Name == "f0").Values[0];
        var answer = await SendAsync(client, PostedForms.Of(written).SetItem("f0", on).SetItem(PlanForms.ChangeField, "0"), "/proven");

        Assert.Equal(["f0"], outcomes[0].Proof.Proven);
        Assert.Equal(["f1"], outcomes[0].Proof.Failures.Select(static failure => failure.Field).Distinct());
        Assert.Contains(outcomes[0].Proof.Failures, static failure => failure is { Value: "x", Theme: "light" } && failure.Reason.StartsWith("plan: ", StringComparison.Ordinal));
        Assert.Contains("(the engine answers the page's posts: inputs outside the guarantee the plan renders otherwise: f1 (text) x in light: plan: ", report, StringComparison.Ordinal);
        Assert.Equal(["f0"], outcomes[1].Proof.Proven);
        Assert.Equal(["f0"], saved.ProvenFields);
        Assert.Equal(0, engine.Posts - before);
        Assert.Contains("Bold", answer, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-313")]
    public async Task AValueThePlanNeverLearnedFailsItsFieldNotThePageAsync()
    {
        var page = PageServer.Page("/learned", "LearnedPost", static _ => new Profile());
        await using var app = await PageServer.StartPlannedAsync(fixture, fixture.Engine, page);
        using var scope = app.Services.CreateScope();
        var handler = app.Services.GetRequiredService<PageHandler>();

        var outcomes = await Warmup.RunAsync([page], scope.ServiceProvider, handler, posts: false, verify: true, TestContext.Current.CancellationToken);
        var report = PrecompileReport.Of(outcomes, []);

        Assert.False(report.Failed, report.Text);
        Assert.Contains(outcomes[0].Diverging, static verdict => verdict.Field.Name == "f0" && verdict.Difference?.StartsWith("the plan does not hold the value", StringComparison.Ordinal) == true);
        Assert.Contains("f0", handler.Plans.EngineFields("/learned"));
    }

    [Fact]
    [Trait("AC", "SSR-314")]
    public async Task LocalsAreNotBoundForModelsThePlanCannotWriteAsync()
    {
        const string Body = """<StackPanel><CheckBox Content="Remember" /><PathIcon Width="16" Height="16" Data="{Binding Name, Converter={x:Static m:NameIcons.Instance}}" /></StackPanel>""";
        var page = await fixture.CompileAsync(Body, new Profile());
        var form = PostedForms.Of(EngineFixture.Body(page, new Profile())).SetItem("f0", "on");

        using (LocalValues.Use())
        {
            var bound = true;
            var error = Record.Exception(() => bound = PlanForms.BindLocals(page.Plan, [new Profile { Name = "never learned" }], form, new Plans.AdapterSlotResolver()));

            Assert.Null(error);
            Assert.False(bound);
            Assert.False(LocalValues.TryGet(0, out _));
        }

        var renamer = PageServer.Page("/renamed", "LearnedCommand", static _ => new Renamer());
        await using var app = await PageServer.StartPlannedAsync(fixture, fixture.Engine, renamer);
        using var client = Microsoft.AspNetCore.TestHost.HostBuilderTestServerExtensions.GetTestClient(app);
        var written = await client.GetStringAsync(new Uri("/renamed", UriKind.Relative), TestContext.Current.CancellationToken);
        var answer = await SendAsync(client, PostedForms.Of(written).SetItem("f0", "on").SetItem("_btn", Button().Match(written).Groups[1].Value), "/renamed");

        var planCss = Stylesheet().Matches(written).Select(static match => match.Value).ToList();

        Assert.DoesNotContain(PageServer.ErrorTitle, answer, StringComparison.Ordinal);
        Assert.Contains(">Renamed<", answer, StringComparison.Ordinal);
        Assert.NotEmpty(planCss);
        Assert.All(planCss, rule => Assert.Contains(rule, answer, StringComparison.Ordinal));
    }

    [GeneratedRegex("\\.k[0-9a-f]{8}\\{[^}]*\\}")]
    private static partial Regex Stylesheet();

    [GeneratedRegex("name=\"_btn\" value=\"([^\"]*)\"")]
    private static partial Regex Button();

    private static async Task<string> SendAsync(HttpClient client, ImmutableDictionary<string, string> form, string path)
    {
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = content };
        request.Headers.Add("X-Aioros-Navigation", "1");
        using var answer = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [GeneratedRegex("<input type=\"text\"[^>]*>")]
    private static partial Regex Input();

    private sealed class CountingEngine(IPageEngine inner) : IPageEngine
    {
        private int _posts;

        public int Posts => Volatile.Read(ref _posts);

        public bool OwnsCommands => inner.OwnsCommands;

        public bool RunsCommands => inner.RunsCommands;

        public ValueTask<CompiledPage> RenderAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken) =>
            inner.RenderAsync(page, models, theme, cancellationToken);

        public ValueTask<CompiledPage> AnswerAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken) =>
            inner.AnswerAsync(page, models, theme, cancellationToken);

        public ValueTask<PostOutcome> PostAsync(PageDefinition page, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _posts);
            return inner.PostAsync(page, models, form, files, theme, cancellationToken);
        }

        public ValueTask<PostOutcome> PostAsync(PageDefinition page, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, bool planAnswers, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _posts);
            return inner.PostAsync(page, models, form, files, theme, planAnswers, cancellationToken);
        }
    }
}
