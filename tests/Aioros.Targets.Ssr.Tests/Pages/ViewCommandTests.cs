using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed partial class ViewCommandTests
{
    private const string FirstKey = "Pages/inbox/Page.axaml:0.1";
    private const string SecondKey = "Pages/inbox/Page.axaml:0.2";
    private const string Failure = "The view's save failed.";

    private static readonly RenderPlan Plan = new([], [
        new LiteralNode("<h1>"), new ValueNode(static model => ((Card)model!).Title), new LiteralNode("</h1>"),
        new ScopeNode(1, [
            new LiteralNode("<main><button name=\"_btn\" value=\""), IdNode.Instance, new LiteralNode("|x\">page</button>"),
            Region(2, FirstKey),
            Region(3, SecondKey),
            new LiteralNode("</main>"),
        ]),
    ], []);

    [Fact]
    [Trait("AC", "SSR-73")]
    public async Task AViewCommandRevalidatesOnlyThatViewAsync()
    {
        var succeeded = await PostAsync("_btn=3.0%7Cx", SaveAsync);
        var recreated = await PostAsync("_btn=3.0%7Cx", SaveAsync, receives: false);
        var thrown = await PostAsync("_btn=3.0%7Cx", static _ => ValueTask.FromException(new InvalidOperationException(Failure)));
        var invalid = await PostAsync("_btn=3.0%7Cx", static models =>
        {
            ((Card)models[3]!).Error = "A title is required.";
            return ValueTask.CompletedTask;
        });
        var moved = await PostAsync("_btn=3.0%7Cx", static models => ((Card)models[3]!).Navigator!.NavigateAsync(new NavigationRequest("/issues"), CancellationToken.None));
        var page = await PostAsync("_btn=0%7Cx", SaveAsync);
        var kept = succeeded.Models.Cast<Card>().ToList();

        Assert.Equal(["first 1", "layout 1", "page 1", "second 1", "second 2"], succeeded.Loads.Select(load => load.Name).Order(StringComparer.Ordinal));
        Assert.Equal(5, succeeded.Loads.Select(load => load.Scope).Distinct().Count());
        Assert.Equal(("second 2", "commanded"), (kept[3].Title, kept[3].Saved));
        Assert.Equal(["layout 1", "page 1", "first 1"], kept.Take(3).Select(card => card.Title));
        Assert.Contains("<b>second 2</b>", Site.Body(succeeded.Http), StringComparison.Ordinal);
        Assert.Equal("second 1", ((Card)recreated.Models[3]!).Title);
        Assert.Contains("<b>second 2</b>", Site.Body(recreated.Http), StringComparison.Ordinal);
        Assert.Equal(5, recreated.Loads.Count);
        Assert.Equal((true, 4), (thrown.Http.Items.ContainsKey(Failure), thrown.Loads.Count));
        Assert.Equal(4, invalid.Loads.Count);
        Assert.Equal((303, 4), (moved.Http.Response.StatusCode, moved.Loads.Count));
        Assert.Equal(8, page.Loads.Count);
    }

    [Fact]
    [Trait("AC", "SSR-74")]
    public async Task AViewCommandFromTheScriptAnswersOnlyItsRegionAsync()
    {
        var script = await PostAsync("_btn=3.0%7Cx", SaveAsync, navigation: true);
        var planned = await PostAsync("_btn=3.0%7Cx", SaveAsync, navigation: true, plan: true);
        var plain = await PostAsync("_btn=3.0%7Cx", SaveAsync);
        var page = await PostAsync("_btn=0%7Cx", SaveAsync, navigation: true, plan: true);
        var absent = await PostAsync("_btn=3.0%7Cx", SaveAsync, navigation: true, hidden: true);
        var region = Site.Body(script.Http);
        var whole = new ArrayBufferWriter<byte>();
        Plan.RenderBody(whole, script.Models);

        Assert.Equal(("text/html; charset=utf-8", SecondKey), (script.Http.Response.ContentType, script.Http.Response.Headers[PageHandler.ViewHeader].ToString()));
        Assert.Equal($"<div data-aioros-view=\"{SecondKey}\" style=\"display:contents\"><b>second 2</b><button name=\"_btn\" value=\"3.0|x\">save</button></div>", region);
        Assert.Contains(region, Encoding.UTF8.GetString(whole.WrittenSpan), StringComparison.Ordinal);
        Assert.Equal((region, SecondKey), (Site.Body(planned.Http), planned.Http.Response.Headers[PageHandler.ViewHeader].ToString()));
        Assert.StartsWith("<!doctype html>", Site.Body(plain.Http), StringComparison.Ordinal);
        Assert.Contains("<b>second 2</b>", Site.Body(plain.Http), StringComparison.Ordinal);
        Assert.Equal("application/json", page.Http.Response.ContentType);
        Assert.False(page.Http.Response.Headers.ContainsKey(PageHandler.ViewHeader));
        Assert.False(absent.Http.Response.Headers.ContainsKey(PageHandler.ViewHeader));
        Assert.Contains("<title>", Site.Body(absent.Http), StringComparison.Ordinal);
        TestContext.Current.TestOutputHelper?.WriteLine($"region {Encoding.UTF8.GetByteCount(region)} B, the page's dynamics {Encoding.UTF8.GetByteCount(Site.Body(page.Http))} B, the page's HTML {Encoding.UTF8.GetByteCount(Site.Body(plain.Http))} B");
    }

    [Fact]
    [Trait("AC", "SSR-75")]
    public async Task TheAntiforgeryTokenStaysValidAcrossRegionAnswersAsync()
    {
        var site = new Site(new SsrOptions { Antiforgery = true });
        site.Engine.Plan = Plan;
        site.Engine.Fields = true;
        var commands = 0;
        site.Engine.Command = _ =>
        {
            commands++;
            return ValueTask.CompletedTask;
        };
        var services = new ServiceCollection().AddLogging().AddAntiforgery().AddScoped<Marker>().BuildServiceProvider();
        var page = Loaded([]);
        var get = Http(services, "GET", null);
        await site.Handler.GetAsync(get, page);
        var token = TokenField().Match(Site.Body(get)).Groups[1].Value;
        var cookie = get.Response.Headers.SetCookie.ToString().Split(';')[0];

        var first = Http(services, "POST", "__RequestVerificationToken=" + Uri.EscapeDataString(token) + "&_btn=3.0%7Cx", cookie);
        first.Request.Headers[DocumentParts.NavigationHeader] = "1";
        await site.Handler.PostAsync(first, page);
        var second = Http(services, "POST", "__RequestVerificationToken=" + Uri.EscapeDataString(token) + "&_btn=3.0%7Cx", cookie);
        second.Request.Headers[DocumentParts.NavigationHeader] = "1";
        await site.Handler.PostAsync(second, page);
        var forged = Http(services, "POST", "_btn=3.0%7Cx", cookie);
        forged.Request.Headers[DocumentParts.NavigationHeader] = "1";
        await site.Handler.PostAsync(forged, page);

        Assert.NotEmpty(token);
        Assert.Equal((200, SecondKey), (first.Response.StatusCode, first.Response.Headers[PageHandler.ViewHeader].ToString()));
        Assert.DoesNotContain("__RequestVerificationToken", Site.Body(first), StringComparison.Ordinal);
        Assert.Equal((200, SecondKey), (second.Response.StatusCode, second.Response.Headers[PageHandler.ViewHeader].ToString()));
        Assert.Equal(400, forged.Response.StatusCode);
        Assert.Equal(2, commands);
    }

    [Fact]
    [Trait("AC", "SSR-109")]
    public async Task AChangedSelectionAnswersItsViewsRegionAsync()
    {
        static ValueTask BindAsync(ImmutableArray<object?> models)
        {
            ((Card)models[3]!).Title += " changed";
            return ValueTask.CompletedTask;
        }

        var script = await PostAsync("_chg=3.0&f3.0=1", BindAsync, navigation: true);
        var plain = await PostAsync("_chg=3.0&f3.0=1", BindAsync);
        var page = await PostAsync("_chg=0&f0=1", BindAsync, navigation: true, plan: true);

        Assert.Equal(SecondKey, script.Http.Response.Headers[PageHandler.ViewHeader].ToString());
        Assert.Equal($"<div data-aioros-view=\"{SecondKey}\" style=\"display:contents\"><b>second 1 changed</b><button name=\"_btn\" value=\"3.0|x\">save</button></div>", Site.Body(script.Http));
        Assert.Equal(4, script.Loads.Count);
        Assert.Equal(string.Empty, ((Card)script.Models[3]!).Saved);
        Assert.StartsWith("<!doctype html>", Site.Body(plain.Http), StringComparison.Ordinal);
        Assert.Equal(4, plain.Loads.Count);
        Assert.Equal("application/json", page.Http.Response.ContentType);
        Assert.False(page.Http.Response.Headers.ContainsKey(PageHandler.ViewHeader));
    }

    private static ViewNode Region(int slot, string key) => new(slot, key, [
        new LiteralNode("<b>"), new ValueNode(static model => ((Card)model!).Title), new LiteralNode("</b><button name=\"_btn\" value=\""), IdNode.Instance, new LiteralNode("|x\">save</button>"),
    ], [new LiteralNode("<em>failed</em>")]);

    private static ValueTask SaveAsync(ImmutableArray<object?> models)
    {
        ((Card)models[^1]!).Saved = "commanded";
        return ValueTask.CompletedTask;
    }

    private static async Task<(DefaultHttpContext Http, List<(string Name, Marker Scope)> Loads, ImmutableArray<object?> Models)> PostAsync(string form, Func<ImmutableArray<object?>, ValueTask> command, bool receives = true, bool navigation = false, bool plan = false, bool hidden = false)
    {
        var site = new Site();
        site.Engine.Plan = hidden ? new RenderPlan([], [new ConditionNode(static _ => false, [.. Plan.Body])], []) : Plan;
        ImmutableArray<object?> commanded = [];
        site.Engine.Command = models =>
        {
            commanded = models;
            return command(models);
        };
        var loads = new List<(string Name, Marker Scope)>();
        var http = Http(Services(), "POST", form);
        if (navigation)
        {
            http.Request.Headers[DocumentParts.NavigationHeader] = "1";
        }

        if (plan)
        {
            http.Request.Headers[PageHandler.PlanHeader] = site.Engine.Plan.Template.Fingerprint;
        }

        try
        {
            await site.Handler.PostAsync(http, Loaded(loads, receives));
        }
        catch (InvalidOperationException error) when (error.Message == Failure)
        {
            http.Items[Failure] = error;
        }

        return (http, loads, commanded);
    }

    private static PageDefinition Loaded(List<(string Name, Marker Scope)> loads, bool receives = true)
    {
        var counts = new Dictionary<string, int>();
        LoaderDefinition Loader(string name) => new((services, _, _) => RecordedAsync(services, loads, counts, name), static (services, data) => new Card((string)data!, services.GetService<INavigator>()))
        {
            Receive = receives || name != "second" ? static (model, data) => ((Card)model).Title = (string)data! : null,
        };
        return new PageDefinition("/inbox", "Pages/inbox/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", Unreachable) { Loader = Loader("layout") }])
        {
            Loader = Loader("page"),
            Views =
            [
                new ViewInstance(FirstKey, 0, new ViewDefinition("Views/Card.axaml", Unreachable) { Loader = Loader("first") }),
                new ViewInstance(SecondKey, 0, new ViewDefinition("Views/Card.axaml", Unreachable) { Loader = Loader("second") }),
            ],
        };
    }

    private static ValueTask<LoaderOutcome> RecordedAsync(IServiceProvider services, List<(string Name, Marker Scope)> loads, Dictionary<string, int> counts, string name)
    {
        var count = counts[name] = counts.GetValueOrDefault(name) + 1;
        loads.Add((name + " " + count, services.GetRequiredService<Marker>()));
        return ValueTask.FromResult(new LoaderOutcome(name + " " + count));
    }

    private static ServiceProvider Services() => new ServiceCollection()
        .AddScoped<Marker>()
        .AddScoped<PageNavigator>()
        .AddScoped<INavigator>(static services => services.GetRequiredService<PageNavigator>())
        .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    private static DefaultHttpContext Http(IServiceProvider services, string method, string? form, string? cookie = null)
    {
        var http = Site.Http();
        http.RequestServices = services.CreateScope().ServiceProvider;
        http.Request.Method = method;
        if (cookie is not null)
        {
            http.Request.Headers.Cookie = cookie;
        }

        if (form is not null)
        {
            http.Request.ContentType = "application/x-www-form-urlencoded";
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));
        }

        return http;
    }

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex TokenField();

    internal sealed class Marker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    internal sealed class Card(string title, INavigator? navigator) : System.ComponentModel.INotifyDataErrorInfo
    {
        public event EventHandler<System.ComponentModel.DataErrorsChangedEventArgs>? ErrorsChanged;

        public string Title { get; set; } = title;

        public string Saved { get; set; } = string.Empty;

        public INavigator? Navigator { get; } = navigator;

        public string? Error
        {
            get;
            set
            {
                field = value;
                ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(nameof(Title)));
            }
        }

        public bool HasErrors => Error is not null;

        public System.Collections.IEnumerable GetErrors(string? propertyName) => Error is null ? Array.Empty<string>() : ImmutableArray.Create(Error);
    }
}
