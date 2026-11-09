using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using Aioros.Functions;
using Aioros.Pages;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Pages;
using Aioros.Targets.Ssr.Functions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class DataCacheTests
{
    private const string Contract = "c1";

    [Fact]
    [Trait("AC", "FUNCTIONS-19")]
    public async Task LoadsShareACachedReadAcrossRequestsAsync()
    {
        await using var app = await AppAsync(shared: true);
        var store = app.Services.GetRequiredService<Store>();

        var first = await LoadIssueAsync(app, 7);
        var second = await LoadIssueAsync(app, 7);
        var other = await LoadIssueAsync(app, 8);
        var failed = await Record.ExceptionAsync(() => LoadIssueAsync(app, Store.FailsOnce));
        var retried = await LoadIssueAsync(app, Store.FailsOnce);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var command = scope.ServiceProvider.GetRequiredService<IIssues>();
            await command.FindAsync(7);
            await command.FindAsync(7);
        }

        Assert.Same(first, second);
        Assert.Equal(8, other.Id);
        Assert.NotNull(failed);
        Assert.Equal(Store.FailsOnce, retried.Id);
        Assert.Equal((3, 1, 2), (store.Runs(7), store.Runs(8), store.Runs(Store.FailsOnce)));
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-20")]
    public async Task InvalidatedKeysEvictTheirEntriesAsync()
    {
        await using var app = await AppAsync(shared: true);
        var store = app.Services.GetRequiredService<Store>();
        var http = app.GetTestClient();
        async Task<(int Seven, int Eight)> LoadBothAsync()
        {
            await LoadIssueAsync(app, 7);
            await LoadIssueAsync(app, 8);
            return (store.Runs(7), store.Runs(8));
        }

        var loaded = await LoadBothAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIssues>().SaveAsync(7);
        }

        var afterCommand = await LoadBothAsync();
        using (var request = new HttpRequestMessage(HttpMethod.Post, ServerFunctionGroup.Path("issues", "save")) { Content = new StringContent("{\"id\":8}", Encoding.UTF8, "application/json") })
        {
            request.Headers.Add(ServerFunctionGroup.FunctionHeader, "1");
            request.Headers.Add(ServerFunctionGroup.ContractHeader, Contract);
            using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        }

        var afterDispatch = await LoadBothAsync();
        app.Services.GetRequiredService<IDataKeys>().Invalidate("issue:7");
        var afterService = await LoadBothAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IIssues>().FailAsync(7));
        }

        var afterFailure = await LoadBothAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIssues>().TouchAsync();
        }

        var afterUndeclared = await LoadBothAsync();

        Assert.Equal((1, 1), loaded);
        Assert.Equal((2, 1), afterCommand);
        Assert.Equal((2, 2), afterDispatch);
        Assert.Equal((3, 2), afterService);
        Assert.Equal((3, 2), afterFailure);
        Assert.Equal((4, 3), afterUndeclared);
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-21")]
    public async Task CachedReadsExpireWithTheirLifetimeAsync()
    {
        var time = new FakeTimeProvider();
        await using var app = await AppAsync(shared: true, time);
        var store = app.Services.GetRequiredService<Store>();

        await LoadIssueAsync(app, 7);
        time.Advance(TimeSpan.FromSeconds(59));
        await LoadIssueAsync(app, 7);
        var beforeTheLifetime = store.Runs(7);
        time.Advance(TimeSpan.FromSeconds(1));
        await LoadIssueAsync(app, 7);

        Assert.Equal(1, beforeTheLifetime);
        Assert.Equal(2, store.Runs(7));
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-22")]
    public async Task AFullCacheDropsStaleThenSoonestToExpireAsync()
    {
        var time = new FakeTimeProvider();
        var cache = new DataCache(new DataVersions(), time, maxEntries: 4);
        var runs = new List<string>();
        var caller = new object();
        Task<string> CallAsync(string name, int seconds) =>
            cache.CallAsync("store/" + name, name, caller, TimeSpan.FromSeconds(seconds), shared: true, [name], () =>
            {
                runs.Add(name);
                return Task.FromResult(name);
            });

        await CallAsync("a", 10);
        await CallAsync("b", 50);
        await CallAsync("c", 40);
        await CallAsync("d", 30);
        time.Advance(TimeSpan.FromSeconds(15));
        cache.Invalidate("b");
        await CallAsync("e", 60);
        var afterStale = cache.Count;
        await CallAsync("f", 60);
        await CallAsync("g", 60);
        var afterFull = cache.Count;
        runs.Clear();
        foreach (var (name, seconds) in new[] { ("c", 40), ("e", 60), ("f", 60), ("g", 60), ("d", 30) })
        {
            await CallAsync(name, seconds);
        }

        Assert.Equal(3, afterStale);
        Assert.Equal(4, afterFull);
        Assert.Equal(["d"], runs);
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-23")]
    public async Task DataCachedPerUserIsNeverSharedAsync()
    {
        await using var app = await AppAsync(shared: false);
        var store = app.Services.GetRequiredService<Store>();
        var ada = User("ada");
        var grace = User("grace");

        var first = await LoadIssueAsync(app, 7, ada);
        var again = await LoadIssueAsync(app, 7, ada);
        var hers = await LoadIssueAsync(app, 7, grace);
        await LoadIssueAsync(app, 7, new ClaimsPrincipal(new ClaimsIdentity()));
        await LoadIssueAsync(app, 7, new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Same(first, again);
        Assert.NotSame(first, hers);
        Assert.Equal(4, store.Runs(7));
    }

    private static ClaimsPrincipal User(string id) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "cookie"));

    private static async Task<Summary> LoadIssueAsync(WebApplication app, int id, ClaimsPrincipal? user = null)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var page = new PageDefinition("/issues/" + id, "Pages/issues/[id]/Page.axaml", _ => null, [])
        {
            Loader = new LoaderDefinition(
                static async (services, request, _) => new LoaderOutcome(await ((IIssues)services.GetService(typeof(IIssues))!).FindAsync(request.Number("id"))),
                static (_, data) => data),
        };
        var request = new PageRequest("/issues/" + id, ImmutableDictionary<string, string>.Empty.Add("id", id.ToString(System.Globalization.CultureInfo.InvariantCulture)), ImmutableDictionary<string, string>.Empty)
        {
            User = user ?? new ClaimsPrincipal(new ClaimsIdentity()),
        };
        var loaded = await PageLoader.LoadAsync(scope.ServiceProvider, page, request, TestContext.Current.CancellationToken);
        return (Summary)loaded.Models[0]!;
    }

    private static async Task<WebApplication> AppAsync(bool shared, TimeProvider? time = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging().AddSingleton<Store>();
        if (time is not null)
        {
            builder.Services.AddSingleton(time);
        }

        builder.Services.AddServerFunction<IIssues, Issues>([Group(shared)], server: null);
        var app = builder.Build();
        app.MapServerFunctions([Group(shared)]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static ServerFunctionGroup Group(bool shared) => new(
        typeof(IIssues),
        "issues",
        Contract,
        [new ServerFunctionRoute("save", static async (service, arguments, _) =>
        {
            await ((IIssues)service).SaveAsync(arguments.GetProperty("id").GetInt32());
            return null;
        }) { Keyed = true }],
        static http => http)
    {
        InProcess = implementation => new IssuesInProcess((IIssues)implementation, shared),
    };

    internal sealed record Summary(int Id);

    internal interface IIssues
    {
        Task<Summary> FindAsync(int id);

        Task SaveAsync(int id);

        Task FailAsync(int id);

        Task TouchAsync();
    }

    internal sealed class Store
    {
        public const int FailsOnce = 9;

        private ImmutableDictionary<int, int> _runs = ImmutableDictionary<int, int>.Empty;

        public int Runs(int id) => Volatile.Read(ref _runs).GetValueOrDefault(id);

        public async Task<Summary> FindAsync(int id)
        {
            await Task.Yield();
            var runs = ImmutableInterlocked.AddOrUpdate(ref _runs, id, 1, static (_, count) => count + 1);
            return id == FailsOnce && runs == 1 ? throw new InvalidOperationException("The store is down.") : new Summary(id);
        }
    }

    internal sealed class Issues(Store store) : IIssues
    {
        [Cached(60, Shared = true)]
        [Reads("issue:{id}")]
        public Task<Summary> FindAsync(int id) => store.FindAsync(id);

        [Invalidates("issue:{id}")]
        public Task SaveAsync(int id) => Task.CompletedTask;

        [Invalidates("issue:{id}")]
        public Task FailAsync(int id) => Task.FromException(new InvalidOperationException("The save failed."));

        public Task TouchAsync() => Task.CompletedTask;
    }

    /// <summary>The in-process caller the generator writes for <see cref="Issues"/>: <c>[Cached]</c>, <c>[Reads]</c> and <c>[Invalidates]</c>.</summary>
    private sealed class IssuesInProcess(IIssues implementation, bool shared) : IIssues, IKeyedCalls, ICachedCalls
    {
        public IDataKeys? Keys { get; set; }

        public DataCache? Cache { get; set; }

        public async Task<Summary> FindAsync(int id)
        {
            var result = await (Cache is { } cache
                ? cache.CallAsync("issues/find", ValueTuple.Create(id), Keys, TimeSpan.FromSeconds(60), shared, ["issue:" + id], () => implementation.FindAsync(id))
                : implementation.FindAsync(id));
            (Keys ?? DataKeys.Current)?.Read("issue:" + id);
            return result;
        }

        public async Task SaveAsync(int id)
        {
            await implementation.SaveAsync(id);
            (Keys ?? DataKeys.Current)?.Invalidate("issue:" + id);
            Cache?.Invalidate("issue:" + id);
        }

        public async Task FailAsync(int id)
        {
            await implementation.FailAsync(id);
            (Keys ?? DataKeys.Current)?.Invalidate("issue:" + id);
            Cache?.Invalidate("issue:" + id);
        }

        public async Task TouchAsync()
        {
            await implementation.TouchAsync();
            if (Keys is null)
            {
                DataKeys.Current?.Undeclared();
                Cache?.InvalidateAll();
            }
        }
    }
}
