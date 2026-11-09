using System.Collections.Concurrent;
using System.Net;
using Aioros.Functions;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Pages;
using Aioros.Targets.Desktop.Navigation;
using Aioros.Targets.Ssr.Data;
using Aioros.Targets.Ssr.Functions;
using Aioros.Targets.Ssr.Live;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopRemoteDataTests(DesktopFixture fixture)
{
    private const string Contract = "c1";
    private const string NotFoundContract = "n1";

    [Fact]
    [Trait("AC", "DESKTOP-27")]
    public async Task RemoteNavigationsLoadThroughTheDataEndpointAsync()
    {
        await using var server = await ServerAsync();
        var requests = new ConcurrentQueue<string>();
        await using var services = Client(server, requests);
        var (texts, title, created, missing, moved) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: Pages(new Counts()));
            await navigator.NavigateAsync("/saving/7?tab=open");
            var shown = (DesktopFixture.Texts(window).Take(3).ToList(), window.Title, Saving.Last!.Label);
            await navigator.NavigateAsync("/missing");
            var notFound = DesktopFixture.Texts(window).ToList();
            await navigator.NavigateAsync("/moved");
            var result = (shown.Item1, shown.Title, shown.Label, notFound, navigator.Current);
            window.Close();
            return result;
        });

        Assert.Equal(["/_data/saving/7?tab=open c1", "/_data/missing c1", "/_data/moved c1"], requests);
        Assert.Equal(["Shell", "section 1 on server", "item 7/open #1 on server"], texts);
        Assert.Equal("Item 7 · server", title);
        Assert.Equal("item 7/open #1 on server", created);
        Assert.Equal(["Shell", "Nothing here"], missing);
        Assert.Equal("/about", moved);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-28")]
    public async Task RemoteRevalidationsLoadThroughTheDataEndpointAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        await using var server = await ServerAsync();
        var requests = new ConcurrentQueue<string>();
        await using var services = Client(server, requests);
        var (revalidated, kept, texts) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: Pages(new Counts()));
            await navigator.NavigateAsync("/saving/7");
            var model = Saving.Last!;
            var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            navigator.CommandSettled += (_, value) => settled.TrySetResult(value);
            DesktopFixture.Click(window, "Save");
            var result = (await settled.Task.WaitAsync(bound), ReferenceEquals(model, Saving.Last), DesktopFixture.Texts(window).Take(4).ToList());
            window.Close();
            return result;
        });

        Assert.True(revalidated);
        Assert.True(kept);
        Assert.Equal(["Shell", "section 2 on server", "item 7 #2 on server", "Saved"], texts);
        Assert.Equal(["/_data/saving/7 c1", "/_data/saving/7 c1"], requests);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-47")]
    public async Task RemoteViewCommandsLoadOnlyThatViewAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        await using var server = await ServerAsync();
        var requests = new ConcurrentQueue<string>();
        await using var services = Client(server, requests);
        var (revalidated, labels, kept) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: Pages(new Counts()));
            await navigator.NavigateAsync("/clicks");
            var second = DesktopViewCommandTests.Context(window, 2);
            var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            navigator.CommandSettled += (_, value) => settled.TrySetResult(value);
            DesktopViewCommandTests.Press(window, "View save", 1);
            var result = (await settled.Task.WaitAsync(bound), Enumerable.Range(0, 3).Select(index => DesktopViewCommandTests.Context(window, index).Label).ToList(), ReferenceEquals(second, DesktopViewCommandTests.Context(window, 2)));
            window.Close();
            return result;
        });

        Assert.True(revalidated);
        Assert.Equal(["page 1 on server", "first 1 on server", "second 2 on server"], labels);
        Assert.True(kept);
        Assert.Equal(["/_data/clicks c1", "/_data/clicks c1 " + Clicks + ":0.3"], requests);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-49")]
    public async Task RemoteCommandsRefreshTheViewsWhoseKeysTheServerInvalidatedAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        await using var server = await ServerAsync();
        var requests = new ConcurrentQueue<string>();
        await using var services = Client(server, requests);
        var (revalidated, labels, kept) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: Pages(new Counts()));
            await navigator.NavigateAsync("/keyed");
            var b = DesktopViewCommandTests.Context(window, 2);
            var settled = DesktopViewCommandTests.NextSettledAsync(navigator, bound);
            DesktopViewCommandTests.Press(window, "View save", 2);
            var result = (await settled, Enumerable.Range(0, 4).Select(index => DesktopViewCommandTests.Context(window, index).Label).ToList(), ReferenceEquals(b, DesktopViewCommandTests.Context(window, 2)));
            window.Close();
            return result;
        });

        Assert.True(revalidated);
        Assert.Equal(["page 1 on server", "a 2 on server", "b 1 on server", "c 2 on server"], labels);
        Assert.True(kept);
        Assert.Equal(["/_data/keyed c1", "/_sf/keys/save k1"], requests.Take(2));
        Assert.Equal(["/_data/keyed c1 " + Keyed + ":0.2", "/_data/keyed c1 " + Keyed + ":0.4"], requests.Skip(2).Order(StringComparer.Ordinal));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-56")]
    public async Task RemoteLivePagesRefetchTheViewsWhoseKeysTheServerInvalidatedAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await ServerAsync(ended);
        var requests = new ConcurrentQueue<string>();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var services = Client(server, requests, opened);
        var versions = server.Services.GetRequiredService<Aioros.Runtime.Caching.DataVersions>();
        var (labels, kept, sent) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: Pages(new Counts()));
            await navigator.NavigateAsync("/live");
            var b = DesktopViewCommandTests.Context(window, 2);
            await opened.Task.WaitAsync(bound);
            var refreshed = DesktopLiveTests.NextRefreshAsync(navigator, bound);
            versions.Invalidate("sales");
            await refreshed;
            var result = (Enumerable.Range(0, 4).Select(index => DesktopViewCommandTests.Context(window, index).Label).ToList(), ReferenceEquals(b, DesktopViewCommandTests.Context(window, 2)), requests.ToList());
            await navigator.NavigateAsync("/about");
            await ended.Task.WaitAsync(bound);
            window.Close();
            return result;
        });

        Assert.Equal(["page 1 on server", "a 2 on server", "b 1 on server", "c 1 on server"], labels);
        Assert.True(kept);
        Assert.Equal(["/_data/live c1", "/_live?keys=other,sales", "/_data/live c1 " + Keyed + ":0.2"], sent);
        Assert.Equal(0, versions.Watchers);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-85")]
    public async Task ARefusedLiveStreamFailsTheNavigatorAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        await using var server = await ServerAsync();
        using var http = new HttpClient(new LiveRefusal(server.GetTestServer().CreateHandler())) { BaseAddress = server.GetTestServer().BaseAddress };
        await using var services = Navigation(new ServiceCollection().AddSingleton<ILoaderResults>(new RemoteLoaderResults(http)));
        var (failure, texts) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: Pages(new Counts()));
            var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            navigator.Failed += (_, exception) => failed.TrySetResult(exception);
            await navigator.NavigateAsync("/live");
            var exception = await failed.Task.WaitAsync(bound);
            var result = (exception, DesktopFixture.Texts(window).ToList());
            window.Close();
            return result;
        });

        Assert.IsType<Aioros.Runtime.SignIn.SignInRequiredException>(failure);
        Assert.Equal(["Shell", "Something went wrong"], texts);
    }

    [Fact]
    [Trait("AC", "DESKTOP-29")]
    public async Task RefusedOrOutdatedDataRequestsFailTheNavigationAsync()
    {
        await using var server = await ServerAsync();
        await using var services = Client(server, new ConcurrentQueue<string>());
        var (refused, refusedTexts, outdated, outdatedTexts) = await fixture.OnUiAsync(async () =>
        {
            var pages = Pages(new Counts());
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [.. pages.Select(page => page.Route == "/saving/{id}" ? page with { DataContract = "c0" } : page)]);
            var failures = new List<Exception>();
            navigator.Failed += (_, exception) => failures.Add(exception);
            await navigator.TryNavigateAsync("/vault");
            var vault = DesktopFixture.Texts(window).ToList();
            await navigator.TryNavigateAsync("/saving/7");
            var result = (failures[0], vault, failures[1], DesktopFixture.Texts(window).ToList());
            window.Close();
            return result;
        });

        Assert.IsType<Aioros.Runtime.SignIn.SignInRequiredException>(refused);
        Assert.Contains("sign-in", refused.Message, StringComparison.Ordinal);
        Assert.Equal(["Shell", "Something went wrong"], refusedTexts);
        Assert.Equal(Contract, Assert.IsType<ClientOutdatedException>(outdated).ServerContract);
        Assert.Equal(["Shell", "Something went wrong"], outdatedTexts);
    }

    [Fact]
    [Trait("AC", "DESKTOP-34")]
    public async Task RemoteNotFoundRendersFromTheNotFoundAnswerAsync()
    {
        await using var server = await ServerAsync();
        var requests = new ConcurrentQueue<string>();
        await using var services = Client(server, requests);
        var (missing, nowhere, failure) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: Pages(new Counts()), notFound: NotFound(NotFoundContract));
            var failures = new List<Exception>();
            navigator.Failed += (_, exception) => failures.Add(exception);
            await navigator.TryNavigateAsync("/missing");
            var missingTexts = DesktopFixture.Texts(window).ToList();
            await navigator.TryNavigateAsync("/nowhere");
            var nowhereTexts = DesktopFixture.Texts(window).ToList();
            window.Close();
            var (outdated, outdatedNavigator) = DesktopFixture.Open(services: services, pages: Pages(new Counts()), notFound: NotFound("n0"));
            outdatedNavigator.Failed += (_, exception) => failures.Add(exception);
            await outdatedNavigator.TryNavigateAsync("/missing");
            outdated.Close();
            return (missingTexts, nowhereTexts, Assert.Single(failures));
        });

        Assert.Equal(["/_data/missing c1", "/_data/nowhere n1", "/_data/missing c1"], requests);
        Assert.Equal(["Shell", "lost /missing on server", "Nothing here"], missing);
        Assert.Equal(["Shell", "lost /nowhere on server", "Nothing here"], nowhere);
        Assert.Equal(NotFoundContract, Assert.IsType<ClientOutdatedException>(failure).ServerContract);
    }

    [Fact]
    [Trait("AC", "DESKTOP-35")]
    public async Task FailuresWithoutAnErrorPageShowTheBuiltInErrorViewAsync()
    {
        await using var server = await ServerAsync();
        await using var services = Client(server, new ConcurrentQueue<string>());
        var broken = new PageDefinition("/_error", "Pages/Error.axaml", static _ => throw new InvalidOperationException("The error page is down."), []);
        var (refused, brokenError, failures) = await fixture.OnUiAsync(async () =>
        {
            var failures = new List<Exception>();
            var (window, navigator) = DesktopFixture.Open(withError: false, services: services, pages: Pages(new Counts()));
            navigator.Failed += (_, exception) => failures.Add(exception);
            await navigator.TryNavigateAsync("/vault");
            var refusedTexts = DesktopFixture.Texts(window).ToList();
            window.Close();
            var (other, otherNavigator) = DesktopFixture.Open(services: DesktopFixture.Navigation(), error: broken);
            otherNavigator.Failed += (_, exception) => failures.Add(exception);
            await otherNavigator.TryNavigateAsync("/broken");
            var brokenTexts = DesktopFixture.Texts(other).ToList();
            other.Close();
            return (refusedTexts, brokenTexts, failures);
        });

        Assert.Equal(["Something went wrong", "This page could not be shown."], refused);
        Assert.Equal(["Something went wrong", "This page could not be shown."], brokenError);
        Assert.Collection(failures, static first => Assert.IsType<Aioros.Runtime.SignIn.SignInRequiredException>(first), static second => Assert.Equal("The board is down.", second.Message));
    }

    [Fact]
    [Trait("AC", "DESKTOP-30")]
    public async Task OnlyAServerAddressMakesTheLoadersRemoteAsync()
    {
        await using var local = Navigation(new ServiceCollection().AddRemoteLoaderResults(null));
        await using var remote = new ServiceCollection().AddRemoteLoaderResults(new Uri("https://api.example.test")).BuildServiceProvider();
        var texts = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: local, pages: Pages(new Counts()));
            await navigator.NavigateAsync("/saving/7");
            var result = DesktopFixture.Texts(window).Take(3).ToList();
            window.Close();
            return result;
        });

        Assert.Null(local.GetService<ILoaderResults>());
        Assert.Equal(["Shell", "section 1 on client", "item 7 #1 on client"], texts);
        Assert.Equal(new Uri("https://api.example.test"), Assert.IsType<RemoteLoaderResults>(remote.GetRequiredService<ILoaderResults>()).Http.BaseAddress);
    }

    private static PageDefinition[] Pages(Counts counts)
    {
        PageDefinition Page(string route, string source, LoaderDefinition? loader) =>
            new(route, source, static _ => null, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)]) { Loader = loader, DataContract = Contract };
        return
        [
            new PageDefinition("/saving/{id}", "Pages/saving/Page.axaml", Unreachable, [
                new LayoutDefinition("Pages/Layout.axaml", static _ => null),
                new LayoutDefinition("Pages/sections/Layout.axaml", Unreachable)
                {
                    Loader = Codec(
                        (services, _) => new LoaderOutcome(new SectionData($"section {counts.Next("section")} on {Origin(services)}")),
                        static (_, data) => new Section((SectionData)data!),
                        static data => ((SectionData)data).Path,
                        static text => new SectionData(text)),
                },
            ])
            {
                Loader = Codec(
                    (services, request) => new LoaderOutcome(new ItemData($"item {request.Value("id")}{(request.Query.TryGetValue("tab", out var tab) ? "/" + tab : string.Empty)} #{counts.Next("item")} on {Origin(services)}"))
                    {
                        Title = $"Item {request.Value("id")} · {Origin(services)}",
                    },
                    static (services, data) => new Saving((ItemData)data!, services.GetService<INavigator>()).Remember(),
                    static data => ((ItemData)data).Label,
                    static text => new ItemData(text)) with
                {
                    Receive = static (model, data) => ((Saving)model).Receive((ItemData)data!),
                },
                DataContract = Contract,
            },
            Page("/missing", "Pages/about/Page.axaml", Codec(static (_, _) => LoaderResult.NotFound(), static (_, _) => null, static _ => string.Empty, static _ => null)),
            Page("/moved", "Pages/about/Page.axaml", Codec(static (_, _) => LoaderResult.Redirect("/about"), static (_, _) => null, static _ => string.Empty, static _ => null)),
            Page("/vault", "Pages/about/Page.axaml", Codec(static (_, _) => new LoaderOutcome("secret"), static (_, _) => null, static data => (string)data, static text => text)) with { Access = [new PageAccess(null, null)] },
            Page("/about", "Pages/about/Page.axaml", null),
            Page("/keyed", Keyed, Counted(counts, "page")) with
            {
                Views =
                [
                    new ViewInstance(Keyed + ":0.2", 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Views.Saver), Loader = Reading(counts, "a", "issues") }),
                    new ViewInstance(Keyed + ":0.3", 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Views.Saver), Loader = Reading(counts, "b", "other") }),
                    new ViewInstance(Keyed + ":0.4", 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Views.Saver), Loader = Reading(counts, "c", null, saves: true) }),
                ],
            },
            Page("/live", Keyed, Counted(counts, "page")) with
            {
                Live = true,
                Views =
                [
                    new ViewInstance(Keyed + ":0.2", 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Views.Saver), Loader = Reading(counts, "a", "sales") }),
                    new ViewInstance(Keyed + ":0.3", 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Views.Saver), Loader = Reading(counts, "b", "other") }),
                    new ViewInstance(Keyed + ":0.4", 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Views.Saver), Loader = Reading(counts, "c", null) }),
                ],
            },
            Page("/clicks", "Pages/clicks/Page.axaml", Counted(counts, "page")) with
            {
                Views =
                [
                    new ViewInstance(Clicks + ":0.2", 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Views.Saver), Loader = Counted(counts, "first") }),
                    new ViewInstance(Clicks + ":0.3", 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Views.Saver), Loader = Counted(counts, "second") }),
                ],
            },
        ];
    }

    private const string Clicks = "Pages/clicks/Page.axaml";

    private const string Keyed = "Pages/keyed/Page.axaml";

    private static readonly ServerFunctionGroup Keys = new(
        typeof(object),
        "keys",
        "k1",
        [new ServerFunctionRoute("save", static (_, _, _) =>
        {
            DataKeys.Current?.Invalidate("issues");
            return Task.FromResult<object?>(null);
        }) { Keyed = true }],
        static http => http);

    private static LoaderDefinition Reading(Counts counts, string name, string? reads, bool saves = false) =>
        Codec(
            (services, _) =>
            {
                if (reads is not null)
                {
                    services.GetRequiredService<IDataKeys>().Read(reads);
                }

                return new LoaderOutcome(new ItemData($"{name} {counts.Next(name)} on {Origin(services)}"));
            },
            (services, data) => new Saving((ItemData)data!, null) { Change = saves ? () => SaveAsync(services) : null },
            static data => ((ItemData)data).Label,
            static text => new ItemData(text)) with
        {
            Receive = static (model, data) => ((Saving)model).Receive((ItemData)data!),
        };

    private static Task SaveAsync(IServiceProvider services) =>
        ServerFunctionCalls.CallAsync(services.GetRequiredService<HttpClient>(), "keys", "save", "k1", 0, static (writer, _) =>
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }, keyed: true, CancellationToken.None);

    private static LoaderDefinition Counted(Counts counts, string name) =>
        Codec(
            (services, _) => new LoaderOutcome(new ItemData($"{name} {counts.Next(name)} on {Origin(services)}")),
            static (_, data) => new Saving((ItemData)data!, null),
            static data => ((ItemData)data).Label,
            static text => new ItemData(text)) with
        {
            Receive = static (model, data) => ((Saving)model).Receive((ItemData)data!),
        };

    private static PageDefinition NotFound(string contract) =>
        new("/_not-found", "Pages/NotFound.axaml", static _ => null, [
            new LayoutDefinition("Pages/Layout.axaml", static _ => null),
            new LayoutDefinition("Pages/sections/Layout.axaml", Unreachable)
            {
                Loader = Codec(
                    static (services, request) => new LoaderOutcome(new SectionData($"lost {request.Path} on {Origin(services)}")),
                    static (_, data) => new Section((SectionData)data!),
                    static data => ((SectionData)data).Path,
                    static text => new SectionData(text)),
            },
        ])
        {
            DataContract = contract,
        };

    private static LoaderDefinition Codec(Func<IServiceProvider, PageRequest, LoaderOutcome> load, Func<IServiceProvider, object?, object?> create, Func<object, string> write, Func<string, object?> read) =>
        new((services, request, _) => ValueTask.FromResult(load(services, request)), create)
        {
            Write = (writer, data) => writer.WriteStringValue(data is null ? null : write(data)),
            Read = element => element.GetString() is { } text ? read(text) : null,
        };

    private static string Origin(IServiceProvider services) => services.GetService<Side>()?.Name ?? "client";

    private static async Task<WebApplication> ServerAsync(TaskCompletionSource? liveEnded = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new Side("server")).AddSingleton(new object()).AddSingleton<Aioros.Runtime.Caching.DataVersions>();
        builder.Services.AddScoped<PageNavigator>().AddScoped<INavigator>(static services => services.GetRequiredService<PageNavigator>());
        var app = builder.Build();
        app.Use(async (http, next) =>
        {
            await next(http);
            if (http.Request.Path == Aioros.Targets.Ssr.Live.LiveKeys.Path)
            {
                liveEnded?.TrySetResult();
            }
        });
        app.MapPageData(Pages(new Counts()), NotFound(NotFoundContract));
        app.MapLiveKeys();
        app.MapServerFunctions([Keys]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static ServiceProvider Client(WebApplication server, ConcurrentQueue<string> requests, TaskCompletionSource? liveOpened = null)
    {
        var http = new HttpClient(new Recorder(server.GetTestServer().CreateHandler(), requests, liveOpened)) { BaseAddress = server.GetTestServer().BaseAddress };
        return Navigation(new ServiceCollection().AddSingleton<ILoaderResults>(new RemoteLoaderResults(http)).AddSingleton(http));
    }

    private static ServiceProvider Navigation(IServiceCollection services) =>
        services
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(static provider => provider.GetRequiredService<PageNavigator>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");

    private sealed record Side(string Name);

    private sealed class Counts
    {
        private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.Ordinal);

        public int Next(string name) => _counts.AddOrUpdate(name, 1, static (_, count) => count + 1);
    }

    private sealed class LiveRefusal(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            request.RequestUri!.AbsolutePath == RemoteLiveKeys.Path ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)) : base.SendAsync(request, cancellationToken);
    }

    private sealed class Recorder(HttpMessageHandler inner, ConcurrentQueue<string> requests, TaskCompletionSource? liveOpened) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Enqueue(request.RequestUri!.PathAndQuery
                + (request.Headers.TryGetValues("X-Aioros-Contract", out var contract) ? " " + string.Join(",", contract) : string.Empty)
                + (request.Headers.TryGetValues(RemoteLoaderResults.ViewHeader, out var view) ? " " + view.Single() : string.Empty));
            var response = await base.SendAsync(request, cancellationToken);
            if (request.RequestUri.AbsolutePath == Aioros.Targets.Ssr.Live.LiveKeys.Path)
            {
                liveOpened?.TrySetResult();
            }

            return response;
        }
    }
}
