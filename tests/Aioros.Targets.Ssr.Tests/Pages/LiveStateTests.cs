using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Live;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed partial class LiveStateTests
{
    private sealed class Stream(SignalStream body, CancellationTokenSource abort, Task live) : IAsyncDisposable
    {
        public SignalStream Body => body;

        public async ValueTask DisposeAsync()
        {
            await abort.CancelAsync();
            await live;
            abort.Dispose();
        }
    }

    private static (Site Site, PageDefinition Page, List<Model> Models) Live()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<p>"), new ValueNode(static model => ((Model)model!).Title), new LiteralNode("</p>")], []);
        site.Engine.Command = static models =>
        {
            ((Model)models[0]!).Title = "Pressed";
            return ValueTask.CompletedTask;
        };
        var models = new List<Model>();
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ =>
        {
            var model = new Model("Start");
            models.Add(model);
            return model;
        }, [], Live: true);
        return (site, page, models);
    }

    private static async Task<(Stream Stream, string Name)> OpenAsync(Site site, PageDefinition page, string? user = null)
    {
        var body = new SignalStream();
        var abort = new CancellationTokenSource();
        var http = Site.Http(body: body);
        http.RequestAborted = abort.Token;
        http.Request.Path = "/inbox/live";
        if (user is not null)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, user)], "test"));
        }

        var live = LiveUpdates.StreamAsync(http, page, site.Handler);
        var opening = await body.Writes.ReadAsync(TestContext.Current.CancellationToken);
        return (new Stream(body, abort, live), Named().Match(opening).Groups[1].Value);
    }

    private static async Task<HttpContext> PostAsync(Site site, PageDefinition page, string? name, string? user = null)
    {
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("_btn=0%7C"));
        if (name is not null)
        {
            http.Request.Headers[LiveStates.Header] = name;
        }

        if (user is not null)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, user)], "test"));
        }

        await site.Handler.PostAsync(http, page);
        return http;
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-259")]
    public async Task APostNamingItsConnectionRunsOnThatConnectionsModelsAsync()
    {
        var (site, page, models) = Live();
        var (first, name) = await OpenAsync(site, page);
        await using var firstStream = first;
        var (second, _) = await OpenAsync(site, page);
        await using var secondStream = second;

        var answer = await PostAsync(site, page, name);
        var pushed = await first.Body.Writes.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Matches("^[0-9a-f]{32}$", name);
        Assert.Equal(["Pressed", "Start"], models.Select(static model => model.Title));
        Assert.Contains("<p>Pressed</p>", pushed, StringComparison.Ordinal);
        Assert.Equal("1", answer.Response.Headers[LiveStates.KeptHeader].ToString());
        Assert.False(second.Body.Writes.TryRead(out _));
    }

    [Fact]
    [Trait("AC", "SSR-260")]
    public async Task APostNamingNoConnectionOfItsOwnLoadsItsModelsAnewAsync()
    {
        var (site, page, models) = Live();
        var other = page with { Route = "/other", Source = "Pages/other/Page.axaml" };
        var (stream, name) = await OpenAsync(site, page, user: "ada");
        var answers = new List<HttpContext>
        {
            await PostAsync(site, page, null, "ada"),
            await PostAsync(site, page, "0123456789abcdef0123456789abcdef", "ada"),
            await PostAsync(site, other, name, "ada"),
            await PostAsync(site, page, name, "grace"),
        };
        await stream.DisposeAsync();
        answers.Add(await PostAsync(site, page, name, "ada"));

        Assert.Equal("Start", models[0].Title);
        Assert.Equal(6, models.Count);
        Assert.All(answers, static answer => Assert.False(answer.Response.Headers.ContainsKey(LiveStates.KeptHeader)));
        Assert.False(stream.Body.Writes.TryRead(out _));
        Assert.Equal(0, site.Handler.Live.Count);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-282")]
    public async Task PostsNamingOneConnectionRunOneAtATimeAsync()
    {
        var (site, page, _) = Live();
        var gate = new TaskCompletionSource();
        var entered = new TaskCompletionSource();
        var order = new List<string>();
        var presses = 0;
        site.Engine.Command = async models =>
        {
            if (Interlocked.Increment(ref presses) == 1)
            {
                order.Add("first");
                entered.SetResult();
                await gate.Task;
                ((Model)models[0]!).Title = "First";
                order.Add("first answered");
                return;
            }

            order.Add("second");
            ((Model)models[0]!).Title = "Second";
        };
        var (stream, name) = await OpenAsync(site, page);
        await using var live = stream;

        var first = PostAsync(site, page, name);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = PostAsync(site, page, name);
        gate.SetResult();
        var answers = await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first", "first answered", "second"], order);
        Assert.All(answers, static answer => Assert.Equal("1", answer.Response.Headers[LiveStates.KeptHeader].ToString()));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-283")]
    public async Task APostsReloadedPageBecomesItsConnectionsAsync()
    {
        var (site, _, _) = Live();
        var runs = 0;
        var models = new List<Model>();
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", static _ => throw new InvalidOperationException("A model with a loader is created from its data."), [], Live: true)
        {
            Loader = new LoaderDefinition(
                (_, _, _) => ValueTask.FromResult(new LoaderOutcome($"run {++runs}")),
                (_, data) =>
                {
                    var model = new Model((string)data!);
                    models.Add(model);
                    return model;
                }),
        };
        var (stream, name) = await OpenAsync(site, page);
        await using var live = stream;

        var answer = await PostAsync(site, page, name);
        while (live.Body.Writes.TryRead(out _))
        {
        }

        models[^1].Title = "Changed";
        var pushed = await live.Body.Writes.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("1", answer.Response.Headers[LiveStates.KeptHeader].ToString());
        Assert.Equal(["Pressed", "Changed"], models.Select(static model => model.Title));
        Assert.Contains("<p>Changed</p>", pushed, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-290")]
    public async Task APostOnAConnectionFollowsTheNavigationItsCommandAsksAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<p>"), new ValueNode(static model => ((Navigating)model!).Title), new LiteralNode("</p>")], []);
        site.Engine.Command = static async models =>
        {
            var model = (Navigating)models[0]!;
            if (model.Moves)
            {
                model.Moves = false;
                await model.Navigator.NavigateAsync(new Aioros.Navigation.NavigationRequest("?range=7d"), CancellationToken.None);
            }
        };
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", static services => new Navigating((Aioros.Navigation.INavigator)services.GetService(typeof(Aioros.Navigation.INavigator))!), [], Live: true);
        var body = new SignalStream();
        using var abort = new CancellationTokenSource();
        var stream = Site.Http(body: body, navigator: true);
        stream.RequestAborted = abort.Token;
        stream.Request.Path = "/inbox/live";
        var live = LiveUpdates.StreamAsync(stream, page, site.Handler);
        var name = Named().Match(await body.Writes.ReadAsync(TestContext.Current.CancellationToken)).Groups[1].Value;
        async Task<HttpContext> PostOnAsync()
        {
            var http = Site.Http(navigator: true);
            http.Request.Method = "POST";
            http.Request.ContentType = "application/x-www-form-urlencoded";
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("_btn=0%7C"));
            http.Request.Headers[LiveStates.Header] = name;
            await site.Handler.PostAsync(http, page);
            return http;
        }

        var moved = await PostOnAsync();
        var stayed = await PostOnAsync();
        await abort.CancelAsync();
        await live;

        Assert.Equal((StatusCodes.Status303SeeOther, "/inbox?range=7d"), (moved.Response.StatusCode, moved.Response.Headers.Location.ToString()));
        Assert.Equal(StatusCodes.Status200OK, stayed.Response.StatusCode);
        Assert.Empty(stayed.Response.Headers.Location.ToString());
    }

    private sealed class Navigating(Aioros.Navigation.INavigator navigator)
    {
        public Aioros.Navigation.INavigator Navigator { get; } = navigator;

        public bool Moves { get; set; } = true;

        public string Title { get; } = "Inbox";
    }

    [GeneratedRegex("event: connection\ndata: ([0-9a-f]+)\n")]
    private static partial Regex Named();
}
