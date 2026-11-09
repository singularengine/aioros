using System.Text;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Live;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class LoaderResultTests
{
    private static readonly PageDefinition NotFoundPage = new("/_not-found", "Pages/NotFound.axaml", _ => new Model("Lost"), []);

    [Fact]
    [Trait("AC", "SSR-44")]
    public async Task LoadersThatFindNothingAnswerTheNotFoundPageAsync()
    {
        var created = new List<string>();
        var site = new Site(new SsrOptions { NotFound = NotFoundPage });
        var fromPage = Site.Http();
        var fromLayout = Site.Http();
        var posted = Post();

        await site.Handler.GetAsync(fromPage, Page(created, new LoaderOutcome("layout"), LoaderResult.NotFound()));
        await site.Handler.GetAsync(fromLayout, Page(created, LoaderResult.NotFound(), new LoaderOutcome("page")));
        await site.Handler.PostAsync(posted, Page(created, new LoaderOutcome("layout"), LoaderResult.NotFound()));

        Assert.All([fromPage, fromLayout, posted], http => Assert.Equal(StatusCodes.Status404NotFound, http.Response.StatusCode));
        Assert.All([fromPage, fromLayout, posted], http => Assert.Contains("<p>Lost</p>", Site.Body(http), StringComparison.Ordinal));
        Assert.Empty(created);
        Assert.Null(site.Engine.Posted);
    }

    [Fact]
    [Trait("AC", "SSR-45")]
    public async Task LoadersThatRedirectAnswerWithoutModelsAsync()
    {
        var created = new List<string>();
        var site = new Site();
        var requested = Site.Http();
        requested.Request.Path = "/crm/list";
        var posted = Post();
        posted.Request.Path = "/crm/list";
        var hostile = Site.Http();

        await site.Handler.GetAsync(requested, Page(created, LoaderResult.Redirect("edit"), new LoaderOutcome("page")));
        await site.Handler.PostAsync(posted, Page(created, new LoaderOutcome("layout"), LoaderResult.Redirect("../about")));
        await Assert.ThrowsAsync<ArgumentException>(() => site.Handler.GetAsync(hostile, Page(created, new LoaderOutcome("layout"), LoaderResult.Redirect("javascript:alert(1)"))));

        Assert.Equal((302, "/crm/edit"), (requested.Response.StatusCode, requested.Response.Headers.Location.ToString()));
        Assert.Equal((303, "/about"), (posted.Response.StatusCode, posted.Response.Headers.Location.ToString()));
        Assert.Empty(created);
        Assert.Empty(site.Engine.Renders);
        Assert.Null(site.Engine.Posted);
    }

    [Fact]
    [Trait("AC", "SSR-46")]
    public async Task RedirectsWinAndTheOutermostLoaderFirstAsync()
    {
        var created = new List<string>();
        var site = new Site(new SsrOptions { NotFound = NotFoundPage });

        var redirectOverNotFound = await AnswerAsync(site, Page(created, LoaderResult.NotFound(), LoaderResult.Redirect("/login")));
        var outermost = await AnswerAsync(site, Page(created, LoaderResult.Redirect("/outer"), LoaderResult.Redirect("/inner")));
        var loaderOverNavigator = await AnswerAsync(site, Page(created, Navigating("/navigator"), LoaderResult.Redirect("/loader")));
        var navigatorOverNotFound = await AnswerAsync(site, Page(created, Navigating("/navigator"), LoaderResult.NotFound()));
        var modelsUnread = await AnswerAsync(site, new PageDefinition("/inbox", "Pages/inbox/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", _ => new Model { RedirectTo = "/model" })])
        {
            Loader = Loader(created, LoaderResult.NotFound()),
        });

        Assert.Equal((302, "/login"), redirectOverNotFound);
        Assert.Equal((302, "/outer"), outermost);
        Assert.Equal((302, "/loader"), loaderOverNavigator);
        Assert.Equal((302, "/navigator"), navigatorOverNotFound);
        Assert.Equal((404, string.Empty), modelsUnread);
        Assert.Empty(created);
    }

    [Fact]
    [Trait("AC", "SSR-47")]
    public async Task LoadersGiveTheTitleAndMetadataAsync()
    {
        var site = new Site();
        Model? model = null;
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", Unreachable, [])
        {
            Loader = new LoaderDefinition(
                (_, _, _) => ValueTask.FromResult(new LoaderOutcome("Model title") { Title = "Issue 7 · Lumen", Metadata = new PageMetadata("The seventh issue.", "/issues/7", Type: "article") }),
                (_, data) => model = new Model((string)data!)),
        };
        var document = Site.Http();
        var navigation = Site.Http();
        navigation.Request.Headers["X-Aioros-Navigation"] = "1";
        var known = Site.Http();
        known.Request.Headers[PageHandler.PlanHeader] = FakeEngine.Page(reusable: true).Plan.Template.Fingerprint;
        var stream = new SignalStream();
        using var abort = new CancellationTokenSource();
        var live = Site.Http(body: stream);
        live.Request.Path = "/inbox/live";
        live.Request.QueryString = new QueryString("?plan=" + FakeEngine.Page(reusable: true).Plan.Template.Fingerprint);
        live.RequestAborted = abort.Token;

        await site.Handler.GetAsync(document, page);
        await site.Handler.GetAsync(navigation, page);
        await site.Handler.GetAsync(known, page);
        var streaming = LiveUpdates.StreamAsync(live, page, site.Handler);
        await stream.Writes.ReadAsync(TestContext.Current.CancellationToken);
        model!.Title = "Changed";
        var message = await stream.Writes.ReadAsync(TestContext.Current.CancellationToken);
        await abort.CancelAsync();
        await streaming;

        var head = Site.Body(document)[..Site.Body(document).IndexOf("</head>", StringComparison.Ordinal)];
        Assert.Contains("<title>Issue 7 · Lumen</title>", head, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\" content=\"The seventh issue.\">", head, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"canonical\" href=\"http://app.test/issues/7\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:title\" content=\"Issue 7 · Lumen\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:type\" content=\"article\">", head, StringComparison.Ordinal);
        Assert.StartsWith("<title>Issue 7 · Lumen</title>", Site.Body(navigation), StringComparison.Ordinal);
        Assert.StartsWith("{\"t\":\"Issue 7 · Lumen\"", Site.Body(known), StringComparison.Ordinal);
        Assert.StartsWith("data: {\"t\":\"Issue 7 · Lumen\",\"d\":[\"Changed\"]}", message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SEO-09")]
    public async Task LayoutAndPageMetadataMergeAsync()
    {
        var site = new Site();
        var layout = new LoaderOutcome("layout") { Title = "Lumen", Metadata = new PageMetadata("Every issue of the team.", Image: "/og/lumen.png", Indexable: false) };
        var untitled = Site.Http();
        var titled = Site.Http();

        await site.Handler.GetAsync(untitled, Page([], layout, new LoaderOutcome("page") { Metadata = new PageMetadata("The seventh issue.", "/issues/7", Type: "article") }));
        await site.Handler.GetAsync(titled, Page([], layout, new LoaderOutcome("page") { Title = "Issue 7" }));

        var head = Head(untitled);
        Assert.Contains("<title>Lumen</title>", head, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\" content=\"The seventh issue.\">", head, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"canonical\" href=\"http://app.test/issues/7\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:image\" content=\"http://app.test/og/lumen.png\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:type\" content=\"article\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", head, StringComparison.Ordinal);
        Assert.Contains("<title>Issue 7</title>", Head(titled), StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\" content=\"Every issue of the team.\">", Head(titled), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SEO-50")]
    public async Task AlternatesInOtherLanguagesAreWrittenInTheHeadAsync()
    {
        var site = new Site();
        var alternates = new PageMetadata(Alternates: [new("es", "/es"), new("zh-Hans", "zh"), new("x-default", "/"), new("ja", "https://ja.example.com/")]);
        var localized = Site.Http();
        var inherited = Site.Http();
        var plain = Site.Http();

        await site.Handler.GetAsync(localized, Page([], new LoaderOutcome("layout"), new LoaderOutcome("page") { Metadata = alternates }));
        await site.Handler.GetAsync(inherited, Page([], new LoaderOutcome("layout") { Metadata = alternates }, new LoaderOutcome("page") { Title = "Inicio", Metadata = new PageMetadata("La página de inicio.") }));
        await site.Handler.GetAsync(plain, Page([], new LoaderOutcome("layout"), new LoaderOutcome("page")));

        static IEnumerable<string> Links(string head) => System.Text.RegularExpressions.Regex.Matches(head, "<link rel=\"alternate\"[^>]*>").Select(match => match.Value);
        string[] expected =
        [
            "<link rel=\"alternate\" hreflang=\"es\" href=\"http://app.test/es\">",
            "<link rel=\"alternate\" hreflang=\"zh-Hans\" href=\"http://app.test/zh\">",
            "<link rel=\"alternate\" hreflang=\"x-default\" href=\"http://app.test/\">",
            "<link rel=\"alternate\" hreflang=\"ja\" href=\"https://ja.example.com/\">",
        ];
        Assert.Equal(expected, Links(Head(localized)));
        Assert.Equal(expected, Links(Head(inherited)));
        Assert.Empty(Links(Head(plain)));
    }

    [Fact]
    [Trait("AC", "SSR-48")]
    public async Task PageModelsStillGiveWhatLoadersLeaveOutAsync()
    {
        var site = new Site(new SsrOptions { NotFound = NotFoundPage });
        var fromModel = Site.Http();
        var mixed = Site.Http();
        var missing = Site.Http();

        await site.Handler.GetAsync(fromModel, Described(new LoaderOutcome("data"), new Model("Model title") { Metadata = new PageMetadata("From the model.") }));
        await site.Handler.GetAsync(mixed, Described(new LoaderOutcome("data") { Title = "Loader title" }, new Model("Model title") { Metadata = new PageMetadata("From the model.") }));
        await site.Handler.GetAsync(missing, Described(new LoaderOutcome("data"), new Model { NotFound = true }));

        Assert.Contains("<title>Model title</title>", Head(fromModel), StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\" content=\"From the model.\">", Head(fromModel), StringComparison.Ordinal);
        Assert.Contains("<title>Loader title</title>", Head(mixed), StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\" content=\"From the model.\">", Head(mixed), StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status404NotFound, missing.Response.StatusCode);
        Assert.Contains("<p>Lost</p>", Site.Body(missing), StringComparison.Ordinal);
    }

    private static PageDefinition Described(LoaderOutcome outcome, Model model) =>
        new("/inbox", "Pages/inbox/Page.axaml", Unreachable, [])
        {
            Loader = new LoaderDefinition((_, _, _) => ValueTask.FromResult(outcome), (_, _) => model),
        };

    private static async Task<(int Status, string Location)> AnswerAsync(Site site, PageDefinition page)
    {
        var http = Site.Http();
        http.RequestServices = new ServiceCollection()
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(services => services.GetRequiredService<PageNavigator>())
            .BuildServiceProvider()
            .CreateScope()
            .ServiceProvider;
        await site.Handler.GetAsync(http, page);
        return (http.Response.StatusCode, http.Response.Headers.Location.ToString());
    }

    private static Func<IServiceProvider, PageRequest, CancellationToken, ValueTask<LoaderOutcome>> Navigating(string path) => async (services, _, cancellationToken) =>
    {
        await services.GetRequiredService<INavigator>().NavigateAsync(new NavigationRequest(path), cancellationToken);
        return new LoaderOutcome("navigated");
    };

    private static PageDefinition Page(List<string> created, LoaderOutcome layout, LoaderOutcome page) =>
        Page(created, (_, _, _) => ValueTask.FromResult(layout), page);

    private static PageDefinition Page(List<string> created, Func<IServiceProvider, PageRequest, CancellationToken, ValueTask<LoaderOutcome>> layout, LoaderOutcome page) =>
        new("/inbox", "Pages/inbox/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", Unreachable) { Loader = new LoaderDefinition(layout, (_, data) => Create(created, data)) }])
        {
            Loader = Loader(created, page),
        };

    private static LoaderDefinition Loader(List<string> created, LoaderOutcome outcome) =>
        new((_, _, _) => ValueTask.FromResult(outcome), (_, data) => Create(created, data));

    private static Model Create(List<string> created, object? data)
    {
        created.Add((string)data!);
        return new Model((string)data!);
    }

    private static DefaultHttpContext Post()
    {
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("f0=Edited"));
        return http;
    }

    private static string Head(HttpContext http) => Site.Body(http)[..Site.Body(http).IndexOf("</head>", StringComparison.Ordinal)];

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");
}
