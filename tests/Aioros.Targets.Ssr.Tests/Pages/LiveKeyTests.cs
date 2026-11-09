using System.Net;
using System.Net.ServerSentEvents;
using System.Security.Claims;
using Aioros.Functions;
using Aioros.Pages;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Pages;
using Aioros.Targets.Ssr.Live;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class LiveKeyTests
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-115")]
    public async Task InvalidatedKeysRerunTheLoadersThatReadThemAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var site = new Site();
        site.Engine.Plan = ViewKeyTests.Plan;
        var counts = new ViewKeyTests.Counts();
        await using var services = Services();
        var versions = services.GetRequiredService<DataVersions>();
        var stream = new SignalStream();
        using var abort = new CancellationTokenSource();
        var http = Site.Http(body: stream);
        http.RequestServices = services.CreateScope().ServiceProvider;
        http.Request.Path = "/board/live";
        http.RequestAborted = abort.Token;

        var live = LiveUpdates.StreamAsync(http, ViewKeyTests.Board(counts, "sales"), site.Handler);
        await stream.Writes.ReadAsync(token);
        var loaded = string.Join(",", counts.Loads);
        versions.Invalidate("issue:7");
        var one = await stream.Writes.ReadAsync(token);
        var afterOne = string.Join(",", counts.Loads.Skip(7));
        versions.Invalidate("issues");
        var issues = await stream.Writes.ReadAsync(token);
        versions.Invalidate("nobody");
        versions.Invalidate("other");
        var other = await stream.Writes.ReadAsync(token);
        var afterOther = string.Join(",", counts.Loads.Skip(7));
        versions.Invalidate("sales");
        var page = await stream.Writes.ReadAsync(token);
        var afterPage = string.Join(",", counts.Loads.Skip(7));
        await abort.CancelAsync();
        await live;

        Assert.Equal("layout 1,page 1,a 1,b 1,c 1,d 1,e 1", loaded);
        Assert.Equal("d 2", afterOne);
        Assert.StartsWith($"event: views\ndata: <div data-aioros-view=\"{ViewKeyTests.Key("d")}\"", one, StringComparison.Ordinal);
        Assert.Contains("<b>d 2</b>", one, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>a 1</b>", one, StringComparison.Ordinal);
        Assert.Contains("<b>a 2</b>", issues, StringComparison.Ordinal);
        Assert.StartsWith($"event: views\ndata: <div data-aioros-view=\"{ViewKeyTests.Key("b")}\"", other, StringComparison.Ordinal);
        Assert.Equal("d 2,a 2,b 2", afterOther);
        Assert.Equal("d 2,a 2,b 2,layout 2,page 2", afterPage);
        Assert.StartsWith("data: <h1>layout 2</h1>", page, StringComparison.Ordinal);
        Assert.Contains("<b>d 2</b>", page, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-116")]
    public async Task EachConnectionRerunsItsLoadersWithItsOwnUserAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var site = new Site();
        var runs = new System.Collections.Concurrent.ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", static _ => throw new InvalidOperationException("A model with a loader is created from its data."), [], Live: true)
        {
            Loader = new LoaderDefinition(
                (services, request, _) =>
                {
                    services.GetRequiredService<IDataKeys>().Read("sales");
                    var name = request.User?.Identity?.Name ?? "nobody";
                    return ValueTask.FromResult(new LoaderOutcome($"{name}, run {runs.AddOrUpdate(name, 1, static (_, count) => count + 1)}"));
                },
                static (_, data) => new Model((string)data!))
            {
                Receive = static (model, data) => ((Model)model).Title = (string)data!,
            },
        };
        await using var services = Services();
        var versions = services.GetRequiredService<DataVersions>();
        using var abort = new CancellationTokenSource();
        (Task Live, SignalStream Stream) Open(string user)
        {
            var stream = new SignalStream();
            var http = Site.Http(body: stream);
            http.RequestServices = services.CreateScope().ServiceProvider;
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, user)], "test"));
            http.Request.Path = "/inbox/live";
            http.RequestAborted = abort.Token;
            return (LiveUpdates.StreamAsync(http, page, site.Handler), stream);
        }

        var ada = Open("Ada");
        var grace = Open("Grace");
        await ada.Stream.Writes.ReadAsync(token);
        await grace.Stream.Writes.ReadAsync(token);
        versions.Invalidate("sales");
        var forAda = await ada.Stream.Writes.ReadAsync(token);
        var forGrace = await grace.Stream.Writes.ReadAsync(token);
        await abort.CancelAsync();
        await Task.WhenAll(ada.Live, grace.Live);

        Assert.Equal("data: <p>Ada, run 2</p>\n\n", forAda);
        Assert.Equal("data: <p>Grace, run 2</p>\n\n", forGrace);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-117")]
    public async Task TheInstalledAppsStreamCarriesOnlyItsInvalidatedKeysAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<DataVersions>();
        await using var app = builder.Build();
        app.MapLiveKeys();
        await app.StartAsync(token);
        var versions = app.Services.GetRequiredService<DataVersions>();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/_live?keys=sales,issue%3A7", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, token);
        var events = SseParser.Create(await response.Content.ReadAsStreamAsync(token)).EnumerateAsync(token).GetAsyncEnumerator(token);
        async Task<(string Type, string Data)> NextAsync() => await events.MoveNextAsync() ? (events.Current.EventType, events.Current.Data) : default;
        var open = await NextAsync();
        versions.Invalidate("other");
        versions.Invalidate("sales");
        var sales = await NextAsync();
        versions.Invalidate("issue:7");
        var issue = await NextAsync();
        versions.InvalidateAll();
        var all = await NextAsync();
        await events.DisposeAsync();
        using var none = await client.GetAsync(new Uri("/_live", UriKind.Relative), token);
        using var many = await client.GetAsync(new Uri("/_live?keys=" + string.Join(',', Enumerable.Range(0, 257)), UriKind.Relative), token);

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("open", open.Type);
        Assert.Equal(("keys", "sales"), sales);
        Assert.Equal(("keys", "issue%3A7"), issue);
        Assert.Equal(("keys", "issue%3A7,sales"), all);
        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (none.StatusCode, many.StatusCode));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-192")]
    public async Task AnInvalidationDuringTheSettleOfARefreshIsStillPushedAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var site = new Site();
        var runs = 0;
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", static _ => throw new InvalidOperationException("A model with a loader is created from its data."), [], Live: true)
        {
            Loader = new LoaderDefinition(
                (services, _, _) =>
                {
                    services.GetRequiredService<IDataKeys>().Read("sales");
                    return ValueTask.FromResult(new LoaderOutcome($"run {++runs}"));
                },
                static (_, data) => new Model((string)data!))
            {
                Receive = static (model, data) => ((Model)model).Title = (string)data!,
            },
        };
        var ui = new GatedUiThread();
        await using var services = Services(ui);
        var versions = services.GetRequiredService<DataVersions>();
        var stream = new SignalStream();
        using var abort = new CancellationTokenSource();
        var http = Site.Http(body: stream);
        http.RequestServices = services.CreateScope().ServiceProvider;
        http.Request.Path = "/inbox/live";
        http.RequestAborted = abort.Token;

        var live = LiveUpdates.StreamAsync(http, page, site.Handler);
        await stream.Writes.ReadAsync(token);
        versions.Invalidate("other");
        await ui.Waiting.WaitAsync(token);
        versions.Invalidate("sales");
        ui.Open();
        var pushed = await stream.Writes.ReadAsync(token);
        await abort.CancelAsync();
        await live;

        Assert.Equal("data: <p>run 2</p>\n\n", pushed);
    }

    private static ServiceProvider Services(IUiThread? ui = null) => (ui is null ? new ServiceCollection() : new ServiceCollection().AddSingleton(ui))
        .AddSingleton<DataVersions>()
        .AddScoped<ViewKeyTests.IssueCalls>()
        .AddScoped<PageNavigator>()
        .AddScoped<Aioros.Navigation.INavigator>(static services => services.GetRequiredService<PageNavigator>())
        .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
}
