using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class PageDataTests
{
    private const string Contract = "c1";

    [Fact]
    [Trait("AC", "SSR-56")]
    public async Task DataRequestsAnswerEverySegmentsLoaderResultsAsync()
    {
        var created = new ConcurrentQueue<string>();
        var page = new PageDefinition("/issues/{id}", "Pages/issues/[id]/Page.axaml", Uncreated(created), [
            new LayoutDefinition("Pages/Layout.axaml", Uncreated(created)) { Loader = Loader((_, _) => new LoaderOutcome("shell") { Title = "Shell", Metadata = new PageMetadata("outer") }, created) },
            new LayoutDefinition("Pages/issues/Layout.axaml", Uncreated(created)),
        ])
        {
            Loader = Loader((request, _) => new LoaderOutcome("issue " + request.Value("id")) { Title = "Issue", Metadata = new PageMetadata("inner", "/issues/7", null, "article") }, created),
            DataContract = Contract,
        };
        await using var app = await StartAsync(null, page);

        using var response = await GetAsync(app, "/_data/issues/7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(
            """{"type":"data","segments":[{"data":"shell","title":"Shell","metadata":{"description":"outer","canonical":null,"image":null,"type":"website","indexable":true}},null,{"data":"issue 7","title":"Issue","metadata":{"description":"inner","canonical":"/issues/7","image":null,"type":"article","indexable":true}}]}""",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["loaded", "loaded"], created);
    }

    [Fact]
    [Trait("AC", "SSR-57")]
    public async Task DataRequestsCarryTheRouteQueryAndUserToTheLoaderAsync()
    {
        PageRequest? seen = null;
        var page = new PageDefinition("/issues/{id}", "Pages/issues/[id]/Page.axaml", _ => null, [])
        {
            Loader = Loader((request, _) => { seen = request; return new LoaderOutcome("issue"); }, []),
            DataContract = Contract,
        };
        await using var app = await StartAsync(null, page);

        using var response = await GetAsync(app, "/_data/issues/7?tab=open&as=member");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/issues/7", seen?.Path);
        Assert.Equal("7", seen?.RouteValues["id"]);
        Assert.Equal("open", seen?.Query["tab"]);
        Assert.Equal("member", seen?.User.Identity?.Name);
    }

    [Fact]
    [Trait("AC", "SSR-58")]
    public async Task NotFoundAndRedirectsAreAnsweredForTheClientAsync()
    {
        static PageDefinition Stopping(string route, Func<IServiceProvider, LoaderOutcome> layout, Func<IServiceProvider, LoaderOutcome> page) =>
            new(route, "Pages" + route + "/Page.axaml", _ => null, [new LayoutDefinition("Pages/Layout.axaml", _ => null) { Loader = Loader((_, services) => layout(services), []) }])
            {
                Loader = Loader((_, services) => page(services), []),
                DataContract = Contract,
            };
        await using var app = await StartAsync(
            null,
            Stopping("/gone", _ => new LoaderOutcome("shell"), _ => LoaderResult.NotFound()),
            Stopping("/hollow", _ => LoaderResult.NotFound(), _ => new LoaderOutcome("page")),
            Stopping("/moved/here", _ => LoaderResult.NotFound(), _ => LoaderResult.Redirect("../items/5")),
            Stopping("/detour", _ => new LoaderOutcome("shell"), services =>
            {
                _ = services.GetRequiredService<INavigator>().NavigateAsync(new NavigationRequest("/items/9"), CancellationToken.None).AsTask();
                return LoaderResult.NotFound();
            }));

        var answers = new List<(HttpStatusCode, string, bool)>();
        foreach (var path in new[] { "/_data/gone", "/_data/hollow", "/_data/nowhere/at/all", "/_data/moved/here", "/_data/detour" })
        {
            using var response = await GetAsync(app, path);
            answers.Add((response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), response.Headers.Location is null));
        }

        Assert.Equal(
            [
                (HttpStatusCode.NotFound, """{"type":"notFound"}""", true),
                (HttpStatusCode.NotFound, """{"type":"notFound"}""", true),
                (HttpStatusCode.NotFound, """{"type":"notFound"}""", true),
                (HttpStatusCode.OK, """{"type":"redirect","location":"/items/5"}""", true),
                (HttpStatusCode.OK, """{"type":"redirect","location":"/items/9"}""", true),
            ],
            answers);
    }

    [Fact]
    [Trait("AC", "SSR-59")]
    public async Task DataRequestsFollowThePagesAccessRulesAsync()
    {
        var loads = 0;
        PageDefinition Page(string route) => new(route, "Pages" + route + "/Page.axaml", _ => null, [])
        {
            Loader = Loader((_, _) => { Interlocked.Increment(ref loads); return new LoaderOutcome("secret"); }, []),
            DataContract = Contract,
        };
        await using var app = await StartAsync(
            new PageConventions().AuthorizeFolder("/members").AllowAnonymousToPage("/members/welcome"),
            Page("/vault") with { Access = [new PageAccess(null, null), new PageAccess("owners", null)] },
            Page("/members/list"),
            Page("/members/welcome"),
            Page("/open") with { Access = [], AllowAnonymous = true });

        using var anonymous = await GetAsync(app, "/_data/vault");
        using var folder = await GetAsync(app, "/_data/members/list");
        var refused = loads;
        using var member = await GetAsync(app, "/_data/vault?as=member");
        var forbidden = loads;
        using var owner = await GetAsync(app, "/_data/vault?as=owner");
        using var welcome = await GetAsync(app, "/_data/members/welcome");
        using var open = await GetAsync(app, "/_data/open");

        Assert.Equal((HttpStatusCode.Unauthorized, true), (anonymous.StatusCode, anonymous.Headers.Location is null));
        Assert.Equal((HttpStatusCode.Unauthorized, true), (folder.StatusCode, folder.Headers.Location is null));
        Assert.Equal((HttpStatusCode.Forbidden, true), (member.StatusCode, member.Headers.Location is null));
        Assert.Equal((0, 0), (refused, forbidden));
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, welcome.StatusCode);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
    }

    [Fact]
    [Trait("AC", "SSR-60")]
    public async Task AnotherDataContractIsAnsweredWithUpgradeRequiredAsync()
    {
        var loads = 0;
        var page = new PageDefinition("/vault", "Pages/vault/Page.axaml", _ => null, [])
        {
            Loader = Loader((_, _) => { Interlocked.Increment(ref loads); return new LoaderOutcome("secret"); }, []),
            DataContract = Contract,
            Access = [new PageAccess(null, null)],
        };
        await using var app = await StartAsync(null, page);

        using var other = await GetAsync(app, "/_data/vault", "c0");
        using var missing = await GetAsync(app, "/_data/vault", null);

        Assert.Equal(HttpStatusCode.UpgradeRequired, other.StatusCode);
        Assert.Equal(HttpStatusCode.UpgradeRequired, missing.StatusCode);
        Assert.Equal([Contract], other.Headers.GetValues("X-Aioros-Contract"));
        Assert.Equal([Contract], missing.Headers.GetValues("X-Aioros-Contract"));
        Assert.Equal(0, loads);
    }

    [Fact]
    [Trait("AC", "SSR-61")]
    public async Task LoaderFailuresAnswerProblemDetailsWithoutTheirDetailsAsync()
    {
        var logs = new Logs();
        var page = new PageDefinition("/broken", "Pages/broken/Page.axaml", _ => null, [])
        {
            Loader = Loader((_, _) => throw new InvalidOperationException("The database password is hunter2."), []),
            DataContract = Contract,
        };
        await using var app = await StartAsync(null, [page], logs);

        using var response = await GetAsync(app, "/_data/broken");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("The page's data could not be loaded.", problem.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("hunter2", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.Contains(logs.Errors, error => error.Message.Contains("hunter2", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "SSR-72")]
    public async Task DataRequestsCarryEachViewByItsKeyAsync()
    {
        static ViewInstance View(int position, LoaderDefinition? loader, string? role = null) =>
            new("Pages/inbox/Page.axaml:" + position, 0, new ViewDefinition("Views/V" + position + ".axaml", _ => null) { Loader = loader, Access = role is null ? [] : [new PageAccess(null, role)] });
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => null, [])
        {
            Loader = Loader((_, _) => new LoaderOutcome("page"), []),
            Views =
            [
                View(0, Loader((_, _) => new LoaderOutcome("recent"), [])),
                View(1, Loader((_, _) => throw new InvalidOperationException("The feed is down."), [])),
                View(2, Loader((_, _) => new LoaderOutcome("admin"), []), "admin"),
                View(3, null),
            ],
            DataContract = Contract,
        };
        await using var app = await StartAsync(null, page);

        using var response = await GetAsync(app, "/_data/inbox?as=member");
        var read = await new RemoteLoaderResults(app.GetTestClient()).LoadAsync(
            page,
            PageRequest.For("/inbox") with { Query = System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("as", "member") },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """{"type":"data","segments":[{"data":"page"}],"views":{"Pages/inbox/Page.axaml:0":{"data":"recent"},"Pages/inbox/Page.axaml:1":{"state":"failed"},"Pages/inbox/Page.axaml:2":{"state":"hidden"}}}""",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["page", "recent", Aioros.Runtime.Rendering.ViewState.Failed, Aioros.Runtime.Rendering.ViewState.Hidden, null], read.Outcomes.Select(outcome => outcome.Data));
    }

    [Fact]
    [Trait("AC", "SSR-77")]
    public async Task DataRequestsForOneViewRunOnlyItsLoaderAsync()
    {
        var runs = new ConcurrentQueue<string>();
        LoaderDefinition Counted(string name, Func<LoaderOutcome> load) => Loader((_, _) =>
        {
            runs.Enqueue(name);
            return load();
        }, []);
        static ViewInstance View(int position, LoaderDefinition? loader, string? role = null) =>
            new("Pages/inbox/Page.axaml:" + position, 0, new ViewDefinition("Views/V" + position + ".axaml", _ => null) { Loader = loader, Access = role is null ? [] : [new PageAccess(null, role)] });
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => null, [])
        {
            Loader = Counted("page", static () => new LoaderOutcome("page")),
            Views =
            [
                View(0, Counted("recent", static () => new LoaderOutcome("recent"))),
                View(1, Counted("broken", static () => throw new InvalidOperationException("The feed is down."))),
                View(2, Counted("admin", static () => new LoaderOutcome("admin")), "admin"),
                View(3, null),
            ],
            DataContract = Contract,
        };
        await using var app = await StartAsync(null, page);

        async Task<(HttpStatusCode Status, string Body)> ViewAsync(string key, string? contract = Contract)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/_data/inbox?as=member", UriKind.Relative));
            request.Headers.Add(RemoteLoaderResults.ViewHeader, key);
            if (contract is not null)
            {
                request.Headers.Add("X-Aioros-Contract", contract);
            }

            using var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        var recent = await ViewAsync("Pages/inbox/Page.axaml:0");
        var broken = await ViewAsync("Pages/inbox/Page.axaml:1");
        var admin = await ViewAsync("Pages/inbox/Page.axaml:2");
        var plain = await ViewAsync("Pages/inbox/Page.axaml:3");
        var unknown = await ViewAsync("Pages/inbox/Page.axaml:9");
        var outdated = await ViewAsync("Pages/inbox/Page.axaml:0", "c0");
        var read = await new RemoteLoaderResults(app.GetTestClient()).LoadViewAsync(page, PageRequest.For("/inbox"), 0, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.OK, """{"type":"data","segments":[],"views":{"Pages/inbox/Page.axaml:0":{"data":"recent"}}}"""), recent);
        Assert.Equal((HttpStatusCode.OK, """{"type":"data","segments":[],"views":{"Pages/inbox/Page.axaml:1":{"state":"failed"}}}"""), broken);
        Assert.Equal((HttpStatusCode.OK, """{"type":"data","segments":[],"views":{"Pages/inbox/Page.axaml:2":{"state":"hidden"}}}"""), admin);
        Assert.Equal(HttpStatusCode.NotFound, plain.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal(HttpStatusCode.UpgradeRequired, outdated.Status);
        Assert.Equal("recent", read.Outcome.Data);
        Assert.Equal(["recent", "broken", "recent"], runs);
    }

    [Fact]
    [Trait("AC", "SSR-83")]
    public async Task DataAnswersCarryTheKeysEachLoaderReadAsync()
    {
        var created = new ConcurrentQueue<string>();
        static LoaderOutcome Reading(IServiceProvider services, string data, params string[] keys)
        {
            foreach (var key in keys)
            {
                services.GetRequiredService<Aioros.Functions.IDataKeys>().Read(key);
            }

            return new LoaderOutcome(data);
        }

        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => null, [new LayoutDefinition("Pages/Layout.axaml", _ => null) { Loader = Loader((_, services) => Reading(services, "layout"), created) }])
        {
            Loader = Loader((_, services) => Reading(services, "page", "issues", "issue:7"), created),
            Views =
            [
                new ViewInstance("Pages/inbox/Page.axaml:0", 0, new ViewDefinition("Views/V.axaml", _ => null) { Loader = Loader((_, services) => Reading(services, "recent", "recent"), created) }),
                new ViewInstance("Pages/inbox/Page.axaml:1", 0, new ViewDefinition("Views/W.axaml", _ => null) { Loader = Loader((_, services) => Reading(services, "plain"), created) }),
            ],
            DataContract = Contract,
        };
        await using var app = await StartAsync(null, page);

        using var whole = await GetAsync(app, "/_data/inbox");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/_data/inbox", UriKind.Relative));
        request.Headers.Add("X-Aioros-Contract", Contract);
        request.Headers.Add(RemoteLoaderResults.ViewHeader, "Pages/inbox/Page.axaml:0");
        using var one = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
        var read = await new RemoteLoaderResults(app.GetTestClient()).LoadAsync(page, PageRequest.For("/inbox"), TestContext.Current.CancellationToken);

        Assert.Equal(
            """{"type":"data","segments":[{"data":"layout"},{"data":"page","keys":["issues","issue:7"]}],"views":{"Pages/inbox/Page.axaml:0":{"data":"recent","keys":["recent"]},"Pages/inbox/Page.axaml:1":{"data":"plain"}}}""",
            await whole.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            """{"type":"data","segments":[],"views":{"Pages/inbox/Page.axaml:0":{"data":"recent","keys":["recent"]}}}""",
            await one.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["", "issues,issue:7", "recent", ""], read.Keys.Select(static keys => string.Join(",", keys)));
    }

    private static Func<IServiceProvider, object?> Uncreated(ConcurrentQueue<string> created) => _ =>
    {
        created.Enqueue("created");
        return null;
    };

    private static LoaderDefinition Loader(Func<PageRequest, IServiceProvider, LoaderOutcome> load, ConcurrentQueue<string> created) =>
        new((services, request, _) =>
        {
            var outcome = load(request, services);
            created.Enqueue("loaded");
            return ValueTask.FromResult(outcome);
        }, (_, _) => throw new InvalidOperationException("A data request creates no view model."))
        {
            Write = static (writer, data) => writer.WriteStringValue((string?)data),
            Read = static element => element.GetString(),
        };

    private static async Task<HttpResponseMessage> GetAsync(WebApplication app, string path, string? contract = Contract)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (contract is not null)
        {
            request.Headers.Add("X-Aioros-Contract", contract);
        }

        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<WebApplication> StartAsync(PageConventions? conventions, params PageDefinition[] pages) => StartAsync(conventions, pages, null);

    private static async Task<WebApplication> StartAsync(PageConventions? conventions, PageDefinition[] pages, Logs? logs)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (logs is not null)
        {
            builder.Logging.AddProvider(logs);
        }

        var site = new Site();
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options);
        builder.Services.AddScoped<PageNavigator>().AddScoped<INavigator>(services => services.GetRequiredService<PageNavigator>());
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/denied";
            options.ForwardAuthenticate = "query";
        }).AddScheme<AuthenticationSchemeOptions, QueryUser>("query", null);
        builder.Services.AddAuthorizationBuilder().AddPolicy("owners", policy => policy.RequireRole("owner"));
        var app = builder.Build();
        app.MapAiorosPages(pages, conventions);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class QueryUser(Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Request.Query["as"].ToString() is { Length: > 0 } role
                ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, role), new Claim(ClaimTypes.Role, role)], "query")), "query"))
                : AuthenticateResult.NoResult());
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
                Errors.Enqueue((formatter(state, exception) + " " + exception?.Message, exception));
            }
        }

        public void Dispose()
        {
        }
    }
}
