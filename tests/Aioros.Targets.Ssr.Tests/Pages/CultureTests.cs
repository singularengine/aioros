using System.Globalization;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class CultureTests
{
    private static readonly string Cookie = CookieRequestCultureProvider.DefaultCookieName + "=" + Uri.EscapeDataString(CookieRequestCultureProvider.MakeCookieValue(new RequestCulture("fr-FR")));

    private const string French = "fr-FR/fr-FR";

    private const string English = "en-US/en-US";

    private static IServiceCollection Localize(IServiceCollection services) =>
        services.AddRequestLocalization(static options => options.SetDefaultCulture("en-US").AddSupportedCultures("en-US", "fr-FR").AddSupportedUICultures("en-US", "fr-FR"));

    private static async Task<WebApplication> StartAsync(Site site, bool localized, params PageDefinition[] pages)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor().AddLocalizedRequests();
        if (localized)
        {
            Localize(builder.Services);
        }

        var app = builder.Build();
        app.MapAiorosPages(pages.Length == 0 ? [Site.Page(static () => new Model())] : pages);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<(HttpResponseMessage Response, string Body)> GetAsync(HttpClient client, string path, string? language = null, string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (language is not null)
        {
            request.Headers.Add("Accept-Language", language);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return (response, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static CompiledPage Literal(string text) => new(new RenderPlan([], [new LiteralNode(text)], []), ".p{}\n", "#fff", "#000", Reusable: true);

    [Fact]
    [Trait("AC", "SSR-214")]
    public async Task TheVisitorsRequestChoosesTheCultureAsync()
    {
        var site = new Site();
        site.Engine.Reusable = false;
        await using var app = await StartAsync(site, localized: true);
        using var client = app.GetTestClient();

        await GetAsync(client, "/inbox?culture=fr-FR");
        await GetAsync(client, "/inbox", cookie: Cookie);
        await GetAsync(client, "/inbox", language: "fr-FR,fr;q=0.9");
        await GetAsync(client, "/inbox", language: "de-DE");
        await GetAsync(client, "/inbox");

        Assert.Equal([French, French, French, English, English], site.Engine.Cultures);
    }

    [Fact]
    [Trait("AC", "SSR-215")]
    public async Task AnswersNameTheirLanguageAndWhatChoseItAsync()
    {
        var site = new Site();
        await using var app = await StartAsync(site, localized: true);
        using var client = app.GetTestClient();

        var header = await GetAsync(client, "/inbox", language: "fr-FR");
        var standard = await GetAsync(client, "/inbox");
        var cookie = await GetAsync(client, "/inbox", cookie: Cookie);
        var query = await GetAsync(client, "/inbox?culture=fr-FR");

        Assert.Equal(["fr-FR"], header.Response.Content.Headers.ContentLanguage);
        Assert.Contains("<html lang=\"fr-FR\">", header.Body, StringComparison.Ordinal);
        Assert.Contains("Accept-Language", header.Response.Headers.Vary);
        Assert.Contains("X-Aioros-Navigation", header.Response.Headers.Vary);
        Assert.Equal(["en-US"], standard.Response.Content.Headers.ContentLanguage);
        Assert.Contains("<html lang=\"en-US\">", standard.Body, StringComparison.Ordinal);
        Assert.Contains("Accept-Language", standard.Response.Headers.Vary);
        Assert.Contains("Cookie", cookie.Response.Headers.Vary);
        Assert.DoesNotContain("Accept-Language", cookie.Response.Headers.Vary);
        Assert.Equal(["fr-FR"], query.Response.Content.Headers.ContentLanguage);
        Assert.DoesNotContain("Accept-Language", query.Response.Headers.Vary);
    }

    [Fact]
    [Trait("AC", "SSR-216")]
    public async Task AppsThatDoNotLocalizeAnswerAsBeforeAsync()
    {
        var site = new Site();
        await using var app = await StartAsync(site, localized: false);
        using var client = app.GetTestClient();

        var (response, body) = await GetAsync(client, "/inbox", language: "fr-FR");

        Assert.Contains("<html lang=\"en\">", body, StringComparison.Ordinal);
        Assert.Empty(response.Content.Headers.ContentLanguage);
        Assert.DoesNotContain("Accept-Language", response.Headers.Vary);
        Assert.Equal([CultureInfo.CurrentCulture.Name + "/" + CultureInfo.CurrentUICulture.Name], site.Engine.Cultures);
    }

    [Fact]
    [Trait("AC", "SSR-217")]
    public async Task PlansAreKeptPerCultureAsync()
    {
        var site = new Site();
        await using var app = await StartAsync(site, localized: true);
        using var client = app.GetTestClient();

        await GetAsync(client, "/inbox", language: "fr-FR");
        await GetAsync(client, "/inbox", language: "en-US");
        var again = await GetAsync(client, "/inbox", language: "fr-FR");

        Assert.Equal([French, English], site.Engine.Cultures);
        Assert.Equal(2, site.Cache.Count);
        Assert.Contains("<p>Inbox</p>", again.Body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-219")]
    public async Task WarmupCompilesEveryCultureAndThemeAsync()
    {
        var site = new Site();
        await using var services = Localize(new ServiceCollection()).BuildServiceProvider();

        var outcome = Assert.Single(await Warmup.RunAsync([Site.Page(static () => new Model())], services, site.Handler, TestContext.Current.CancellationToken));

        Assert.Equal(["en-US light", "en-US dark", "fr-FR light", "fr-FR dark"], outcome.Entries.Select(entry => entry.Culture + " " + entry.Theme));
        Assert.Equal([English, English, French, French], site.Engine.Cultures);
        Assert.Equal(4, site.Cache.Count);
    }

    [Fact]
    [Trait("AC", "SSR-220")]
    public async Task PrecompiledPlansAreServedPerCultureAsync()
    {
        var site = new Site();
        var page = Site.Page(static () => new Model());
        site.Cache.Preload(
            [
                new PlannedPage(page.Route, "light", Literal("<p>English</p>")) { Culture = "en-US" },
                new PlannedPage(page.Route, "light", Literal("<p>Français</p>")) { Culture = "fr-FR" },
            ],
            [page],
            site.Options);
        await using var app = await StartAsync(site, localized: true, page);
        using var client = app.GetTestClient();

        var french = await GetAsync(client, "/inbox", language: "fr-FR");
        var english = await GetAsync(client, "/inbox", language: "en-US");

        Assert.Contains("<p>Français</p>", french.Body, StringComparison.Ordinal);
        Assert.Contains("<p>English</p>", english.Body, StringComparison.Ordinal);
        Assert.Empty(site.Engine.Renders);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-221")]
    public async Task PostsAndLiveRefreshesRunInTheVisitorsCultureAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var site = new Site();
        site.Engine.Reusable = false;
        var model = new Model();
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => model, [], Live: true);
        await using var app = await StartAsync(site, localized: true, page);
        using var client = app.GetTestClient();
        using var form = new HttpRequestMessage(HttpMethod.Post, "/inbox") { Content = new FormUrlEncodedContent([new("_btn", "0|")]) };
        form.Headers.Add("Accept-Language", "fr-FR");

        using var posted = await client.SendAsync(form, token);
        var afterPost = site.Engine.Cultures.ToList();
        using var live = new HttpRequestMessage(HttpMethod.Get, "/inbox/live");
        live.Headers.Add("Accept-Language", "fr-FR");
        using var stream = await client.SendAsync(live, HttpCompletionOption.ResponseHeadersRead, token);
        using var reader = new StreamReader(await stream.Content.ReadAsStreamAsync(token));
        while (await reader.ReadLineAsync(token) is { } opening && !opening.StartsWith("data:", StringComparison.Ordinal))
        {
        }

        site.Engine.Cultures.Clear();
        model.Title = "Refreshed";
        string? line;
        do
        {
            line = await reader.ReadLineAsync(token);
        }
        while (line is not null && !line.StartsWith("data:", StringComparison.Ordinal));

        Assert.Equal([French], afterPost);
        Assert.Equal([French], site.Engine.Cultures);
    }
}
