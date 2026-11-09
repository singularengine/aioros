using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Aioros.Functions;
using Aioros.Pages;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Caching;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed partial class PageCacheTests
{
    [Fact]
    [Trait("AC", "SSR-100")]
    public async Task PagesThatDoNotDependOnTheUserAreServedFromTheCacheAsync()
    {
        var site = new CachedSite();
        var page = site.Page();

        var first = await site.GetAsync(page);
        var second = await site.GetAsync(page);
        var loadsAfterTwo = site.Loads;
        await site.GetAsync(page, query: "?tab=open");
        await site.GetAsync(page, query: "?tab=open");
        await site.GetAsync(page, theme: "dark");
        await site.GetAsync(page, theme: "dark");
        var loadsAfterVariants = site.Loads;
        var layout = Layout(first.Body);
        var navigation = await site.GetAsync(page, navigation: true);
        var partial = await site.GetAsync(page, navigation: true, layout: layout);
        var dynamics = await site.GetAsync(page, plan: site.Fingerprint(page));
        var loadsAfterAnswers = site.Loads;
        var again = new[]
        {
            await site.GetAsync(page, navigation: true),
            await site.GetAsync(page, navigation: true, layout: layout),
            await site.GetAsync(page, plan: site.Fingerprint(page)),
        };

        Assert.Equal(first.Body, second.Body);
        Assert.Contains("<p>Issues</p>", second.Body, StringComparison.Ordinal);
        Assert.Equal(2, loadsAfterTwo);
        Assert.Equal(6, loadsAfterVariants);
        Assert.Equal("1", partial.Partial);
        Assert.Contains("\"d\"", dynamics.Body, StringComparison.Ordinal);
        Assert.Equal([navigation.Body, partial.Body, dynamics.Body], again.Select(answer => answer.Body));
        Assert.Equal(loadsAfterAnswers, site.Loads);
        Assert.All(again.Append(first).Append(second), answer => Assert.Equal("public, no-cache", answer.CacheControl));
        Assert.All(again.Append(first), answer => Assert.Contains("Cookie", answer.Vary, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "SSR-218")]
    public async Task KeptAnswersArePerCultureAsync()
    {
        var site = new CachedSite();
        var page = site.Page();
        static async Task<Answer> InAsync(string name, Func<Task<Answer>> get)
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo(name);
            PageCultures.Use(culture, culture);
            return await get();
        }

        await InAsync("fr-FR", () => site.GetAsync(page));
        var french = site.Loads;
        await InAsync("en-US", () => site.GetAsync(page));
        var english = site.Loads;
        await InAsync("fr-FR", () => site.GetAsync(page));

        Assert.True(english > french);
        Assert.Equal(english, site.Loads);
    }

    [Fact]
    [Trait("AC", "SSR-101")]
    public async Task CachedPagesAreEvictedByKeyLifetimeAndPlanAsync()
    {
        var site = new CachedSite();
        var page = site.Page();
        site.Engine.Command = _ =>
        {
            site.Title = "Fresh";
            site.Versions.Invalidate("issues");
            return ValueTask.CompletedTask;
        };

        await site.GetAsync(page);
        var post = Site.Http();
        post.Request.Method = "POST";
        post.Request.ContentType = "application/x-www-form-urlencoded";
        post.Request.Body = new MemoryStream("_btn=1%7Cx"u8.ToArray());
        await site.Handler.PostAsync(post, page);
        var loadsAfterPost = site.Loads;
        var evicted = await site.GetAsync(page);
        var loadsAfterEviction = site.Loads;
        site.Versions.Invalidate("projects");
        await site.GetAsync(page);
        var loadsAfterOtherKey = site.Loads;
        site.Time.Advance(TimeSpan.FromSeconds(29));
        await site.GetAsync(page);
        var loadsBeforeTheLifetime = site.Loads;
        site.Time.Advance(TimeSpan.FromSeconds(1));
        await site.GetAsync(page);
        var loadsAfterTheLifetime = site.Loads;
        site.Plans.Clear();
        site.Plans.Store(page.Route, "light", site.Plans.Document(await site.Engine.RenderAsync(page, [], "light", TestContext.Current.CancellationToken), new SsrOptions(), live: false));
        await site.GetAsync(page);

        Assert.Contains("<p>Fresh</p>", evicted.Body, StringComparison.Ordinal);
        Assert.Equal(loadsAfterPost + 2, loadsAfterEviction);
        Assert.Equal(loadsAfterEviction, loadsAfterOtherKey);
        Assert.Equal(loadsAfterEviction, loadsBeforeTheLifetime);
        Assert.Equal(loadsBeforeTheLifetime + 2, loadsAfterTheLifetime);
        Assert.Equal(loadsAfterTheLifetime + 2, site.Loads);
    }

    [Fact]
    [Trait("AC", "SSR-102")]
    public async Task PagesWithoutADeclaredLifetimeAreNeverKeptAsync()
    {
        var site = new CachedSite();
        var pages = new[]
        {
            site.Page(pageCached: false),
            site.Page(layoutLoader: false),
            site.Page() with { Live = true },
            site.Page(notFound: true),
        };

        var answers = new List<Answer>();
        var loads = new List<int>();
        foreach (var page in pages)
        {
            var before = site.Loads;
            answers.Add(await site.GetAsync(page));
            answers.Add(await site.GetAsync(page));
            loads.Add(site.Loads - before);
        }

        Assert.Equal([4, 2, 4, 4], loads);
        Assert.Equal(0, site.Pages.Count);
        Assert.All(answers.Take(6), answer => Assert.Equal("private, no-cache", answer.CacheControl));
        Assert.All(answers.Skip(6), answer => Assert.Equal(404, answer.Status));
    }

    [Fact]
    [Trait("AC", "SSR-231")]
    public async Task PagesReadingCookiesAreNeverSharedAsync()
    {
        var site = new CachedSite();
        var page = new PageDefinition("/state", "Pages/inbox/Page.axaml", _ => new Model("unloaded"), [])
        {
            Loader = new LoaderDefinition((_, request, _) => ValueTask.FromResult(new LoaderOutcome(request.Cookies.GetValueOrDefault("sidebar_state") ?? "none")), static (_, data) => new Model((string)data!)),
        };

        var folded = await site.GetAsync(page, cookie: "sidebar_state=false");
        var open = await site.GetAsync(page, cookie: "sidebar_state=true");
        var kept = await site.GetAsync(site.Page());

        Assert.Contains(">false<", folded.Body, StringComparison.Ordinal);
        Assert.Contains(">true<", open.Body, StringComparison.Ordinal);
        Assert.Equal(1, site.Pages.Count);
        Assert.All([folded, open], answer => Assert.Equal(("private, no-cache", true), (answer.CacheControl, answer.Vary.Split(", ").Contains("Cookie"))));
        Assert.Equal(("public, no-cache", true), (kept.CacheControl, kept.Vary.Split(", ").Contains("Cookie")));
    }

    [Fact]
    [Trait("AC", "SSR-103")]
    public async Task UserPagesAreNeverCachedForACookieBearingRequestAsync()
    {
        var site = new CachedSite();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "ada")], "cookie"));
        var pages = new[]
        {
            site.Page() with { Access = [new PageAccess(null, null)] },
            new PageConventions().AuthorizeFolder("/inbox").Apply(site.Page()),
        };

        var answers = new List<Answer>();
        var loads = new List<int>();
        foreach (var page in pages)
        {
            var before = site.Loads;
            answers.Add(await site.GetAsync(page, user: user));
            answers.Add(await site.GetAsync(page, user: user));
            loads.Add(site.Loads - before);
        }

        Assert.Equal([4, 4], loads);
        Assert.Equal(0, site.Pages.Count);
        Assert.All(answers, answer => Assert.Equal("private, no-cache", answer.CacheControl));
        Assert.All(answers, answer => Assert.DoesNotContain("public", answer.CacheControl, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "SSR-104")]
    public async Task EveryPageAnswerCarriesAStrongTagAndRevalidatesAsync()
    {
        var site = new CachedSite();
        var page = site.Page(pageCached: false);

        var first = await site.GetAsync(page);
        var same = await site.GetAsync(page);
        var gzip = await site.GetAsync(page, encoding: "gzip");
        var brotli = await site.GetAsync(page, encoding: "br");
        var revalidations = new[]
        {
            await site.GetAsync(page, ifNoneMatch: first.ETag),
            await site.GetAsync(page, ifNoneMatch: "\"0000\", " + first.ETag),
            await site.GetAsync(page, ifNoneMatch: "W/" + first.ETag),
            await site.GetAsync(page, ifNoneMatch: "*"),
        };
        var stale = await site.GetAsync(page, ifNoneMatch: "\"0000\"");
        site.Title = "Changed";
        var changed = await site.GetAsync(page);
        var post = Site.Http();
        post.Request.Method = "POST";
        post.Request.ContentType = "application/x-www-form-urlencoded";
        post.Request.Body = new MemoryStream("_btn=1%7Cx"u8.ToArray());
        post.Request.Headers.IfNoneMatch = first.ETag;
        site.Engine.Command = _ => ValueTask.CompletedTask;
        await site.Handler.PostAsync(post, page);

        Assert.Matches("^\"[0-9a-f]{16}\"$", first.ETag);
        Assert.Equal((first.Body, first.ETag), (same.Body, same.ETag));
        Assert.Equal(3, new[] { same.ETag, gzip.ETag, brotli.ETag }.Distinct().Count());
        Assert.All(revalidations, answer => Assert.Equal((304, string.Empty, "private, no-cache"), (answer.Status, answer.Body, answer.CacheControl)));
        Assert.All(revalidations, answer => Assert.Matches("^\"[0-9a-f]{16}\"$", answer.ETag));
        Assert.Equal(200, stale.Status);
        Assert.NotEqual(first.ETag, changed.ETag);
        Assert.Equal((200, string.Empty, "no-store"), (post.Response.StatusCode, post.Response.Headers.ETag.ToString(), post.Response.Headers.CacheControl.ToString()));
    }

    [Fact]
    [Trait("AC", "SSR-105")]
    public async Task CachedPagesRevalidateWithoutLoadingOrRenderingAsync()
    {
        var site = new CachedSite();
        var page = site.Page();

        var document = await site.GetAsync(page);
        var layout = Layout(document.Body);
        var answers = new[]
        {
            document,
            await site.GetAsync(page, navigation: true),
            await site.GetAsync(page, navigation: true, layout: layout),
            await site.GetAsync(page, plan: site.Fingerprint(page)),
        };
        var (loads, renders) = (site.Loads, site.Renders);
        var revalidated = new[]
        {
            await site.GetAsync(page, ifNoneMatch: answers[0].ETag),
            await site.GetAsync(page, navigation: true, ifNoneMatch: answers[1].ETag),
            await site.GetAsync(page, navigation: true, layout: layout, ifNoneMatch: answers[2].ETag),
            await site.GetAsync(page, plan: site.Fingerprint(page), ifNoneMatch: answers[3].ETag),
        };

        Assert.Equal(4, answers.Select(answer => answer.ETag).Distinct().Count());
        Assert.All(revalidated, answer => Assert.Equal((304, string.Empty), (answer.Status, answer.Body)));
        Assert.Equal(answers.Select(answer => answer.ETag), revalidated.Select(answer => answer.ETag));
        Assert.Equal((loads, renders), (site.Loads, site.Renders));
    }

    [Fact]
    [Trait("AC", "SSR-106")]
    public async Task APageCacheOfNoBytesKeepsNothingAsync()
    {
        var site = new CachedSite(maxBytes: 0);
        var page = site.Page();

        var first = await site.GetAsync(page);
        var second = await site.GetAsync(page);

        Assert.Equal(4, site.Loads);
        Assert.Equal(0, site.Pages.Count);
        Assert.Equal((first.ETag, "public, no-cache"), (second.ETag, second.CacheControl));
        Assert.Matches("^\"[0-9a-f]{16}\"$", second.ETag);
    }

    private static string Layout(string body) => LayoutMeta().Match(body).Groups[1].Value;

    [GeneratedRegex("name=\"aioros-layout\" content=\"([0-9a-f]+)\"")]
    private static partial Regex LayoutMeta();

    internal sealed record Answer(int Status, string Body, string ETag, string CacheControl, string Vary, string Partial);

    private sealed class CachedSite
    {
        private int _loads;
        private int _renders;

        public CachedSite(long maxBytes = PageCache.DefaultMaxBytes)
        {
            Versions = new DataVersions();
            Pages = new PageCache(Versions, Time, maxBytes);
            Engine.Plan = new RenderPlan(
                [],
                [
                    new LiteralNode("<nav>shell</nav>" + PageRegion.Open + "<p>"),
                    new ValueNode(model =>
                    {
                        Interlocked.Increment(ref _renders);
                        return (model as IPageModel)?.Title;
                    }),
                    new LiteralNode("</p>" + PageRegion.Close),
                ],
                []);
            Handler = new PageHandler(Engine, Plans, new SsrOptions(), protection: null, pages: Pages);
        }

        public FakeTimeProvider Time { get; } = new();

        public DataVersions Versions { get; }

        public PageCache Pages { get; }

        public PlanCache Plans { get; } = new();

        public FakeEngine Engine { get; } = new();

        public PageHandler Handler { get; }

        public string Title { get; set; } = "Issues";

        public int Loads => Volatile.Read(ref _loads);

        public int Renders => Volatile.Read(ref _renders);

        public string Fingerprint(PageDefinition page) => Plans.TryGet(page.Route, "light", out var entry) ? entry.Page.Plan.Template.Fingerprint : string.Empty;

        public PageDefinition Page(bool pageCached = true, bool layoutLoader = true, bool notFound = false) =>
            new("/inbox", "Pages/inbox/Page.axaml", _ => new Model("unloaded"), [new LayoutDefinition("Pages/Layout.axaml", _ => new Model("shell"))
            {
                Loader = layoutLoader
                    ? new LoaderDefinition((_, _, _) => CountedAsync(new LoaderOutcome(Title)), static (_, data) => new Model((string)data!)) { Lifetime = TimeSpan.FromSeconds(60) }
                    : null,
            }])
            {
                Loader = new LoaderDefinition(
                    (services, _, _) =>
                    {
                        ((IDataKeys)services.GetService(typeof(IDataKeys))!).Read("issues");
                        return CountedAsync(notFound ? LoaderResult.NotFound() : new LoaderOutcome(Title));
                    },
                    static (_, data) => new Model((string)data!)) { Lifetime = pageCached ? TimeSpan.FromSeconds(30) : null },
            };

        private ValueTask<LoaderOutcome> CountedAsync(LoaderOutcome outcome)
        {
            Interlocked.Increment(ref _loads);
            return ValueTask.FromResult(outcome);
        }

        public async Task<Answer> GetAsync(PageDefinition page, string? query = null, string? theme = null, bool navigation = false, string? layout = null, string? plan = null, string? ifNoneMatch = null, ClaimsPrincipal? user = null, string? encoding = null, string? cookie = null)
        {
            var http = Site.Http(theme);
            http.Request.Method = "GET";
            if (query is not null)
            {
                http.Request.QueryString = new QueryString(query);
            }

            if (navigation)
            {
                http.Request.Headers[DocumentParts.NavigationHeader] = "1";
            }

            if (layout is not null)
            {
                http.Request.Headers[PageHandler.LayoutHeader] = layout;
            }

            if (plan is not null)
            {
                http.Request.Headers[PageHandler.PlanHeader] = plan;
            }

            if (ifNoneMatch is not null)
            {
                http.Request.Headers.IfNoneMatch = ifNoneMatch;
            }

            if (encoding is not null)
            {
                http.Request.Headers.AcceptEncoding = encoding;
            }

            if (cookie is not null)
            {
                http.Request.Headers.Cookie = cookie;
            }

            if (user is not null)
            {
                http.User = user;
                http.Request.Headers.Cookie = ".AspNetCore.Cookies=signed-in";
            }

            await Handler.GetAsync(http, page);
            return new Answer(
                http.Response.StatusCode,
                Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray()),
                http.Response.Headers.ETag.ToString(),
                http.Response.Headers.CacheControl.ToString(),
                http.Response.Headers.Vary.ToString(),
                http.Response.Headers[PageHandler.PartialHeader].ToString());
        }
    }
}
