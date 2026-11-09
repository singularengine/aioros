using System.Text;
using Aioros.Development;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Development;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.Extensions.DependencyInjection;
using Aioros.Targets.Ssr.Live;
using Aioros.Targets.Ssr.Themes;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class PageHandlerTests
{
    [Fact]
    [Trait("AC", "SSR-01")]
    public async Task PagesAreWrittenAsCompleteDocumentsAsync()
    {
        var site = new Site();
        var http = Site.Http();

        await site.Handler.GetAsync(http, Site.Page(() => new Model("Inbox & co")));

        var html = Site.Body(http);
        Assert.Equal("text/html; charset=utf-8", http.Response.ContentType);
        Assert.StartsWith("<!doctype html>", html, StringComparison.Ordinal);
        Assert.Contains("<title>Inbox &amp; co</title>", html, StringComparison.Ordinal);
        Assert.Contains(".page{}", html, StringComparison.Ordinal);
        Assert.Contains("<body style=\"background:#fafafa;color:#111\">", html, StringComparison.Ordinal);
        Assert.Contains("<form method=\"post\" novalidate class=\"c\" style=\"min-height:100vh\"><div class=\"aioros-body\" style=\"display:contents\"><p>Inbox &amp; co</p></div></form>", html, StringComparison.Ordinal);
        Assert.Contains("<script src=\"/_aioros/app/", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-190")]
    public async Task TheFrameworksScriptIsAnImmutableFileAsync()
    {
        var site = new Site();
        var http = Site.Http();
        await site.Handler.GetAsync(http, Site.Page(() => new Model("Inbox")));
        var html = Site.Body(http);
        var path = Documents.Scripts.Path;
        var served = new DefaultHttpContext();
        served.Response.Body = new MemoryStream();
        var missing = new DefaultHttpContext();

        await Documents.Scripts.ServeAsync(served, path[Documents.Scripts.Prefix.Length..]);
        await Documents.Scripts.ServeAsync(missing, "0000000000000000.js");

        Assert.Matches("^/_aioros/app/[0-9a-f]{16}\\.js$", path);
        Assert.Contains("<script src=\"" + path + "\" defer></script>", html[..html.IndexOf("</head>", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.DoesNotContain("window.aioros={go", html, StringComparison.Ordinal);
        Assert.Equal(("text/javascript; charset=utf-8", "public, max-age=31536000, immutable"), (served.Response.ContentType, served.Response.Headers.CacheControl.ToString()));
        Assert.Contains("window.aioros={go", System.Text.Encoding.UTF8.GetString(((MemoryStream)served.Response.Body).ToArray()), StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status404NotFound, missing.Response.StatusCode);
    }

    [Fact]
    [Trait("AC", "SSR-02")]
    public async Task ReusablePlansAreCompiledOnceAsync()
    {
        var site = new Site();
        await site.Handler.GetAsync(Site.Http(), Site.Page(() => new Model("First")));
        var second = Site.Http();

        await site.Handler.GetAsync(second, Site.Page(() => new Model("Second")));

        Assert.Single(site.Engine.Renders);
        Assert.Contains("<p>Second</p>", Site.Body(second), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-03")]
    public async Task PagesThatAreNotReusableRenderEveryTimeAsync()
    {
        var site = new Site();
        site.Engine.Reusable = false;
        site.Engine.Failure = "an unfollowed binding";
        var page = Site.Page(() => new Model());

        await site.Handler.GetAsync(Site.Http(), page);
        var second = await site.Handler.EntryAsync(page, [new Model()], "light", CancellationToken.None);
        site.Cache.Clear();
        await site.Handler.GetAsync(Site.Http(), page);

        Assert.Equal(3, site.Engine.Renders.Count);
        Assert.Equal(1, site.Engine.Answers);
        Assert.Equal((false, "an unfollowed binding"), (second.Page.Reusable, second.Page.Failure));
        Assert.Equal(0, site.Cache.Count);
    }

    [Fact]
    [Trait("AC", "SSR-04")]
    public async Task MissingModelsAnswerNotFoundAsync()
    {
        var http = Site.Http();

        await new Site().Handler.GetAsync(http, Site.Page(() => new Model { NotFound = true }));

        Assert.Equal(StatusCodes.Status404NotFound, http.Response.StatusCode);
    }

    [Fact]
    [Trait("AC", "SSR-05")]
    public async Task RouteValuesAndQueriesReachTheLoaderAsync()
    {
        var model = new Model();
        var http = Site.Http();
        http.Request.RouteValues["id"] = "7";
        http.Request.QueryString = new QueryString("?tab=open");

        await new Site().Handler.GetAsync(http, Site.Page(() => model));

        Assert.Equal(7, model.Request!.Number("id"));
        Assert.Equal("open", model.Request.Value("tab"));
        Assert.Equal("/inbox", model.Request.Path);
    }

    [Fact]
    [Trait("AC", "SSR-06")]
    public async Task ThemesHaveTheirOwnPlansAsync()
    {
        var site = new Site();

        await site.Handler.GetAsync(Site.Http(theme: "dark"), Site.Page(() => new Model()));
        await site.Handler.GetAsync(Site.Http(theme: "dark"), Site.Page(() => new Model()));
        await site.Handler.GetAsync(Site.Http(), Site.Page(() => new Model()));

        Assert.Equal(["dark", "light"], site.Engine.Renders.Select(render => render.Theme));
        Assert.Equal(2, site.Cache.Count);
    }

    [Fact]
    [Trait("AC", "SSR-07")]
    public void TheThemeSwitchTogglesAndGoesBack()
    {
        var local = Site.Http();
        local.Request.Headers.Referer = "https://app.test/issues?x=1";
        var foreign = Site.Http(theme: "dark");
        foreign.Request.Headers.Referer = "https://evil.test/";

        var back = Assert.IsAssignableFrom<IResult>(ThemeSwitch.Switch(local, "toggle"));
        var home = ThemeSwitch.Switch(foreign, "toggle");

        Assert.Contains("theme=dark", local.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
        Assert.Equal("/issues?x=1", Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.RedirectHttpResult>(back).Url);
        Assert.Contains("theme=light", foreign.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
        Assert.Equal("/", Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.RedirectHttpResult>(home).Url);
    }

    [Fact]
    [Trait("AC", "SSR-183")]
    public void TheThemeSwitchGoesBackOnlyToALocalPath()
    {
        static string BackTo(string referer)
        {
            var http = Site.Http();
            http.Request.Headers.Referer = referer;
            return Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.RedirectHttpResult>(ThemeSwitch.Switch(http, "dark")).Url;
        }

        string[] referers = ["http://app.test//evil.example/x", "http://app.test/\\evil.example/x", "http://app.test/\\/evil.example/x", "http://app.test/issues?x=1"];

        Assert.Equal(["/", "/", "/", "/issues?x=1"], referers.Select(BackTo));
    }

    [Fact]
    [Trait("AC", "SSR-272")]
    public void AnotherSiteCannotSwitchTheTheme()
    {
        static (bool Set, string Url) Switched(string? site)
        {
            var http = Site.Http();
            http.Request.Headers.Referer = "https://app.test/issues";
            if (site is not null)
            {
                http.Request.Headers["Sec-Fetch-Site"] = site;
            }

            var answer = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.RedirectHttpResult>(ThemeSwitch.Switch(http, "dark"));
            return (http.Response.Headers.SetCookie.ToString().Contains("theme=dark", StringComparison.Ordinal), answer.Url);
        }

        string?[] sites = ["cross-site", "same-site", "same-origin", "none", null];

        Assert.Equal([(false, "/"), (false, "/"), (true, "/issues"), (true, "/issues"), (true, "/issues")], sites.Select(Switched));
    }

    [Fact]
    [Trait("AC", "SSR-291")]
    public async Task AnAppCanFollowTheDeviceThemeByDefaultAsync()
    {
        static DefaultHttpContext Visitor(string? theme, string? scheme, string? fallback)
        {
            var http = Site.Http(theme: theme);
            http.RequestServices = new ServiceCollection().AddSingleton(new SsrOptions { Theme = fallback ?? ThemeSwitch.Light }).BuildServiceProvider();
            if (scheme is not null)
            {
                http.Request.Headers[ThemeSwitch.Hint] = scheme;
            }

            return http;
        }

        var site = new Site();
        var (dark, plain, chosen, unset) = (Visitor(null, "\"dark\"", "system"), Visitor(null, null, "system"), Visitor("light", "\"dark\"", "system"), Visitor(null, "\"dark\"", null));
        foreach (var http in new[] { dark, plain, chosen, unset })
        {
            await site.Handler.GetAsync(http, Site.Page(() => new Model()));
        }

        Assert.Equal(["dark", "light", "light", "light"], new[] { dark, plain, chosen, unset }.Select(ThemeSwitch.Of));
        Assert.Equal((ThemeSwitch.Hint, ThemeSwitch.Hint), (plain.Response.Headers["Accept-CH"].ToString(), plain.Response.Headers["Critical-CH"].ToString()));
        Assert.Contains(ThemeSwitch.Hint, plain.Response.Headers.Vary.ToString(), StringComparison.Ordinal);
        Assert.Contains("theme=system", plain.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
        Assert.Equal((string.Empty, string.Empty), (chosen.Response.Headers["Accept-CH"].ToString(), chosen.Response.Headers.SetCookie.ToString()));
        Assert.Equal((string.Empty, string.Empty), (unset.Response.Headers["Accept-CH"].ToString(), unset.Response.Headers.SetCookie.ToString()));
    }

    [Fact]
    [Trait("AC", "SSR-170")]
    public async Task TheSystemThemeFollowsTheColorSchemeHintAsync()
    {
        static DefaultHttpContext Hinted(string? scheme)
        {
            var http = Site.Http(theme: ThemeSwitch.SystemPreference);
            if (scheme is not null)
            {
                http.Request.Headers[ThemeSwitch.Hint] = scheme;
            }

            return http;
        }

        var switched = Site.Http();
        ThemeSwitch.Switch(switched, "system");
        var site = new Site();
        var (dark, light, none, chosen) = (Hinted("\"dark\""), Hinted("\"light\""), Hinted(null), Site.Http(theme: "dark"));
        foreach (var http in new[] { dark, light, none, chosen })
        {
            await site.Handler.GetAsync(http, Site.Page(() => new Model()));
        }

        var toggled = Hinted("\"dark\"");
        ThemeSwitch.Switch(toggled, "toggle");

        Assert.Contains("theme=system", switched.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
        Assert.Equal(ThemeSwitch.Hint, switched.Response.Headers["Accept-CH"].ToString());
        Assert.Equal(["dark", "light", "light", "dark"], new[] { dark, light, none, chosen }.Select(ThemeSwitch.Of));
        Assert.Equal(["dark", "light"], site.Engine.Renders.Select(render => render.Theme));
        Assert.Equal((ThemeSwitch.Hint, ThemeSwitch.Hint), (none.Response.Headers["Accept-CH"].ToString(), none.Response.Headers["Critical-CH"].ToString()));
        Assert.Contains(ThemeSwitch.Hint, none.Response.Headers.Vary.ToString(), StringComparison.Ordinal);
        Assert.False(chosen.Response.Headers.ContainsKey("Accept-CH"));
        Assert.Contains("theme=light", toggled.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-171")]
    public async Task TheThemeCookieIsSecureOnlyOverHttpsAsync()
    {
        var plain = Site.Http();
        plain.Request.Host = new HostString("203.0.113.21:3010");
        var secure = Site.Http();
        secure.Request.Scheme = "https";
        ThemeSwitch.Switch(plain, "dark");
        ThemeSwitch.Switch(secure, "dark");
        var cookie = plain.Response.Headers.SetCookie.ToString();
        var site = new Site();
        var next = Site.Http();
        next.Request.Headers.Cookie = cookie.Split(';')[0];

        await site.Handler.GetAsync(next, Site.Page(() => new Model()));

        Assert.DoesNotContain("secure", cookie.Split("; "));
        Assert.Contains("secure", secure.Response.Headers.SetCookie.ToString().Split("; "));
        Assert.Equal(["dark"], site.Engine.Renders.Select(render => render.Theme));
    }

    [Fact]
    [Trait("AC", "SSR-176")]
    public async Task TheSystemThemeFallsBackToTheScriptsCookieAsync()
    {
        static DefaultHttpContext Visitor(string cookies, string? hint = null)
        {
            var http = Site.Http();
            http.Request.Headers.Cookie = cookies;
            if (hint is not null)
            {
                http.Request.Headers[ThemeSwitch.Hint] = hint;
            }

            return http;
        }

        var site = new Site();
        var cookie = Visitor("theme=system; theme-system=dark");
        var switched = Site.Http();

        await site.Handler.GetAsync(cookie, Site.Page(() => new Model()));
        ThemeSwitch.Switch(switched, "system");

        Assert.Equal(["dark"], site.Engine.Renders.Select(render => render.Theme));
        Assert.Equal(
            ["dark", "light", "light", "light"],
            new[] { cookie, Visitor("theme=system; theme-system=dark", "\"light\""), Visitor("theme=system"), Visitor("theme=light; theme-system=dark") }.Select(ThemeSwitch.Of));
        Assert.DoesNotContain("httponly", switched.Response.Headers.SetCookie.ToString().Split("; "));
    }

    [Fact]
    [Trait("AC", "SSR-08")]
    public async Task PostsRedirectConflictOrRenderAsync()
    {
        var redirected = await PostAsync(new Model { RedirectTo = "/inbox/7" }, new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false)));
        var conflicted = await PostAsync(new Model(), new PostOutcome(PostStatus.Conflict, null));
        var rejected = await PostAsync(new Model(), new PostOutcome(PostStatus.BadRequest, null));
        var rendered = await PostAsync(new Model("Saved"), null);

        Assert.Equal((StatusCodes.Status303SeeOther, "/inbox/7"), (redirected.Http.Response.StatusCode, redirected.Http.Response.Headers.Location.ToString()));
        Assert.Equal("Ada", redirected.Site.Engine.Posted!["f0"]);
        Assert.Equal(StatusCodes.Status409Conflict, conflicted.Http.Response.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, rejected.Http.Response.StatusCode);
        Assert.Contains("<p>Saved</p>", Site.Body(rendered.Http), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-09")]
    public async Task ReloadsDropPlansAndNotifyTheBrowserAsync()
    {
        var site = new Site();
        await site.Handler.GetAsync(Site.Http(), Site.Page(() => new Model()));

        DevelopmentChannel.Apply(new XamlReloaded("Pages/inbox/Page.axaml"), site.Cache);

        Assert.Equal(0, site.Cache.Count);
        Assert.Equal("event: reload\ndata: Pages/inbox/Page.axaml\n\n", DevelopmentChannel.Format(new XamlReloaded("Pages/inbox/Page.axaml")));
        Assert.Equal(
            "event: failure\ndata: {\"source\":\"Pages/inbox/Page.axaml\",\"message\":\"Unexpected end\"}\n\n",
            DevelopmentChannel.Format(new XamlReloadFailed("Pages/inbox/Page.axaml", "Unexpected end")));
    }

    [Fact]
    [Trait("AC", "SSR-10")]
    public async Task LivePagesStreamTheirChangesAsync()
    {
        var site = new Site();
        var model = new Model("Before");
        var stream = new SignalStream();
        using var abort = new CancellationTokenSource();
        var http = Site.Http(body: stream);
        http.RequestAborted = abort.Token;

        var live = LiveUpdates.StreamAsync(http, Site.Page(() => model), site.Handler);
        var connected = await stream.Writes.ReadAsync(TestContext.Current.CancellationToken);
        model.Title = "After";
        var message = await stream.Writes.ReadAsync(TestContext.Current.CancellationToken);
        await abort.CancelAsync();
        await live;

        Assert.Matches("^: connected\n\nevent: connection\ndata: [0-9a-f]{32}\n\n$", connected);
        Assert.Equal("text/event-stream", http.Response.ContentType);
        Assert.Equal("data: <p>After</p>\n\n", message);
    }

    [Fact]
    [Trait("AC", "SSR-11")]
    public async Task LayoutModelsComeFirstAsync()
    {
        var site = new Site();
        var outer = new Model("outer");
        var inner = new Model("inner");
        var page = new Model("page");

        await site.Handler.GetAsync(Site.Http(), Site.Page(() => page, () => outer, () => inner));

        Assert.Equal([outer, inner, page], site.Engine.Renders.Single().Models);
        Assert.All([outer, inner, page], model => Assert.NotNull(model.Request));
    }

    [Fact]
    [Trait("AC", "SSR-12")]
    public async Task LoadersRedirectWithoutRenderingAsync()
    {
        var site = new Site();
        var http = Site.Http();

        await site.Handler.GetAsync(http, Site.Page(() => new Model { RedirectTo = "/issues" }));

        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal("/issues", http.Response.Headers.Location.ToString());
        Assert.Empty(site.Engine.Renders);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-13")]
    public async Task ChangesInOneUiJobAreSentOnceAsync()
    {
        var site = new Site();
        var model = new Model("Start");
        var stream = new SignalStream();
        var ui = new GatedUiThread();
        using var abort = new CancellationTokenSource();
        var http = Site.Http(body: stream, ui: ui);
        http.RequestAborted = abort.Token;

        var live = LiveUpdates.StreamAsync(http, Site.Page(() => model), site.Handler);
        await stream.Writes.ReadAsync(TestContext.Current.CancellationToken);
        model.Title = "A";
        await ui.Waiting.WaitAsync(TestContext.Current.CancellationToken);
        model.Title = "B";
        model.Title = "C";
        ui.Open();
        var message = await stream.Writes.ReadAsync(TestContext.Current.CancellationToken);
        await abort.CancelAsync();
        await live;

        Assert.Equal("data: <p>C</p>\n\n", message);
    }

    [Fact]
    [Trait("AC", "SSR-14")]
    public async Task PagesAreCapturedByPathAsync()
    {
        var capture = new FakeCapture();
        var model = new Model();
        PageDefinition[] pages = [new("/issues/{id}", "Pages/issues/[id]/Page.axaml", _ => model, [])];
        var http = Site.Http();
        http.Request.QueryString = new QueryString("?path=/issues/7&theme=dark&width=800&height=600");
        var missing = Site.Http();
        missing.Request.QueryString = new QueryString("?path=/nowhere");

        await CaptureEndpoint.HandleAsync(http, pages, capture);
        await CaptureEndpoint.HandleAsync(missing, pages, capture);

        Assert.Equal("image/png", http.Response.ContentType);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], ((MemoryStream)http.Response.Body).ToArray());
        Assert.Equal(("/issues/{id}", new CaptureOptions("dark", 800, 600, CaptureOptions.Normal)), (capture.Last!.Value.Page.Route, capture.Last.Value.Options));
        Assert.Equal(7, model.Request!.Number("id"));
        Assert.Equal(StatusCodes.Status404NotFound, missing.Response.StatusCode);
    }

    [Fact]
    [Trait("AC", "SSR-107")]
    public async Task CapturesAnswerJsonInAStateAsync()
    {
        var capture = new FakeCapture();
        PageDefinition[] pages = [new("/issues/{id}", "Pages/issues/[id]/Page.axaml", _ => new Model(), [])];
        var http = Site.Http();
        http.Request.QueryString = new QueryString("?path=/issues/7&state=pointerover&format=json");
        var unknown = Site.Http();
        unknown.Request.QueryString = new QueryString("?path=/issues/7&state=hovering");

        await CaptureEndpoint.HandleAsync(http, pages, capture);
        var options = capture.Last!.Value.Options;
        await CaptureEndpoint.HandleAsync(unknown, pages, capture);

        Assert.Equal("application/json", http.Response.ContentType);
        Assert.Equal(
            """{"png":"iVBORw==","texts":[{"text":"Hello","x":10,"y":20,"width":30.5,"height":17}]}""",
            System.Text.Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray()));
        Assert.Equal("pointerover", options.State);
        Assert.Equal(StatusCodes.Status400BadRequest, unknown.Response.StatusCode);
    }

    [Fact]
    [Trait("AC", "SSR-15")]
    public async Task FormsCarryAntiforgeryTokensWhenTurnedOnAsync()
    {
        var site = new Site(new SsrOptions { Antiforgery = true });
        site.Engine.Fields = true;
        var readOnly = new Site(new SsrOptions { Antiforgery = true });
        var page = Site.Http(antiforgery: true);
        var plain = Site.Http(antiforgery: true);
        var forged = Site.Http(antiforgery: true);
        forged.Request.Method = "POST";
        forged.Request.ContentType = "application/x-www-form-urlencoded";
        forged.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("f0=Ada&_btn=1%7Cx"));

        await site.Handler.GetAsync(page, Site.Page(() => new Model()));
        await readOnly.Handler.GetAsync(plain, Site.Page(() => new Model()));
        await site.Handler.PostAsync(forged, Site.Page(() => new Model()));

        Assert.Matches("<form method=\"post\"[^>]*><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"[^\"]+\"><div class=\"aioros-body\"", Site.Body(page));
        Assert.DoesNotContain("__RequestVerificationToken", Site.Body(plain), StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status400BadRequest, forged.Response.StatusCode);
        Assert.Null(site.Engine.Posted);
    }

    [Fact]
    [Trait("AC", "SSR-16")]
    public async Task NotFoundPagesAreRenderedWith404Async()
    {
        var site = new Site(new SsrOptions { NotFound = new PageDefinition("/_not-found", "Pages/NotFound.axaml", _ => new Model("Lost"), []) });
        var missingModel = Site.Http();
        var unmatched = Site.Http();
        var bare = Site.Http();

        await site.Handler.GetAsync(missingModel, Site.Page(() => new Model { NotFound = true }));
        await site.Handler.NotFoundAsync(unmatched);
        await new Site().Handler.NotFoundAsync(bare);

        Assert.Equal(StatusCodes.Status404NotFound, missingModel.Response.StatusCode);
        Assert.Contains("<p>Lost</p>", Site.Body(missingModel), StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status404NotFound, unmatched.Response.StatusCode);
        Assert.Contains("<title>Lost</title>", Site.Body(unmatched), StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status404NotFound, bare.Response.StatusCode);
        Assert.Empty(Site.Body(bare));
    }

    [Fact]
    [Trait("AC", "SSR-17")]
    public async Task ErrorPagesAreRenderedWith500Async()
    {
        var site = new Site(new SsrOptions { Error = new PageDefinition("/_error", "Pages/Error.axaml", _ => new Model("Something failed"), []) });
        var failed = Site.Http();
        var bare = Site.Http();

        await site.Handler.ErrorAsync(failed);
        await new Site().Handler.ErrorAsync(bare);

        Assert.Equal(StatusCodes.Status500InternalServerError, failed.Response.StatusCode);
        Assert.Contains("<p>Something failed</p>", Site.Body(failed), StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status500InternalServerError, bare.Response.StatusCode);
        Assert.Empty(Site.Body(bare));
    }

    [Fact]
    [Trait("AC", "SSR-18")]
    public async Task InterIsServedAsAvaloniasStaticWoff2Async()
    {
        var css = WebFonts.Css("/_aioros/fonts");
        var semiBold = await WebFonts.ReadAsync("Inter-SemiBold-latin.woff2", TestContext.Current.CancellationToken);
        var unknown = await WebFonts.ReadAsync("../secret", TestContext.Current.CancellationToken);

        Assert.Equal(12, css.Split("@font-face").Length - 1);
        foreach (var (weight, number) in new[] { ("Thin", 100), ("Light", 300), ("Regular", 400), ("Medium", 500), ("SemiBold", 600), ("Bold", 700) })
        {
            Assert.Contains($"src:url(/_aioros/fonts/Inter-{weight}-latin.woff2) format('woff2');font-weight:{number};font-display:swap;unicode-range:U+0000-00FF", css, StringComparison.Ordinal);
            Assert.Contains($"src:url(/_aioros/fonts/Inter-{weight}-latin-ext.woff2) format('woff2');font-weight:{number};font-display:swap;unicode-range:U+0100-02BA", css, StringComparison.Ordinal);
        }

        Assert.Equal("wOF2"u8.ToArray(), semiBold![..4]);
        Assert.Null(unknown);
    }

    [Fact]
    [Trait("AC", "SSR-244")]
    public async Task TheWarmupReportsWhenItFinishesAsync()
    {
        var site = new Site();
        using var logs = new RecordedLogs();
        PageDefinition[] pages =
        [
            new("/inbox", "Pages/inbox/Page.axaml", _ => new Model(), []),
            new("/broken", "Pages/broken/Page.axaml", _ => throw new InvalidOperationException("no data"), []),
        ];

        var outcomes = await Warmup.RunAsync(pages, new ServiceCollection().BuildServiceProvider(), site.Handler, TestContext.Current.CancellationToken);
        Warmup.Report(logs.CreateLogger("Aioros.Warmup"), outcomes, TimeSpan.FromMilliseconds(1234));
        var finished = await logs.WaitForAsync(static entry => entry.Message.StartsWith("Warm-up finished", StringComparison.Ordinal), TestContext.Current.CancellationToken);
        var failed = await logs.WaitForAsync(static entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning, TestContext.Current.CancellationToken);

        Assert.Equal((Microsoft.Extensions.Logging.LogLevel.Information, "Warm-up finished: 1 of 2 pages compiled in 1234 ms"), (finished.Level, finished.Message));
        Assert.Equal("/broken could not be compiled at startup", failed.Message);
    }

    [Fact]
    [Trait("AC", "SSR-281")]
    public async Task TheWarmupNamesThePagesItCouldNotPlanAsync()
    {
        using var logs = new RecordedLogs();
        WarmupOutcome[] outcomes =
        [
            new("/dashboard", [], "not reusable: the body differs") { Unplanned = true },
            new("/lab", [], "not reusable: Button.Content is bound") { Unplanned = true, At = new SourcePosition("/app/Pages/lab/Page.axaml", 12, 6) },
            new("/old", [], "its model redirects to /new") { Skipped = true },
            new("/tags/{tag}", [], "route parameters without a sample URL"),
        ];

        Warmup.Report(logs.CreateLogger("Aioros.Warmup"), outcomes, TimeSpan.FromMilliseconds(5));
        await logs.WaitForAsync(static entry => entry.Message.StartsWith("Warm-up finished", StringComparison.Ordinal), TestContext.Current.CancellationToken);

        Assert.Equal(["/dashboard is answered by the engine: not reusable: the body differs", "/app/Pages/lab/Page.axaml(12,6): /lab is answered by the engine: not reusable: Button.Content is bound"], logs.Entries.Where(static entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning).Select(static entry => entry.Message));
    }

    [Fact]
    [Trait("AC", "SSR-19")]
    public async Task StaticPagesAreCompiledAtStartupAsync()
    {
        var site = new Site();
        var issue = new Model();
        PageDefinition[] pages =
        [
            new("/inbox", "Pages/inbox/Page.axaml", _ => new Model(), []),
            new("/broken", "Pages/broken/Page.axaml", _ => throw new InvalidOperationException("no data"), []),
            new("/tags/{tag}", "Pages/tags/[tag]/Page.axaml", _ => new Model(), []),
            new("/old", "Pages/old/Page.axaml", _ => new Model { RedirectTo = "/inbox" }, []),
            new("/issues/{id}", "Pages/issues/[id]/Page.axaml", _ => issue, []),
            new("/users/{name}", "Pages/users/[name]/Page.axaml", _ => new Model(), []),
        ];
        var services = new ServiceCollection()
            .AddSingleton<ISitemapSource>(new Sitemap("https://app.test/about", "https://app.test/issues/7"))
            .AddSingleton<IPrecompileSource>(new Samples("/users/ada"))
            .BuildServiceProvider();

        var outcomes = await Warmup.RunAsync(pages, services, site.Handler, TestContext.Current.CancellationToken);
        site.Engine.Reusable = false;
        site.Engine.Failure = "a bound collection";
        var dynamic = Assert.Single(await Warmup.RunAsync([new("/live", "Pages/live/Page.axaml", _ => new Model(), [])], new ServiceCollection().BuildServiceProvider(), site.Handler, TestContext.Current.CancellationToken));

        Assert.Equal(6, site.Cache.Count);
        Assert.Equal(2, outcomes[5].Entries.Length);
        Assert.Equal(["dark", "light"], outcomes[0].Entries.Select(entry => entry.Theme).Order());
        Assert.Equal("no data", outcomes[1].Error?.Message);
        Assert.Equal(("route parameters without a sample URL", "its model redirects to /inbox"), (outcomes[2].Reason, outcomes[3].Reason));
        Assert.Equal(("/issues/7", "7", 2), (issue.Request?.Path, issue.Request?.RouteValues["id"], outcomes[4].Entries.Length));
        Assert.Equal(("not reusable: a bound collection", 0), (dynamic.Reason, dynamic.Entries.Length));
    }

    [Fact]
    [Trait("AC", "SSR-187")]
    public async Task ButtonPostsGoThroughTheEngineWhileItRunsAvaloniaAsync()
    {
        var plan = new RenderPlan([], [new LiteralNode("<p>"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</p>"), new IdNode { Field = new SlotSpec("text") }, new IdNode { Field = new SlotSpec("command"), Button = true }], []);
        var posted = new List<(bool Engine, string? Title)>();
        foreach (var (owns, form) in new[] { (true, "f0=Ada&_btn=0%7Cgo"), (true, "f0=Ada&_chg=0"), (false, "f0=Ada&_btn=0%7Cgo") })
        {
            var site = new Site(fields: new FakeFields());
            site.Engine.Plan = plan;
            site.Engine.OwnsCommands = owns;
            var model = new Model("Kept");
            await site.Handler.GetAsync(Site.Http(), Site.Page(() => model));
            var http = Site.Http();
            http.Request.Method = "POST";
            http.Request.ContentType = "application/x-www-form-urlencoded";
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));

            await site.Handler.PostAsync(http, Site.Page(() => model));

            posted.Add((site.Engine.Posted is not null, site.Engine.Posted is null ? model.Title : null));
        }

        Assert.Equal([(true, null), (false, "Ada"), (false, "Ada")], posted);
    }

    [Fact]
    [Trait("AC", "SSR-166")]
    public async Task PostPathIsWarmedWithoutBindingOrRunningAnythingAsync()
    {
        var site = new Site(fields: new FakeFields());
        var model = new Model("Kept");
        var bound = new RenderPlan([], [new LiteralNode("<p>"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</p>"), new IdNode { Field = new SlotSpec("text") }, new IdNode { Field = new SlotSpec("command"), Button = true }], []);
        var larger = new RenderPlan([], [new LiteralNode("<h1>Larger</h1>"), .. bound.Body], []);
        var special = new RenderPlan([], [new IdNode { Field = new SlotSpec("text") }], []);
        var unbound = new RenderPlan([], [new LiteralNode("<p>"), IdNode.Instance], []);
        site.Engine.PlanFor = page => page.Route switch { "/inbox" => bound, "/larger" => larger, "/_special" => special, _ => unbound };
        PageDefinition[] pages =
        [
            new("/larger", "Pages/larger/Page.axaml", _ => new Model(), []),
            new("/inbox", "Pages/inbox/Page.axaml", _ => model, []),
            new("/other", "Pages/other/Page.axaml", _ => new Model(), []),
            new("/_special", "Pages/Special.axaml", _ => new Model(), []),
        ];
        var services = new ServiceCollection().BuildServiceProvider();

        var exported = await Warmup.RunAsync(pages, services, site.Handler, TestContext.Current.CancellationToken);
        var started = await Warmup.RunAsync(pages, services, site.Handler, posts: true, TestContext.Current.CancellationToken);

        Assert.Equal([null, null, null, null], exported.Select(outcome => outcome.Posted));
        Assert.Equal([null, StatusCodes.Status409Conflict, null, null], started.Select(outcome => outcome.Posted));
        Assert.Equal(("Kept", null), (model.Title, model.RedirectTo));
        Assert.Null(site.Engine.Posted);
    }

    [Fact]
    [Trait("AC", "PRODUCTION-05")]
    public async Task PrecompiledPagesSkipTheEngineAsync()
    {
        var site = new Site();
        var page = Site.Page(() => new Model("Loaded"));
        var loaded = site.Cache.Preload([new PlannedPage("/inbox", "light", FakeEngine.Page(reusable: true))], [page], site.Options);
        var http = Site.Http();

        await site.Handler.GetAsync(http, page);
        await Warmup.RunAsync([page], new ServiceCollection().BuildServiceProvider(), site.Handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, loaded);
        Assert.Contains("<p>Loaded</p>", Site.Body(http), StringComparison.Ordinal);
        Assert.Equal(["dark"], site.Engine.Renders.Select(render => render.Theme));
    }

    [Fact]
    [Trait("AC", "SSR-20")]
    public async Task LiveStreamsAreCompressedPerConnectionAsync()
    {
        var site = new Site();
        var model = new Model("Before");
        using var abort = new CancellationTokenSource();
        var stream = new SignalStream { Written = count => { if (count == 2) { abort.Cancel(); } } };
        var http = Site.Http(body: stream);
        http.Request.Headers.AcceptEncoding = "gzip, br";
        http.RequestAborted = abort.Token;
        var received = new MemoryStream();

        var live = LiveUpdates.StreamAsync(http, Site.Page(() => model), site.Handler);
        await received.WriteAsync(await stream.Raw.ReadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        model.Title = "After";
        await live;
        while (stream.Raw.TryRead(out var rest))
        {
            await received.WriteAsync(rest, TestContext.Current.CancellationToken);
        }

        received.Position = 0;
        await using var gzip = new System.IO.Compression.GZipStream(received, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal("gzip", http.Response.Headers.ContentEncoding.ToString());
        Assert.Matches("^: connected\n\nevent: connection\ndata: [0-9a-f]{32}\n\ndata: <p>After</p>\n\n$", text);
    }

    [Fact]
    [Trait("AC", "PRODUCTION-08")]
    public async Task DescribedFormsPostWithoutTheEngineAsync()
    {
        var site = new Site(fields: new FakeFields());
        var plan = new RenderPlan([], [new LiteralNode("<p>"), new ValueNode(model => (model as Model)?.Title), new LiteralNode("</p>"), new IdNode { Field = new SlotSpec("text") }, new IdNode { Field = new SlotSpec("command"), Button = true }], []);
        var page = Site.Page(() => new Model());
        site.Cache.Preload([new PlannedPage("/inbox", "light", new CompiledPage(plan, string.Empty, "#fff", "#111", true))], [page], site.Options);
        var saved = Posted("f0=Saved");
        var redirected = Posted("f0=Saved&_btn=0%7Cgo");

        await site.Handler.PostAsync(saved, page);
        await site.Handler.PostAsync(redirected, page);

        Assert.Null(site.Engine.Posted);
        Assert.Contains("<p>Saved</p>", Site.Body(saved), StringComparison.Ordinal);
        Assert.Equal((303, "/done"), (redirected.Response.StatusCode, redirected.Response.Headers.Location.ToString()));
    }

    [Fact]
    [Trait("AC", "SSR-21")]
    public async Task ReusablePagesLinkHashedStylesheetsAsync()
    {
        var site = new Site(new SsrOptions { ExternalCss = true, HeadCss = ".head{}" });
        var reusable = Site.Http();
        await site.Handler.GetAsync(reusable, Site.Page(() => new Model()));
        var other = new Site(new SsrOptions { ExternalCss = true });
        other.Engine.Reusable = false;
        var dynamic = Site.Http();
        await other.Handler.GetAsync(dynamic, Site.Page(() => new Model()));
        var links = System.Text.RegularExpressions.Regex.Matches(Site.Body(reusable), "<link rel=\"stylesheet\" href=\"(/_aioros/css/[0-9a-f]+\\.css)\"").Select(match => match.Groups[1].Value).ToList();
        var page = Site.Http();
        var missing = Site.Http();

        await Stylesheets.ServeAsync(page, links[1][Stylesheets.Path.Length..], site.Cache.Styles);
        await Stylesheets.ServeAsync(missing, "nope.css", site.Cache.Styles);

        Assert.Equal(2, links.Count);
        Assert.DoesNotContain(".page{}", Site.Body(reusable), StringComparison.Ordinal);
        Assert.Equal((".page{}\n", "text/css; charset=utf-8", "public, max-age=31536000, immutable"), (Site.Body(page), page.Response.ContentType, page.Response.Headers.CacheControl.ToString()));
        Assert.Equal(404, missing.Response.StatusCode);
        Assert.Contains(".page{}", Site.Body(dynamic), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-22")]
    public async Task NavigationRequestsGetOnlyThePageAsync()
    {
        var site = new Site(new SsrOptions { HeadCss = ".head{}" });
        var navigation = Site.Http();
        navigation.Request.Headers["X-Aioros-Navigation"] = "1";
        var full = Site.Http();

        await site.Handler.GetAsync(navigation, Site.Page(() => new Model("Inbox") { Metadata = new PageMetadata("All mail") }));
        await site.Handler.GetAsync(full, Site.Page(() => new Model("Inbox") { Metadata = new PageMetadata("All mail") }));

        var html = Site.Body(navigation);
        Assert.Matches("^<title>Inbox</title>\n<meta name=\"aioros-plan\" content=\"[0-9a-f]{16}\">\n<style data-css>", html);
        Assert.EndsWith("<p>Inbox</p></div></form>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.Ordinal);
        Assert.DoesNotContain("All mail", html, StringComparison.Ordinal);
        Assert.Equal("X-Aioros-Navigation, X-Aioros-Layout, Cookie", navigation.Response.Headers.Vary.ToString());
        Assert.Contains("All mail", Site.Body(full), StringComparison.Ordinal);
        Assert.Contains("<script", Site.Body(full), StringComparison.Ordinal);
    }



    private static DefaultHttpContext Posted(string body)
    {
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return http;
    }

    [Fact]
    [Trait("AC", "SSR-23")]
    public async Task LayoutsCanRedirectAsync()
    {
        var get = Site.Http();
        await new Site().Handler.GetAsync(get, Site.Page(() => new Model(), () => new Model { RedirectTo = "/login" }));
        var both = Site.Http();
        await new Site().Handler.GetAsync(both, Site.Page(() => new Model { RedirectTo = "/page" }, () => new Model { RedirectTo = "/login" }));
        var site = new Site();
        site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false));
        var post = Site.Http();
        post.Request.Method = "POST";
        post.Request.ContentType = "application/x-www-form-urlencoded";
        post.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("_btn=1%7Cx"));
        await site.Handler.PostAsync(post, Site.Page(() => new Model(), () => new Model { RedirectTo = "/signed-out" }));

        Assert.Equal((302, "/login"), (get.Response.StatusCode, get.Response.Headers.Location.ToString()));
        Assert.Equal("/page", both.Response.Headers.Location.ToString());
        Assert.Equal((303, "/signed-out"), (post.Response.StatusCode, post.Response.Headers.Location.ToString()));
    }

    [Fact]
    [Trait("AC", "SSR-24")]
    public async Task LiveUpdatesCarryTheCssOfPlansThatAreNotReusableAsync()
    {
        static async Task<List<string>> StreamAsync(bool reusable)
        {
            var site = new Site();
            site.Engine.Reusable = reusable;
            var model = new Model("Before");
            var stream = new SignalStream();
            using var abort = new CancellationTokenSource();
            var http = Site.Http(body: stream);
            http.RequestAborted = abort.Token;
            await site.Handler.GetAsync(Site.Http(), Site.Page(() => model));
            var live = LiveUpdates.StreamAsync(http, Site.Page(() => model), site.Handler);
            await stream.Writes.ReadAsync(TestContext.Current.CancellationToken);
            var messages = new List<string>();
            foreach (var title in new[] { "One", "Two" })
            {
                model.Title = title;
                messages.Add(await stream.Writes.ReadAsync(TestContext.Current.CancellationToken));
            }

            await abort.CancelAsync();
            await live;
            return messages;
        }

        var planned = await StreamAsync(reusable: true);
        var literal = await StreamAsync(reusable: false);

        Assert.Equal(["data: <p>One</p>\n\n", "data: <p>Two</p>\n\n"], planned);
        Assert.Equal(["data: <style data-live-css>.page{}\ndata: </style><p>One</p>\n\n", "data: <style data-live-css>.page{}\ndata: </style><p>Two</p>\n\n"], literal);
    }

    [Fact]
    [Trait("AC", "SSR-26")]
    public async Task PagesTheBrowserKnowsAnswerWithTheirDynamicsAsync()
    {
        var site = new Site();
        site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: true));
        var page = Site.Page(() => new Model("Inbox & co"));
        var document = Site.Http();
        await site.Handler.GetAsync(document, page);
        var fingerprint = FakeEngine.Page(reusable: true).Plan.Template.Fingerprint;
        var known = Site.Http();
        known.Request.Headers[PageHandler.PlanHeader] = fingerprint;
        await site.Handler.GetAsync(known, page);
        var other = Site.Http();
        other.Request.Headers[PageHandler.PlanHeader] = "0000000000000000";
        await site.Handler.GetAsync(other, page);
        var posted = Site.Http();
        posted.Request.Method = "POST";
        posted.Request.ContentType = "application/x-www-form-urlencoded";
        posted.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("_btn=1%7Cx"));
        posted.Request.Headers[PageHandler.PlanHeader] = fingerprint;
        await site.Handler.PostAsync(posted, page);
        var template = Site.Http();
        var routes = new Dictionary<string, System.Collections.Immutable.ImmutableArray<PageAccess>> { [page.Route] = [] };
        await PageHandler.TemplateAsync(template, site.Cache, fingerprint, routes);
        var unknown = Site.Http();
        await PageHandler.TemplateAsync(unknown, site.Cache, "ffffffffffffffff", routes);
        var literal = new Site();
        literal.Engine.Reusable = false;
        var dynamic = Site.Http();
        await literal.Handler.GetAsync(dynamic, page);

        Assert.Contains($"<meta name=\"aioros-plan\" content=\"{fingerprint}\">", Site.Body(document), StringComparison.Ordinal);
        Assert.DoesNotContain("<meta name=\"aioros-plan\"", Site.Body(dynamic), StringComparison.Ordinal);
        Assert.Equal(("application/json", "{\"t\":\"Inbox & co\",\"d\":[\"Inbox &amp; co\"]}"), (known.Response.ContentType, Site.Body(known)));
        Assert.StartsWith("<!doctype html>", Site.Body(other), StringComparison.Ordinal);
        Assert.Equal("application/json", posted.Response.ContentType);
        var json = new System.Buffers.ArrayBufferWriter<byte>();
        PlanTemplate.Write(json, FakeEngine.Page(reusable: true).Plan.Body);
        Assert.Equal((Encoding.UTF8.GetString(json.WrittenSpan), "public, max-age=31536000, immutable"), (Site.Body(template), template.Response.Headers.CacheControl.ToString()));
        Assert.Equal(404, unknown.Response.StatusCode);
    }

    [Fact]
    [Trait("AC", "SSR-29")]
    public async Task PagesAreRenderedForTheirPathAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<a href=\"/inbox\""), new CurrentNode("/inbox"), new LiteralNode(">"), new ValueNode(scope => ((Model)scope!).Title)], []);
        var model = new Model("Before");
        var page = Site.Page(() => model);
        var document = Site.Http();
        await site.Handler.GetAsync(document, page);
        var dynamics = Site.Http();
        dynamics.Request.Headers[PageHandler.PlanHeader] = site.Engine.Plan.Template.Fingerprint;
        await site.Handler.GetAsync(dynamics, page);
        var stream = new SignalStream();
        using var abort = new CancellationTokenSource();
        var live = Site.Http(body: stream);
        live.Request.Path = "/inbox/live";
        live.RequestAborted = abort.Token;
        var streaming = LiveUpdates.StreamAsync(live, page, site.Handler);
        await stream.Writes.ReadAsync(TestContext.Current.CancellationToken);
        model.Title = "After";
        var message = await stream.Writes.ReadAsync(TestContext.Current.CancellationToken);
        await abort.CancelAsync();
        await streaming;

        Assert.Contains("<a href=\"/inbox\" aria-current=\"page\" data-nav>", Site.Body(document), StringComparison.Ordinal);
        Assert.Contains("aria-current=\\\"page\\\"", Site.Body(dynamics), StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\" data-nav>After", message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-30")]
    public async Task NavigationsThatKeepTheLayoutGetThePageAloneAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<nav><a href=\"/inbox\""), new CurrentNode("/inbox"), new LiteralNode(">Inbox</a><a href=\"/other\""), new CurrentNode("/other"), new LiteralNode(">Other</a></nav>"),
            new LiteralNode(PageRegion.Open), new LiteralNode("<p>"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</p>"), new LiteralNode(PageRegion.Close),
            new LiteralNode("<footer>Shell</footer>"),
        ], []);
        var page = Site.Page(() => new Model("Mail"));
        var inbox = Site.Http();
        await site.Handler.GetAsync(inbox, page);
        var other = Site.Http();
        other.Request.Path = "/other";
        await site.Handler.GetAsync(other, page);
        var signature = System.Text.RegularExpressions.Regex.Match(Site.Body(inbox), "<meta name=\"aioros-layout\" content=\"([0-9a-f]{16})\">").Groups[1].Value;
        var partial = Site.Http();
        partial.Request.Headers[DocumentParts.NavigationHeader] = "1";
        partial.Request.Headers[PageHandler.LayoutHeader] = signature;
        await site.Handler.GetAsync(partial, page);
        var whole = Site.Http();
        whole.Request.Headers[DocumentParts.NavigationHeader] = "1";
        whole.Request.Headers[PageHandler.LayoutHeader] = "0000000000000000";
        await site.Handler.GetAsync(whole, page);

        Assert.NotEmpty(signature);
        Assert.Contains($"<meta name=\"aioros-layout\" content=\"{signature}\">", Site.Body(other), StringComparison.Ordinal);
        Assert.Contains("<a href=\"/other\" aria-current=\"page\"", Site.Body(other), StringComparison.Ordinal);
        Assert.Equal("1", partial.Response.Headers[PageHandler.PartialHeader].ToString());
        Assert.Contains("<div class=\"aioros-body\" style=\"display:contents\"><p>Mail</p></div>", Site.Body(partial), StringComparison.Ordinal);
        Assert.DoesNotContain("Shell", Site.Body(partial), StringComparison.Ordinal);
        Assert.Contains("<footer>Shell</footer>", Site.Body(whole), StringComparison.Ordinal);
        Assert.Empty(whole.Response.Headers[PageHandler.PartialHeader].ToString());
    }

    [Fact]
    [Trait("AC", "SSR-233")]
    public async Task RefusedPostsStillAnswerThePageAsync()
    {
        var conflicted = await PostAsync(new Model("Questionnaire"), new PostOutcome(PostStatus.Conflict, null));
        var rejected = await PostAsync(new Model("Questionnaire"), new PostOutcome(PostStatus.BadRequest, null));

        Assert.Equal(StatusCodes.Status409Conflict, conflicted.Http.Response.StatusCode);
        Assert.Contains("<p>Questionnaire</p>", Site.Body(conflicted.Http), StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status400BadRequest, rejected.Http.Response.StatusCode);
        Assert.Contains("<p>Questionnaire</p>", Site.Body(rejected.Http), StringComparison.Ordinal);
    }

    private static async Task<(Site Site, DefaultHttpContext Http)> PostAsync(Model model, PostOutcome? outcome)
    {
        var site = new Site();
        site.Engine.Outcome = outcome;
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("f0=Ada&_btn=1%7Cx"));
        await site.Handler.PostAsync(http, Site.Page(() => model));
        return (site, http);
    }
}
