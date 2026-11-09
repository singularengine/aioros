using System.Text;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Targets.Ssr.CrossSite;
using Aioros.Targets.Ssr.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class CrossSiteTests
{
    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS"];

    [Fact]
    [Trait("AC", "SSR-92")]
    public async Task SameOriginPostsRunTheirCommandAsync()
    {
        var sameOrigin = await PostAsync("same-origin", "http://app.test");
        var typed = await PostAsync("none", null);

        Assert.Equal((200, true), (sameOrigin.Http.Response.StatusCode, sameOrigin.Site.Engine.Posted is not null));
        Assert.Equal((200, true), (typed.Http.Response.StatusCode, typed.Site.Engine.Posted is not null));
    }

    [Fact]
    [Trait("AC", "SSR-93")]
    public async Task CrossSitePostsAreRefusedAsync()
    {
        var forged = await PostAsync("cross-site", "http://evil.test");
        var spoofed = await PostAsync("cross-site", "http://app.test");

        Assert.Equal((403, true), (forged.Http.Response.StatusCode, forged.Site.Engine.Posted is null));
        Assert.Equal((403, true), (spoofed.Http.Response.StatusCode, spoofed.Site.Engine.Posted is null));
        Assert.Equal(0, forged.Http.Response.Body.Length);
    }

    [Fact]
    [Trait("AC", "SSR-94")]
    public async Task SameSitePostsFromASiblingSubdomainAreRefusedAsync()
    {
        var sibling = await PostAsync("same-site", "http://evil.app.test");

        Assert.Equal((403, true), (sibling.Http.Response.StatusCode, sibling.Site.Engine.Posted is null));
    }

    [Fact]
    [Trait("AC", "SSR-95")]
    public async Task WithoutFetchMetadataTheOriginMustNameTheHostAsync()
    {
        var same = await PostAsync(null, "http://app.test");
        var otherHost = await PostAsync(null, "http://evil.test");
        var otherPort = await PostAsync(null, "http://app.test:8080");
        var opaque = await PostAsync(null, "null");

        Assert.Equal((200, true), (same.Http.Response.StatusCode, same.Site.Engine.Posted is not null));
        Assert.All([otherHost, otherPort, opaque], refused => Assert.Equal((403, true), (refused.Http.Response.StatusCode, refused.Site.Engine.Posted is null)));
    }

    [Fact]
    [Trait("AC", "SSR-96")]
    public async Task PostsWithoutFetchMetadataOrOriginRunTheirCommandAsync()
    {
        var client = await PostAsync(null, null);

        Assert.Equal((200, true), (client.Http.Response.StatusCode, client.Site.Engine.Posted is not null));
    }

    [Fact]
    [Trait("AC", "SSR-97")]
    public async Task TrustedOriginsAndBypassedRoutesAreAcceptedAsync()
    {
        var protection = new CrossOriginProtection { TrustedOrigins = ["https://admin.test"], InsecureBypassPatterns = ["/hooks"] };
        var trusted = await PostAsync("cross-site", "https://admin.test", protection);
        var untrusted = await PostAsync("cross-site", "https://evil.test", protection);
        var hook = await PostAsync("cross-site", "https://evil.test", protection, "/hooks");

        Assert.Equal((200, true), (trusted.Http.Response.StatusCode, trusted.Site.Engine.Posted is not null));
        Assert.Equal((403, true), (untrusted.Http.Response.StatusCode, untrusted.Site.Engine.Posted is null));
        Assert.Equal((200, true), (hook.Http.Response.StatusCode, hook.Site.Engine.Posted is not null));
        Assert.Throws<ArgumentException>(() => new CrossOriginProtection { TrustedOrigins = ["https://admin.test/path"] });
        Assert.Throws<ArgumentException>(() => new CrossOriginProtection { TrustedOrigins = ["https://admin.test?q=1"] });
        Assert.Throws<ArgumentException>(() => new CrossOriginProtection { TrustedOrigins = ["admin.test"] });
    }

    [Fact]
    [Trait("AC", "SSR-98")]
    public async Task SafeMethodsAreNeverCheckedAsync()
    {
        var site = new Site();
        var page = Site.Http();
        CrossSite(page.Request, "cross-site", "http://evil.test");
        var definition = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => null, [])
        {
            Loader = new LoaderDefinition((_, _, _) => ValueTask.FromResult(new LoaderOutcome("inbox")), (_, _) => throw new InvalidOperationException())
            {
                Write = static (writer, data) => writer.WriteStringValue((string?)data),
            },
            DataContract = "c1",
        };
        var data = Site.Http();
        data.Request.Headers["X-Aioros-Contract"] = "c1";
        CrossSite(data.Request, "cross-site", "http://evil.test");
        var protection = new CrossOriginProtection();
        var safe = SafeMethods.Select(method =>
        {
            var request = Site.Http().Request;
            request.Method = method;
            CrossSite(request, "cross-site", "http://evil.test");
            return protection.Allows(request, "/inbox");
        });

        await site.Handler.GetAsync(page, Site.Page(() => new Model()));
        await PageDataEndpoints.ServeAsync(data, definition);

        Assert.Equal(200, page.Response.StatusCode);
        Assert.Contains("<title>Inbox</title>", Site.Body(page), StringComparison.Ordinal);
        Assert.Equal(200, data.Response.StatusCode);
        Assert.Contains("\"data\":\"inbox\"", Site.Body(data), StringComparison.Ordinal);
        Assert.All(safe, Assert.True);
    }

    [Fact]
    [Trait("AC", "SSR-99")]
    public async Task PagesCarryNoAntiforgeryTokenByDefaultAsync()
    {
        var site = new Site();
        site.Engine.Fields = true;
        var page = Site.Http(antiforgery: true);
        var post = Site.Http(antiforgery: true);
        post.Request.Method = "POST";
        post.Request.ContentType = "application/x-www-form-urlencoded";
        post.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("f0=Ada&_btn=1%7Cx"));
        CrossSite(post.Request, "same-origin", "http://app.test");

        await site.Handler.GetAsync(page, Site.Page(() => new Model()));
        await site.Handler.PostAsync(post, Site.Page(() => new Model()));

        Assert.NotNull(page.RequestServices.GetService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>());
        Assert.Contains("<form method=\"post\"", Site.Body(page), StringComparison.Ordinal);
        Assert.DoesNotContain("__RequestVerificationToken", Site.Body(page), StringComparison.Ordinal);
        Assert.DoesNotContain("Antiforgery", page.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
        Assert.Equal((200, "Ada"), (post.Response.StatusCode, site.Engine.Posted?["f0"]));
    }

    private static async Task<(Site Site, DefaultHttpContext Http)> PostAsync(string? fetchSite, string? origin, CrossOriginProtection? protection = null, string route = "/inbox")
    {
        var site = new Site(protection: protection);
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.Path = route;
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("f0=Ada&_btn=1%7Cx"));
        CrossSite(http.Request, fetchSite, origin);
        await site.Handler.PostAsync(http, new PageDefinition(route, "Pages" + route + "/Page.axaml", _ => new Model(), []));
        return (site, http);
    }

    private static void CrossSite(HttpRequest request, string? fetchSite, string? origin)
    {
        if (fetchSite is not null)
        {
            request.Headers[CrossOriginProtection.FetchSiteHeader] = fetchSite;
        }

        if (origin is not null)
        {
            request.Headers.Origin = origin;
        }
    }
}
