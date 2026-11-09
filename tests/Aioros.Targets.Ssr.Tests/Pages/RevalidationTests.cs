using System.Collections;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Text;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class RevalidationTests
{
    [Fact]
    [Trait("AC", "SSR-52")]
    public async Task SuccessfulCommandsRunTheLoadersAgainAsync()
    {
        var site = new Site();
        site.Engine.Command = static _ => ValueTask.CompletedTask;
        var loads = new List<(string Name, Marker Scope)>();
        var posted = Http("_btn=1%7Cx");
        await site.Handler.PostAsync(posted, Loaded(loads));
        var script = new Site();
        script.Engine.Command = static _ => ValueTask.CompletedTask;
        script.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: true));
        var scriptLoads = new List<(string Name, Marker Scope)>();
        var fetched = Http("_btn=1%7Cx");
        fetched.Request.Headers[PageHandler.PlanHeader] = FakeEngine.Page(reusable: true).Plan.Template.Fingerprint;
        await script.Handler.PostAsync(fetched, Loaded(scriptLoads));
        var boundLoads = new List<(string Name, Marker Scope)>();
        await new Site().Handler.PostAsync(Http("f0=Ada"), Loaded(boundLoads));

        Assert.Equal(["layout 1", "layout 2", "page 1", "page 2"], loads.Select(load => load.Name).Order(StringComparer.Ordinal));
        Assert.Equal(4, loads.Select(load => load.Scope).Distinct().Count());
        Assert.Contains("<p>layout 2</p>", Site.Body(posted), StringComparison.Ordinal);
        Assert.Equal(("application/json", "{\"t\":\"page 2\",\"d\":[\"layout 2\"]}"), (fetched.Response.ContentType, Site.Body(fetched)));
        Assert.Equal(4, scriptLoads.Count);
        Assert.Equal(2, boundLoads.Count);
    }

    [Fact]
    [Trait("AC", "SSR-53")]
    public async Task ViewModelsThatTakeDataAreKeptAndOthersCreatedAgainAsync()
    {
        var created = new List<string>();
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<p>"), new ValueNode(static model => ((Draft)model!).Title), new LiteralNode("</p><p>"), new ValueNode(static model => ((Draft)model!).Saved), new LiteralNode("</p>")], []);
        Draft? commanded = null;
        site.Engine.Command = models =>
        {
            commanded = (Draft)models[0]!;
            commanded.Saved = "Saved";
            return ValueTask.CompletedTask;
        };
        var counts = new Dictionary<string, int>();
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", Unreachable)
        {
            Loader = new LoaderDefinition((_, _, _) => NextAsync(counts, "team"), (_, data) => Created(created, data))
            {
                Receive = static (model, data) => ((Draft)model).Title = (string)data!,
            },
        }])
        {
            Loader = new LoaderDefinition((_, _, _) => NextAsync(counts, "issue"), (_, data) => Created(created, data)),
        };
        var posted = Http("_btn=1%7Cx");
        posted.Request.Headers[PageHandler.PlanHeader] = site.Engine.Plan.Template.Fingerprint;

        await site.Handler.PostAsync(posted, page);

        Assert.Equal(["team 1", "issue 1", "issue 2"], created);
        Assert.Equal(("team 2", "Saved"), (commanded?.Title, commanded?.Saved));
        Assert.Equal("{\"t\":\"issue 2\",\"d\":[\"team 2\",\"Saved\"]}", Site.Body(posted));
    }

    [Fact]
    [Trait("AC", "SSR-54")]
    public async Task FailedCommandsRevalidateNothingAsync()
    {
        var throwing = new Site();
        throwing.Engine.Command = static _ => ValueTask.FromException(new InvalidOperationException("The save failed."));
        var thrownLoads = new List<(string Name, Marker Scope)>();
        var failure = await Record.ExceptionAsync(() => throwing.Handler.PostAsync(Http("_btn=1%7Cx"), Loaded(thrownLoads)));
        var invalid = new Site();
        invalid.Engine.Command = static models =>
        {
            ((Draft)models[^1]!).Error = "A title is required.";
            return ValueTask.CompletedTask;
        };
        var invalidLoads = new List<(string Name, Marker Scope)>();
        var rejected = Http("_btn=1%7Cx");
        await invalid.Handler.PostAsync(rejected, Loaded(invalidLoads));
        var valid = new Site();
        valid.Engine.Command = static _ => ValueTask.CompletedTask;
        var withErrors = await PostBytesAsync(invalid, rejected: true);
        var revalidated = await PostBytesAsync(valid, rejected: false);
        TestContext.Current.TestOutputHelper?.WriteLine($"a post that revalidates {revalidated} B, one that does not {withErrors} B");

        Assert.Equal("The save failed.", Assert.IsType<InvalidOperationException>(failure).Message);
        Assert.Equal(2, thrownLoads.Count);
        Assert.Equal(2, invalidLoads.Count);
        Assert.Contains("<p>layout 1</p>", Site.Body(rejected), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-55")]
    public async Task NavigatingCommandsSkipRevalidationAndFreshResultsAnswerAsync()
    {
        var moving = new Site();
        moving.Engine.Command = static models => ((Draft)models[^1]!).Navigator!.NavigateAsync(new NavigationRequest("/issues"), CancellationToken.None);
        var movingLoads = new List<(string Name, Marker Scope)>();
        var moved = Http("_btn=1%7Cx");
        await moving.Handler.PostAsync(moved, Loaded(movingLoads));
        var deleting = new Site(new SsrOptions { NotFound = new PageDefinition("/_not-found", "Pages/NotFound.axaml", _ => new Model("Lost"), []) });
        deleting.Engine.Command = static _ => ValueTask.CompletedTask;
        var deleted = Http("_btn=1%7Cx");
        await deleting.Handler.PostAsync(deleted, Then(LoaderResult.NotFound()));
        var renaming = new Site();
        renaming.Engine.Command = static _ => ValueTask.CompletedTask;
        var renamed = Http("_btn=1%7Cx");
        await renaming.Handler.PostAsync(renamed, Then(LoaderResult.Redirect("../issues/8")));

        Assert.Equal((303, "/issues"), (moved.Response.StatusCode, moved.Response.Headers.Location.ToString()));
        Assert.Equal(2, movingLoads.Count);
        Assert.Equal(404, deleted.Response.StatusCode);
        Assert.Contains("<p>Lost</p>", Site.Body(deleted), StringComparison.Ordinal);
        Assert.Equal((303, "/issues/8"), (renamed.Response.StatusCode, renamed.Response.Headers.Location.ToString()));
    }

    private static async Task<long> PostBytesAsync(Site site, bool rejected)
    {
        var page = Loaded([]);
        await site.Handler.PostAsync(Http("_btn=1%7Cx"), page);
        var http = Http("_btn=1%7Cx");
        if (rejected)
        {
            site.Engine.Command = static models =>
            {
                ((Draft)models[^1]!).Error = "A title is required.";
                return ValueTask.CompletedTask;
            };
        }

        var start = GC.GetAllocatedBytesForCurrentThread();
        await site.Handler.PostAsync(http, page);
        return GC.GetAllocatedBytesForCurrentThread() - start;
    }

    [Fact]
    [Trait("AC", "SSR-153")]
    public async Task PostsThatShowWhatTheyLeftAreAnsweredAsRenderedAsync()
    {
        var shown = new RenderPlan([], [new LiteralNode("<p>page</p><dialog open class=\"prompt\"></dialog>")], []);
        var site = new Site();
        site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Plan = shown }) { Shows = true };
        var loads = new List<(string Name, Marker Scope)>();
        var posted = Http("_btn=1%7Cx");
        posted.Request.Headers["X-Aioros-Navigation"] = "1";
        var planned = new Site(fields: new FakeFields());
        var page = Site.Page(() => new Model());
        var plan = new RenderPlan([], [new LiteralNode("<p>"), new ValueNode(model => (model as Model)?.Title), new LiteralNode("</p>"), new IdNode { Field = new SlotSpec("text") }, new IdNode { Field = new SlotSpec("command") }], []);
        planned.Cache.Preload([new PlannedPage("/inbox", "light", new CompiledPage(plan, string.Empty, "#fff", "#111", true))], [page], planned.Options);
        var prompted = Http("f0=Saved&_prompt=_btn%3D1%257Cgo&_btn=3%7C");
        prompted.Request.Path = "/inbox";

        await site.Handler.PostAsync(posted, Loaded(loads));
        await planned.Handler.PostAsync(prompted, page);

        Assert.Equal(["layout 1", "page 1"], loads.Select(load => load.Name).Order(StringComparer.Ordinal));
        Assert.Contains("<p>page</p><dialog open class=\"prompt\"></dialog>", Site.Body(posted), StringComparison.Ordinal);
        Assert.Equal("_btn=1%7Cgo", planned.Engine.Posted?["_prompt"]);
    }

    private static PageDefinition Loaded(List<(string Name, Marker Scope)> loads)
    {
        var counts = new Dictionary<string, int>();
        return new PageDefinition("/inbox", "Pages/inbox/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", Unreachable)
        {
            Loader = new LoaderDefinition((services, _, _) => RecordedAsync(services, loads, counts, "layout"), static (services, data) => new Draft((string)data!, services.GetService<INavigator>())),
        }])
        {
            Loader = new LoaderDefinition((services, _, _) => RecordedAsync(services, loads, counts, "page"), static (services, data) => new Draft((string)data!, services.GetService<INavigator>())),
        };
    }

    private static PageDefinition Then(LoaderOutcome second)
    {
        var calls = 0;
        return new PageDefinition("/issues/7", "Pages/issues/[id]/Page.axaml", Unreachable, [])
        {
            Loader = new LoaderDefinition((_, _, _) => ValueTask.FromResult(++calls == 1 ? new LoaderOutcome("issue") : second), static (_, data) => new Draft((string)data!, null)),
        };
    }

    private static ValueTask<LoaderOutcome> RecordedAsync(IServiceProvider services, List<(string Name, Marker Scope)> loads, Dictionary<string, int> counts, string name)
    {
        var count = counts[name] = counts.GetValueOrDefault(name) + 1;
        loads.Add((name + " " + count, services.GetRequiredService<Marker>()));
        return ValueTask.FromResult(new LoaderOutcome(name + " " + count));
    }

    private static ValueTask<LoaderOutcome> NextAsync(Dictionary<string, int> counts, string name)
    {
        var count = counts[name] = counts.GetValueOrDefault(name) + 1;
        return ValueTask.FromResult(new LoaderOutcome(name + " " + count));
    }

    private static Draft Created(List<string> created, object? data)
    {
        created.Add((string)data!);
        return new Draft((string)data!, null);
    }

    private static DefaultHttpContext Http(string form)
    {
        var http = Site.Http();
        http.Request.Path = "/issues/7";
        http.RequestServices = new ServiceCollection()
            .AddScoped<Marker>()
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(static services => services.GetRequiredService<PageNavigator>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })
            .CreateScope()
            .ServiceProvider;
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));
        return http;
    }

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");

    internal sealed class Marker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    internal sealed class Draft(string title, INavigator? navigator) : IPageModel, INotifyDataErrorInfo
    {
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public string Title { get; set; } = title;

        public string Saved { get; set; } = string.Empty;

        public INavigator? Navigator { get; } = navigator;

        public string? Error
        {
            get;
            set
            {
                field = value;
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Title)));
            }
        }

        public bool HasErrors => Error is not null;

        public IEnumerable GetErrors(string? propertyName) => Error is null ? Array.Empty<string>() : ImmutableArray.Create(Error);
    }
}
