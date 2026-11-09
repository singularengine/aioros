using System.Collections.Immutable;
using Aioros.Avalonia.Tests.Models;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Pages;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class PlanParityTests(EngineFixture fixture)
{
    private static readonly PageRequest Request = new("/parity", ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty);

    [Fact]
    [Trait("AC", "SSR-300")]
    public async Task EveryFieldOfEveryInputKindAgreesWithTheEngineAsync()
    {
        var verdicts = await VerifyAsync(static resolver => resolver);

        Assert.Equal(["_btn", "f0", "f1", "f2", "f3", "f4"], verdicts.Select(static verdict => verdict.Field.Name).Distinct().Order(StringComparer.Ordinal));
        Assert.All(verdicts, static verdict => Assert.True(verdict.Agrees, verdict.Field + " " + verdict.Sample + ": " + verdict.Difference));
    }

    [Fact]
    [Trait("AC", "SSR-301")]
    public async Task AFieldThePlanBindsOtherwiseIsNamedAsync()
    {
        var verdicts = await VerifyAsync(static resolver => new Muted(resolver, "text"));

        Assert.All(verdicts.Where(static verdict => verdict.Field.Name == "f0"), static verdict => Assert.False(verdict.Agrees));
        Assert.Contains(verdicts, static verdict => verdict.Field.Name == "f0" && verdict.Difference is { Length: > 0 });
        Assert.All(verdicts.Where(static verdict => verdict.Field.Name != "f0"), static verdict => Assert.True(verdict.Agrees, verdict.Field + " " + verdict.Sample + ": " + verdict.Difference));
    }

    [Fact]
    [Trait("AC", "SSR-307")]
    public async Task APostThePlanLeavesToTheEngineSaysWhyAsync()
    {
        var verdicts = await VerifyAsync(static resolver => new Unbound(resolver, "text"));

        Assert.Contains(verdicts, static verdict => verdict.Difference == "the plan does not bind it: fields it cannot bind: f0 (text)");
    }

    [Fact]
    [Trait("AC", "SSR-306")]
    public async Task APageThatRendersDifferentlyOnEachLoadIsNotVerifiedAsync()
    {
        var loads = 0;
        var page = PageServer.Page("/parity", "Parity", _ => new ParityModel { Title = "Load " + Interlocked.Increment(ref loads) });
        await using var app = await PageServer.StartAsync(fixture, page);
        using var scope = app.Services.CreateScope();

        var result = await PlanParity.VerifyPageAsync(page, Request, scope.ServiceProvider, app.Services.GetRequiredService<PageHandler>(), fixture.Engine, new Plans.AdapterSlotResolver(), TestContext.Current.CancellationToken);
        var report = PrecompileReport.Of([new WarmupOutcome("/parity", [], null) { Volatile = result.Volatile }], []);

        Assert.True(result.Volatile);
        Assert.Empty(result.Verdicts);
        Assert.Contains("precompiled  /parity  (posts not verified: its render changes from one load to the next)", report.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-304")]
    public async Task AFieldThatDivergesIsNamedAndLeftToTheEngineAsync()
    {
        var page = PageServer.Page("/parity", "Parity", static _ => new ParityModel());
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        Microsoft.AspNetCore.TestHost.WebHostBuilderExtensions.UseTestServer(builder.WebHost);
        builder.Services.AddAiorosPages(new Aioros.Targets.Ssr.Hosting.SsrOptions()).AddSingleton<IPageEngine>(fixture.Engine).AddSingleton<IFieldResolver>(new Muted(new Plans.AdapterSlotResolver(), "text"));
        await using var app = builder.Build();
        app.MapAiorosPages([page]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var logs = new Aioros.Targets.Ssr.Tests.Pages.RecordedLogs();
        var handler = app.Services.GetRequiredService<PageHandler>();
        using var scope = app.Services.CreateScope();
        using var client = Microsoft.AspNetCore.TestHost.HostBuilderTestServerExtensions.GetTestClient(app);

        var outcomes = await Warmup.RunAsync([page], scope.ServiceProvider, handler, posts: false, verify: true, TestContext.Current.CancellationToken);
        Warmup.Report(logs.CreateLogger("Aioros.Pages"), outcomes, TimeSpan.Zero);
        var written = await client.GetStringAsync(new Uri("/parity", UriKind.Relative), TestContext.Current.CancellationToken);
        var form = PostedForms.Of(written);
        var text = await PostAsync(client, form.SetItem("f0", "Typed").SetItem("_chg", "0"));
        var check = await PostAsync(client, form.SetItem("f1", "on").SetItem("_chg", "1"));

        Assert.Contains(logs.Entries, static entry => entry.Message.Contains("/parity", StringComparison.Ordinal) && entry.Message.Contains("f0 (text)", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, static entry => entry.Message.Contains("f1 (check)", StringComparison.Ordinal));
        Assert.Contains("Typed False Free", text, StringComparison.Ordinal);
        Assert.Contains("Report True Free", check, StringComparison.Ordinal);
        Assert.Equal(["f0"], handler.Plans.EngineFields("/parity").Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("AC", "SSR-309")]
    public async Task APostsLocalInputsShowTheirPostedValuesAsync()
    {
        var page = PageServer.Page("/locals", "Locals", static _ => new ParityModel());
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        Microsoft.AspNetCore.TestHost.WebHostBuilderExtensions.UseTestServer(builder.WebHost);
        builder.Services.AddAiorosPages(new Aioros.Targets.Ssr.Hosting.SsrOptions()).AddSingleton<IPageEngine>(fixture.Engine).AddSingleton<IFieldResolver>(new Muted(new Plans.AdapterSlotResolver(), "text"));
        await using var app = builder.Build();
        app.MapAiorosPages([page]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var handler = app.Services.GetRequiredService<PageHandler>();
        using var scope = app.Services.CreateScope();
        using var client = Microsoft.AspNetCore.TestHost.HostBuilderTestServerExtensions.GetTestClient(app);

        await Warmup.RunAsync([page], scope.ServiceProvider, handler, posts: false, verify: true, TestContext.Current.CancellationToken);
        var written = await client.GetStringAsync(new Uri("/locals", UriKind.Relative), TestContext.Current.CancellationToken);
        var form = PostedForms.Of(written).SetItem("f1", "on").SetItem("f2", "9");
        var planned = await PostAsync(client, form.SetItem("_chg", "1"), "/locals");
        var engine = await PostAsync(client, form.SetItem("f0", "Typed").SetItem("_chg", "0"), "/locals");
        var again = await client.GetStringAsync(new Uri("/locals", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(["f0"], handler.Plans.EngineFields("/locals").Order(StringComparer.Ordinal));
        Assert.Matches("name=\"f1\"[^>]* checked", planned);
        Assert.Contains("value=\"9\"", planned, StringComparison.Ordinal);
        Assert.Contains("Typed False Free", engine, StringComparison.Ordinal);
        Assert.Matches("name=\"f1\"[^>]* checked", engine);
        Assert.Contains("value=\"9\"", engine, StringComparison.Ordinal);
        Assert.DoesNotMatch("name=\"f1\"[^>]* checked", again);
        Assert.Contains("value=\"4\"", again, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-310")]
    public async Task APostCarryingAFieldThePlanCannotShowIsRenderedByTheEngineAsync()
    {
        var page = PageServer.Page("/shown", "LocalsShown", static _ => new ParityModel());
        await using var app = await PageServer.StartAsync(fixture, page);
        using var client = Microsoft.AspNetCore.TestHost.HostBuilderTestServerExtensions.GetTestClient(app);

        var written = await client.GetStringAsync(new Uri("/shown", UriKind.Relative), TestContext.Current.CancellationToken);
        var answer = await PostAsync(client, PostedForms.Of(written).SetItem("f0", "Typed").SetItem("f1", "true").SetItem("_chg", "0"), "/shown");

        Assert.Contains("Typed False Free", answer, StringComparison.Ordinal);
        Assert.Matches("name=\"f1\"[^>]*value=\"true\" checked", answer);
    }

    private static async Task<string> PostAsync(HttpClient client, ImmutableDictionary<string, string> form, string path = "/parity")
    {
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = content };
        request.Headers.Add("X-Aioros-Navigation", "1");
        using var answer = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ImmutableArray<FieldVerdict>> VerifyAsync(Func<IFieldResolver, IFieldResolver> resolver)
    {
        var page = PageServer.Page("/parity", "Parity", static _ => new ParityModel());
        await using var app = await PageServer.StartAsync(fixture, page);
        var services = app.Services;
        using var scope = services.CreateScope();
        return await PlanParity.VerifyAsync(page, Request, scope.ServiceProvider, services.GetRequiredService<PageHandler>(), fixture.Engine,
            resolver(services.GetService<IFieldResolver>() ?? new Plans.AdapterSlotResolver()), TestContext.Current.CancellationToken);
    }

    private sealed class Unbound(IFieldResolver inner, string kind) : IFieldResolver
    {
        public FieldBinding? Binding(SlotSpec field) => field.Kind == kind ? null : inner.Binding(field);

        public FieldCommand? Command(SlotSpec field) => inner.Command(field);
    }

    private sealed class Muted(IFieldResolver inner, string kind) : IFieldResolver
    {
        public FieldBinding? Binding(SlotSpec field) => field.Kind == kind ? static (_, _) => { } : inner.Binding(field);

        public FieldCommand? Command(SlotSpec field) => inner.Command(field);
    }
}
