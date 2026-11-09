using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using Aioros.Functions;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Access;
using Aioros.Targets.Ssr.Data;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class RepeatedViewTests
{
    private const string Key = "Pages/board/Page.axaml:0.1.0.0";

    private static readonly RenderPlan Plan = new([], [
        new LiteralNode("<h1>"), new ValueNode(static model => ((Board)model!).Title), new LiteralNode("</h1>"),
        new RepeatNode(static model => ((Board)model!).Cards, [
            new ViewNode(1, Key, [
                new LiteralNode("<b>"), new ValueNode(static model => ((CardModel)model!).Title), new LiteralNode("</b><button name=\"_btn\" value=\""), IdNode.Instance, new LiteralNode("|x\">save</button>"),
            ], [new LiteralNode("<em>failed</em>")]),
        ]),
    ], []);

    [Fact]
    [Trait("AC", "SSR-85")]
    public async Task ABatchLoaderLoadsEveryItemOfTheListOnceAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var batch = new Batch();
        var loaded = await PageLoader.LoadAsync(Services(), Page(batch, "a", "b", "a", "gone"), PageRequest.For("/board"), token);
        var items = (ViewItems)loaded.Models[1]!;

        var logs = new Logs();
        var site = new Site { Engine = { Plan = Plan } };
        var failing = Site.Http();
        failing.RequestServices = new ServiceCollection().AddLogging(logging => logging.AddProvider(logs)).AddScoped<Marker>().BuildServiceProvider().CreateScope().ServiceProvider;
        await site.Handler.GetAsync(failing, Page(new Batch { Fails = true }, "a", "b"));

        var refused = new Batch();
        var anonymous = Site.Http();
        anonymous.RequestServices = new ServiceCollection().AddLogging().AddAuthorization().AddScoped<Marker>().AddSingleton<IViewAccess, AuthorizationViewAccess>().BuildServiceProvider();
        anonymous.User = new ClaimsPrincipal();
        await new Site { Engine = { Plan = Plan } }.Handler.GetAsync(anonymous, Page(refused, ["a"], "admin"));

        Assert.Equal([["a", "b", "gone"]], batch.Inputs);
        Assert.Equal(["page", "batch"], batch.Order);
        Assert.Equal(2, batch.Scopes.Distinct().Count());
        Assert.Equal(["a", "b", "gone"], items.Items.Select(item => item.Key));
        Assert.Equal(("card a", "card b"), (((CardModel)items.Model("a")!).Title, ((CardModel)items.Model("b")!).Title));
        Assert.Same(ViewState.Hidden, items.Model("gone"));
        Assert.Equal(200, failing.Response.StatusCode);
        Assert.Contains($"<div data-aioros-view=\"{Key}[a]\" data-view-error role=\"alert\" style=\"display:contents\"><em>failed</em></div>", Site.Body(failing), StringComparison.Ordinal);
        Assert.Contains(Key, Assert.Single(logs.Errors), StringComparison.Ordinal);
        Assert.Empty(refused.Inputs);
        Assert.Equal(200, anonymous.Response.StatusCode);
        Assert.DoesNotContain("<div data-aioros-view", Site.Body(anonymous), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-86")]
    public async Task ALoaderForOneInstanceRunsOncePerItemAsync()
    {
        var runs = new ConcurrentQueue<Marker>();
        var count = 0;
        var loader = new LoaderDefinition(
            (services, _, _) =>
            {
                runs.Enqueue(services.GetRequiredService<Marker>());
                return ValueTask.FromResult(new LoaderOutcome("run " + Interlocked.Increment(ref count)));
            },
            static (_, data) => new CardModel((string)data!));

        var loaded = await PageLoader.LoadAsync(Services(), Page(loader, ["a", "b", "c"]), PageRequest.For("/board"), TestContext.Current.CancellationToken);
        var items = (ViewItems)loaded.Models[1]!;

        Assert.Equal(3, runs.Count);
        Assert.Equal(3, runs.Distinct().Count());
        Assert.Equal(["run 1", "run 2", "run 3"], items.Items.Select(item => ((CardModel)item.Model!).Title).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("AC", "SSR-87")]
    public async Task AnItemsCommandRefreshesThatItemThroughItsBatchLoaderAsync()
    {
        var one = new Batch();
        var site = new Site { Engine = { Plan = Plan } };
        CardModel? commanded = null;
        site.Engine.Command = models =>
        {
            commanded = (CardModel)((ViewItems)models[1]!).Model("c")!;
            commanded.Saved = "commanded";
            return ValueTask.CompletedTask;
        };
        var item = Post("_btn=1-c.0%7Cx");
        await site.Handler.PostAsync(item, Page(one, "a", "b", "c"));

        var all = new Batch();
        var keyed = new Site { Engine = { Plan = Plan } };
        keyed.Engine.Command = _ =>
        {
            DataKeys.Current?.Invalidate("cards");
            return ValueTask.CompletedTask;
        };
        var every = Post("_btn=1-c.0%7Cx");
        await keyed.Handler.PostAsync(every, Page(all, "a", "b", "c"));

        Assert.Equal([["a", "b", "c"], ["c"]], one.Inputs);
        Assert.Equal(($"{Key}[c]", "text/html; charset=utf-8"), (item.Response.Headers[PageHandler.ViewHeader].ToString(), item.Response.ContentType));
        Assert.Equal($"<div data-aioros-view=\"{Key}[c]\" style=\"display:contents\"><b>card c</b><button name=\"_btn\" value=\"1-c.0|x\">save</button></div>", Site.Body(item));
        Assert.Equal([["a", "b", "c"], ["a", "b", "c"]], all.Inputs);
        Assert.Equal($"{Key}[a], {Key}[b], {Key}[c]", every.Response.Headers[PageHandler.ViewHeader].ToString());
        Assert.StartsWith($"<div data-aioros-view=\"{Key}[a]\"", Site.Body(every), StringComparison.Ordinal);
        Assert.Equal(3, Site.Body(every).Split("<div data-aioros-view").Length - 1);
        Assert.Equal((3, 1), (one.Created, one.Received));
        Assert.Equal(("card c", "commanded"), (commanded!.Title, commanded.Saved));
    }

    [Fact]
    [Trait("AC", "SSR-88")]
    public async Task DataRequestsCarryEachItemOfARepeatedViewAsync()
    {
        var batch = new Batch();
        var page = Page(batch, "a", "b", "gone") with { DataContract = "c1" };

        var whole = await DataAsync(page, null);
        var single = await DataAsync(page, Key + "[b]");

        Assert.Equal(
            """{"type":"data","segments":[{"data":"board"}],"views":{"KEY":{"items":[{"key":"a","data":"card a"},{"key":"b","data":"card b"},{"key":"gone","state":"hidden"}],"keys":["cards"]}}}""".Replace("KEY", Key, StringComparison.Ordinal),
            whole);
        Assert.Equal(
            """{"type":"data","segments":[],"views":{"KEY":{"items":[{"key":"b","data":"card b"}],"keys":["cards"]}}}""".Replace("KEY", Key, StringComparison.Ordinal),
            single);
        Assert.Equal([["a", "b", "gone"], ["b"]], batch.Inputs);
    }

    private static async Task<string> DataAsync(PageDefinition page, string? view)
    {
        var http = new DefaultHttpContext { RequestServices = Services().CreateScope().ServiceProvider };
        http.Request.Path = "/_data/board";
        http.Request.Headers[ServerFunctionGroup.ContractHeader] = page.DataContract;
        if (view is not null)
        {
            http.Request.Headers[RemoteLoaderResults.ViewHeader] = view;
        }

        http.Response.Body = new MemoryStream();
        await PageDataEndpoints.ServeAsync(http, page);
        return Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
    }

    private static DefaultHttpContext Post(string form)
    {
        var http = Site.Http();
        http.RequestServices = Services().CreateScope().ServiceProvider;
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));
        http.Request.Headers[DocumentParts.NavigationHeader] = "1";
        return http;
    }

    private static ServiceProvider Services() => new ServiceCollection().AddScoped<Marker>().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    private static PageDefinition Page(Batch batch, params string[] cards) => Page(batch.Loader, cards, batch: batch);

    private static PageDefinition Page(Batch batch, string[] cards, string role) => Page(batch.Loader, cards, role, batch);

    private static PageDefinition Page(LoaderDefinition view, string[] cards, string? role = null, Batch? batch = null) =>
        new("/board", "Pages/board/Page.axaml", _ => null, [])
        {
            Loader = new LoaderDefinition(
                (services, _, _) =>
                {
                    batch?.Order.Enqueue("page");
                    batch?.Scopes.Enqueue(services.GetRequiredService<Marker>());
                    return ValueTask.FromResult(new LoaderOutcome(new Board("board", [.. cards.Select(id => new Card(id))])));
                },
                static (_, data) => data)
            {
                Write = static (writer, data) => writer.WriteStringValue(((Board)data!).Title),
            },
            Views =
            [
                new ViewInstance(Key, 0, new ViewDefinition("Views/Card.axaml", _ => null) { Loader = view, Access = role is null ? [] : [new PageAccess(null, role)] })
                {
                    List = new ViewList(0, static model => ((Board)model!).Cards) { Key = static item => ((Card)item!).Id, Parse = static text => text },
                },
            ],
        };

    private sealed record Board(string Title, Card[] Cards);

    private sealed record Card(string Id);

    private sealed class CardModel(string title)
    {
        public string Title { get; set; } = title;

        public string Saved { get; set; } = string.Empty;
    }

    private sealed class Marker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class Batch
    {
        public ConcurrentQueue<string[]> Inputs { get; } = new();

        public ConcurrentQueue<string> Order { get; } = new();

        public ConcurrentQueue<Marker> Scopes { get; } = new();

        public bool Fails { get; init; }

        public int Created => Volatile.Read(ref _created);

        public int Received => Volatile.Read(ref _received);

        private int _created;

        private int _received;

        public LoaderDefinition Loader => new(
            static (_, _, _) => throw new InvalidOperationException("A batch loader loads many."),
            (_, data) =>
            {
                Interlocked.Increment(ref _created);
                return new CardModel((string)data!);
            })
        {
            LoadManyAsync = LoadAsync,
            Receive = (model, data) =>
            {
                Interlocked.Increment(ref _received);
                ((CardModel)model).Title = (string)data!;
            },
            Write = static (writer, data) => writer.WriteStringValue((string)data!),
        };

        private ValueTask<IReadOnlyList<LoaderOutcome>> LoadAsync(IServiceProvider services, IReadOnlyList<object?> inputs, PageRequest request, CancellationToken cancellationToken)
        {
            Inputs.Enqueue([.. inputs.Cast<string>()]);
            Order.Enqueue("batch");
            Scopes.Enqueue(services.GetRequiredService<Marker>());
            ((IDataKeys?)services.GetService(typeof(IDataKeys)))?.Read("cards");
            if (Fails)
            {
                throw new InvalidOperationException("The cards are down.");
            }

            return ValueTask.FromResult<IReadOnlyList<LoaderOutcome>>([.. inputs.Cast<string>().Select(id => id == "gone" ? LoaderResult.NotFound() : new LoaderOutcome("card " + id))]);
        }
    }

    private sealed class Logs : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<string> Errors { get; } = new();

        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                Errors.Enqueue(formatter(state, exception));
            }
        }

        public void Dispose()
        {
        }
    }
}
