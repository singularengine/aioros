using System.Collections.Concurrent;
using System.Security.Claims;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Access;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class ViewLoaderTests
{
    private const string Key = "Pages/inbox/Page.axaml:0.1";

    private static readonly RenderPlan ViewPlan = new([], [
        new LiteralNode("<p>page</p>"),
        new ViewNode(1, Key, [new LiteralNode("<b>"), new ValueNode(model => ((Model)model!).Title), new LiteralNode("</b>")], [new LiteralNode("<em>failed</em>")]),
    ], []);

    [Fact]
    [Trait("AC", "SSR-67")]
    public async Task ViewLoadersRunInParallelWithThePagesInTheirOwnScopesAsync()
    {
        var started = Enumerable.Range(0, 4).Select(_ => new TaskCompletionSource()).ToArray();
        var data = Enumerable.Range(0, 4).Select(_ => new TaskCompletionSource<object?>()).ToArray();
        var scopes = new ConcurrentQueue<Scoped>();
        LoaderDefinition Loader(int index) => new(
            async (services, _, _) =>
            {
                scopes.Enqueue(services.GetRequiredService<Scoped>());
                started[index].SetResult();
                return new LoaderOutcome(await data[index].Task);
            },
            (_, loaded) => new Model((string)loaded!));
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", Unreachable) { Loader = Loader(0) }])
        {
            Loader = Loader(1),
            Views = [View("Views/A.axaml", Loader(2)), View("Views/B.axaml", Loader(3))],
        };
        var site = new Site();
        var http = Site.Http();
        http.RequestServices = new ServiceCollection().AddScoped<Scoped>().BuildServiceProvider().CreateScope().ServiceProvider;

        var get = site.Handler.GetAsync(http, page);
        var allStarted = started.All(signal => signal.Task.IsCompleted);
        foreach (var (signal, value) in data.Zip(["layout", "page", "a", "b"]).Reverse())
        {
            signal.SetResult(value);
        }

        await get;

        Assert.True(allStarted);
        Assert.Equal(4, scopes.Distinct().Count());
        Assert.Equal(["layout", "page", "a", "b"], site.Engine.Renders.Single().Models.Select(model => ((Model)model!).Title));
    }

    [Fact]
    [Trait("AC", "SSR-68")]
    public async Task OnlyThePageAndItsLayoutsNameThePageAsync()
    {
        var page = Paged(new LoaderOutcome("page") { Title = "Page title", Metadata = new PageMetadata("Page description") },
            View("Views/A.axaml", Loading(_ => new LoaderOutcome("a") { Title = "View title", Metadata = new PageMetadata("View description") })),
            View("Views/B.axaml", Loading(_ => LoaderResult.NotFound())),
            View("Views/C.axaml", Loading(_ => LoaderResult.Redirect("/elsewhere"))));
        var stopped = Paged(LoaderResult.NotFound(), View("Views/A.axaml", Loading(_ => new LoaderOutcome("a"))));
        var moved = Paged(LoaderResult.Redirect("/elsewhere"), View("Views/A.axaml", Loading(_ => new LoaderOutcome("a"))));
        var site = new Site();
        var http = Site.Http();
        var (stoppedHttp, movedHttp) = (Site.Http(), Site.Http());

        await site.Handler.GetAsync(http, page);
        await site.Handler.GetAsync(stoppedHttp, stopped);
        await site.Handler.GetAsync(movedHttp, moved);
        var body = Site.Body(http);

        Assert.Equal(200, http.Response.StatusCode);
        Assert.Contains("<title>Page title</title>", body, StringComparison.Ordinal);
        Assert.Contains("Page description", body, StringComparison.Ordinal);
        Assert.DoesNotContain("View title", body, StringComparison.Ordinal);
        Assert.DoesNotContain("View description", body, StringComparison.Ordinal);
        Assert.Equal([ViewState.Hidden, ViewState.Hidden], site.Engine.Renders.Single().Models[2..]);
        Assert.Equal((404, 302, "/elsewhere"), (stoppedHttp.Response.StatusCode, movedHttp.Response.StatusCode, movedHttp.Response.Headers.Location.ToString()));
    }

    [Fact]
    [Trait("AC", "SSR-69")]
    public async Task ViewsTheVisitorMayNotSeeRenderNothingAsync()
    {
        var runs = 0;
        var page = Paged(new LoaderOutcome("page"), new ViewInstance(Key, 0, new ViewDefinition("Views/Admin.axaml", Unreachable)
        {
            Loader = Loading(_ =>
            {
                Interlocked.Increment(ref runs);
                return new LoaderOutcome("admin view");
            }),
            Access = [new PageAccess(null, "admin")],
        }));
        var site = new Site { Engine = { Plan = ViewPlan } };
        var visitors = new[] { new ClaimsPrincipal(), User("member"), User("admin") };
        var bodies = new List<string>();

        foreach (var visitor in visitors)
        {
            var http = Site.Http();
            http.RequestServices = new ServiceCollection().AddLogging().AddAuthorization().AddSingleton<IViewAccess, AuthorizationViewAccess>().BuildServiceProvider();
            http.User = visitor;
            await site.Handler.GetAsync(http, page);
            Assert.Equal(200, http.Response.StatusCode);
            bodies.Add(Site.Body(http));
        }

        Assert.Equal(1, runs);
        Assert.Same(ViewState.Hidden, site.Engine.Renders[0].Models[1]);
        Assert.All(bodies.Take(2), body => Assert.DoesNotContain("<div data-aioros-view", body, StringComparison.Ordinal));
        Assert.Contains($"<div data-aioros-view=\"{Key}\" style=\"display:contents\"><b>admin view</b></div>", bodies[2], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-70")]
    public async Task AFailingViewRendersItsErrorWhileThePageAnswersAsync()
    {
        var logs = new Logs();
        var page = Paged(new LoaderOutcome("page"), View("Views/Broken.axaml", new LoaderDefinition((_, _, _) => ValueTask.FromException<LoaderOutcome>(new InvalidOperationException("The feed is down.")), (_, _) => new Model())));
        var site = new Site { Engine = { Plan = ViewPlan } };
        var http = Site.Http();
        http.RequestServices = new ServiceCollection().AddLogging(logging => logging.AddProvider(logs)).BuildServiceProvider();

        await site.Handler.GetAsync(http, page);

        Assert.Equal(200, http.Response.StatusCode);
        Assert.Same(ViewState.Failed, site.Engine.Renders.Single().Models[1]);
        Assert.Contains($"<p>page</p><div data-aioros-view=\"{Key}\" data-view-error role=\"alert\" style=\"display:contents\"><em>failed</em></div>", Site.Body(http), StringComparison.Ordinal);
        Assert.Equal("The feed is down.", Assert.Single(logs.Errors).Exception?.Message);
        Assert.Contains(Key, logs.Errors.Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-71")]
    public async Task ViewsWithoutLoadersGetTheirViewModelFromDependencyInjectionAsync()
    {
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model("page"), [])
        {
            Views = [new ViewInstance(Key, 0, new ViewDefinition("Views/Plain.axaml", services => services.GetService(typeof(Model))))],
        };
        var site = new Site { Engine = { Reusable = false } };
        var registered = new List<Model>();

        for (var request = 0; request < 2; request++)
        {
            var http = Site.Http();
            var model = new Model("view " + request);
            registered.Add(model);
            http.RequestServices = new ServiceCollection().AddSingleton(model).BuildServiceProvider();
            await site.Handler.GetAsync(http, page);
        }

        Assert.Equal(registered, site.Engine.Renders.Select(render => (Model)render.Models[1]!));
    }

    private static PageDefinition Paged(LoaderOutcome outcome, params ViewInstance[] views) =>
        new("/inbox", "Pages/inbox/Page.axaml", Unreachable, [])
        {
            Loader = Loading(_ => outcome),
            Views = [.. views],
        };

    private static ViewInstance View(string source, LoaderDefinition loader) => new(Key, 0, new ViewDefinition(source, Unreachable) { Loader = loader });

    private static LoaderDefinition Loading(Func<PageRequest, LoaderOutcome> load) =>
        new((_, request, _) => ValueTask.FromResult(load(request)), (_, data) => new Model((string)data!));

    private static ClaimsPrincipal User(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, role), new Claim(ClaimTypes.Role, role)], "test"));

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");

    private sealed class Scoped
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class Logs : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<(string Message, Exception? Exception)> Errors { get; } = new();

        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                Errors.Enqueue((formatter(state, exception), exception));
            }
        }

        public void Dispose()
        {
        }
    }
}
