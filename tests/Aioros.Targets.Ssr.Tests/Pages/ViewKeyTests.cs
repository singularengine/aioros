using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using Aioros.Functions;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class ViewKeyTests
{
    private const string Host = "Pages/board/Page.axaml";
    private static readonly string[] Names = ["a", "b", "c", "d", "e"];

    [Fact]
    [Trait("AC", "SSR-78")]
    public async Task EachLoaderKeepsTheKeysItReadAsync()
    {
        var counts = new Counts();
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();

        var loaded = await PageLoader.LoadAsync(scope.ServiceProvider, Board(counts, pageReads: null), PageRequest.For("/board"), TestContext.Current.CancellationToken);

        Assert.Equal(7, loaded.Keys.Length);
        Assert.Equal(["", "", "issues", "other", "", "issue:7", "issue:8"], loaded.Keys.Select(static keys => string.Join(",", keys)));
    }

    [Fact]
    [Trait("AC", "SSR-79")]
    public async Task ACommandRerunsTheViewsWhoseKeysItInvalidatedAsync()
    {
        var views = await PostAsync(static calls => calls.Save(7));
        var page = await PostAsync(static calls => calls.Save(7), pageReads: "issues");

        Assert.Equal("layout 1,page 1,a 1,b 1,c 1,d 1,e 1,a 2,c 2,d 2", views.Loads);
        Assert.Equal(("a 2", "b 1", "c 2", "d 2", "e 1"), Titles(views.Models));
        Assert.Equal("layout 1,page 1,a 1,b 1,c 1,d 1,e 1,layout 2,page 2,a 2,c 2,d 2", page.Loads);
        Assert.Equal("page 2", ((Card)page.Models[1]!).Title);
        Assert.False(page.Http.Response.Headers.ContainsKey(PageHandler.ViewHeader));
        Assert.Contains("<title>", Site.Body(page.Http), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-80")]
    public async Task UnknownKeysRefreshTheWholePageAndNoCallOnlyItsViewAsync()
    {
        var unknown = await PostAsync(static calls => calls.Untracked());
        var none = await PostAsync(static _ => { });
        var failed = await PostAsync(static calls =>
        {
            calls.Save(7);
            throw new InvalidOperationException("The save failed.");
        });
        var invalid = await PostAsync(static calls => calls.Save(7), invalid: true);

        Assert.Equal("layout 1,page 1,a 1,b 1,c 1,d 1,e 1,layout 2,page 2,a 2,b 2,c 2,d 2,e 2", unknown.Loads);
        Assert.Contains("<title>", Site.Body(unknown.Http), StringComparison.Ordinal);
        Assert.Equal("layout 1,page 1,a 1,b 1,c 1,d 1,e 1,c 2", none.Loads);
        Assert.Equal("layout 1,page 1,a 1,b 1,c 1,d 1,e 1", failed.Loads);
        Assert.Equal("layout 1,page 1,a 1,b 1,c 1,d 1,e 1", invalid.Loads);
    }

    [Fact]
    [Trait("AC", "SSR-81")]
    public async Task TheScriptGetsEveryRefreshedRegionInOneAnswerAsync()
    {
        var script = await PostAsync(static calls => calls.Save(7), navigation: true);
        var plain = await PostAsync(static calls => calls.Save(7));
        var whole = new ArrayBufferWriter<byte>();
        Plan.RenderBody(whole, script.Models);
        var page = Encoding.UTF8.GetString(whole.WrittenSpan);
        string Region(string name)
        {
            var start = page.IndexOf($"<div data-aioros-view=\"{Key(name)}\"", StringComparison.Ordinal);
            return page[start..(page.IndexOf($"<!--{name}-->", StringComparison.Ordinal) + name.Length + 7)] + "</div>";
        }


        Assert.Equal(("text/html; charset=utf-8", $"{Key("a")}, {Key("c")}, {Key("d")}"), (script.Http.Response.ContentType, script.Http.Response.Headers[PageHandler.ViewHeader].ToString()));
        Assert.Equal(Region("a") + Region("c") + Region("d"), Site.Body(script.Http));
        Assert.StartsWith("<!doctype html>", Site.Body(plain.Http), StringComparison.Ordinal);
    }

    internal static string Key(string name) => Host + ":0." + (Array.IndexOf(Names, name) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static readonly RenderPlan Plan = new([], [
        new LiteralNode("<h1>"), new ValueNode(static model => ((Card)model!).Title), new LiteralNode("</h1>"),
        new ScopeNode(1, [
            new LiteralNode("<main>"),
            .. Names.Select((name, index) => Region(index + 2, name)),
            new LiteralNode("</main>"),
        ]),
    ], []);

    private static ViewNode Region(int slot, string name) => new(slot, Key(name), [
        new LiteralNode("<b>"), new ValueNode(static model => ((Card)model!).Title), new LiteralNode("</b><button name=\"_btn\" value=\""), IdNode.Instance, new LiteralNode("|x\">save</button><!--" + name + "-->"),
    ], [new LiteralNode("<em>failed</em>")]);

    private static (string, string, string, string, string) Titles(ImmutableArray<object?> models) =>
        (((Card)models[2]!).Title, ((Card)models[3]!).Title, ((Card)models[4]!).Title, ((Card)models[5]!).Title, ((Card)models[6]!).Title);

    private static async Task<(DefaultHttpContext Http, string Loads, ImmutableArray<object?> Models)> PostAsync(Action<IssueCalls> command, string? pageReads = null, bool navigation = false, bool invalid = false)
    {
        var site = new Site();
        site.Engine.Plan = Plan;
        ImmutableArray<object?> commanded = [];
        site.Engine.Command = models =>
        {
            commanded = models;
            var card = (Card)models[4]!;
            card.Invalid = invalid;
            command(card.Calls!);
            return ValueTask.CompletedTask;
        };
        var counts = new Counts();
        await using var services = Services();
        var http = Site.Http();
        http.RequestServices = services.CreateScope().ServiceProvider;
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream("_btn=4.0%7Cx"u8.ToArray());
        if (navigation)
        {
            http.Request.Headers[DocumentParts.NavigationHeader] = "1";
        }

        try
        {
            await site.Handler.PostAsync(http, Board(counts, pageReads));
        }
        catch (InvalidOperationException error) when (error.Message == "The save failed.")
        {
            http.Items["failed"] = error;
        }

        return (http, string.Join(",", counts.Loads), commanded);
    }

    internal static PageDefinition Board(Counts counts, string? pageReads)
    {
        LoaderDefinition Loader(string name, Action<IServiceProvider>? reads = null) => new(
            (services, _, _) =>
            {
                reads?.Invoke(services);
                return ValueTask.FromResult(new LoaderOutcome(counts.Next(name)));
            },
            static (services, data) => new Card((string)data!) { Calls = services.GetService<IssueCalls>() })
        {
            Receive = static (model, data) => ((Card)model).Title = (string)data!,
        };
        ViewInstance View(string name, Action<IServiceProvider>? reads = null) =>
            new(Key(name), 1, new ViewDefinition("Views/Card.axaml", static _ => null) { Loader = Loader(name, reads) });
        return new PageDefinition("/board", Host, static _ => null, [new LayoutDefinition("Pages/Layout.axaml", static _ => null) { Loader = Loader("layout") }])
        {
            Loader = Loader("page", pageReads is null ? null : services => services.GetRequiredService<IDataKeys>().Read(pageReads)),
            Views =
            [
                View("a", static services => services.GetRequiredService<IssueCalls>().List()),
                View("b", static services => services.GetRequiredService<IDataKeys>().Read("other")),
                View("c"),
                View("d", static services => services.GetRequiredService<IDataKeys>().Read("issue:7")),
                View("e", static services => services.GetRequiredService<IssueCalls>().Find(8)),
            ],
        };
    }

    internal static ServiceProvider Services() => new ServiceCollection()
        .AddScoped<IssueCalls>()
        .AddScoped<PageNavigator>()
        .AddScoped<Aioros.Navigation.INavigator>(static services => services.GetRequiredService<PageNavigator>())
        .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    internal sealed class Counts
    {
        private readonly Dictionary<string, int> _counts = [];

        public List<string> Loads { get; } = [];

        public string Next(string name)
        {
            var count = _counts.GetValueOrDefault(name) + 1;
            _counts[name] = count;
            var load = name + " " + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Loads.Add(load);
            return load;
        }
    }

    /// <summary>A server function group's in-process caller as the generator writes it for <c>[Reads]</c> and <c>[Invalidates]</c>.</summary>
    internal sealed class IssueCalls : IKeyedCalls
    {
        public IDataKeys? Keys { get; set; }

        public void List() => (Keys ?? DataKeys.Current)?.Read("issues");

        public void Find(int id) => (Keys ?? DataKeys.Current)?.Read("issue:" + id.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public void Save(int id)
        {
            if ((Keys ?? DataKeys.Current) is { } keys)
            {
                keys.Invalidate("issues");
                keys.Invalidate("issue:" + id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        public void Untracked()
        {
            if (Keys is null)
            {
                DataKeys.Current?.Undeclared();
            }
        }
    }

    internal sealed class Card(string title) : System.ComponentModel.INotifyDataErrorInfo
    {
        public event EventHandler<System.ComponentModel.DataErrorsChangedEventArgs>? ErrorsChanged;

        public string Title { get; set; } = title;

        public IssueCalls? Calls { get; init; }

        public bool Invalid
        {
            get;
            set
            {
                field = value;
                ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(nameof(Title)));
            }
        }

        public bool HasErrors => Invalid;

        public System.Collections.IEnumerable GetErrors(string? propertyName) => Invalid ? ImmutableArray.Create("A title is required.") : ImmutableArray<string>.Empty;
    }
}
