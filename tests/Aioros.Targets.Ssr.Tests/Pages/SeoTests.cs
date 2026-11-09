using System.Xml.Linq;
using Aioros.Pages;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Aioros.Targets.Ssr.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class SeoTests
{
    [Fact]
    [Trait("AC", "SEO-01")]
    public async Task MetadataIsWrittenInTheHeadAsync()
    {
        var described = Site.Http();
        described.Request.Scheme = "https";
        var plain = Site.Http();
        var metadata = new PageMetadata("Plans & \"pricing\"", "/pricing", "/og/pricing.png", "article");

        await new Site().Handler.GetAsync(described, Site.Page(() => new Model("Pricing") { Metadata = metadata }));
        await new Site().Handler.GetAsync(plain, Site.Page(() => new Model("Plain")));

        var head = Head(Site.Body(described));
        Assert.Contains("<meta name=\"description\" content=\"Plans &amp; &quot;pricing&quot;\">", head, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"canonical\" href=\"https://app.test/pricing\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:title\" content=\"Pricing\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:url\" content=\"https://app.test/pricing\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:image\" content=\"https://app.test/og/pricing.png\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:type\" content=\"article\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"twitter:card\" content=\"summary_large_image\">", head, StringComparison.Ordinal);
        Assert.DoesNotContain("<meta name=\"description\"", Head(Site.Body(plain)), StringComparison.Ordinal);
        Assert.DoesNotContain("og:", Head(Site.Body(plain)), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SEO-02")]
    public async Task UnindexablePagesAskRobotsToSkipThemAsync()
    {
        var http = Site.Http();

        await new Site().Handler.GetAsync(http, Site.Page(() => new Model { Metadata = new PageMetadata(Indexable: false) }));

        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", Head(Site.Body(http)), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SEO-03")]
    public async Task SitemapsListPublicPagesAsync()
    {
        PageDefinition[] pages =
        [
            new("/", "Pages/Page.axaml", _ => null, []),
            new("/pricing", "Pages/pricing/Page.axaml", _ => null, []),
            new("/blog/{slug}", "Pages/blog/[slug]/Page.axaml", _ => null, []),
            new("/_not-found", "Pages/NotFound.axaml", _ => null, []),
            new("/files", "Pages/files/Page.axaml", _ => null, []) { Platforms = PagePlatforms.Desktop },
        ];
        var http = Site.Http();
        http.RequestServices = new ServiceCollection().AddSingleton<ISitemapSource>(new Posts()).BuildServiceProvider();

        await SearchEndpoints.SitemapAsync(http, pages);

        var xml = XDocument.Parse(Site.Body(http));
        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";
        Assert.Equal("application/xml; charset=utf-8", http.Response.ContentType);
        Assert.Equal(
            ["http://app.test/", "http://app.test/pricing", "http://app.test/blog/hello", "https://cdn.test/external"],
            xml.Descendants(sitemap + "loc").Select(location => location.Value));
    }

    [Fact]
    [Trait("AC", "SEO-04")]
    public async Task RobotsPointToTheSitemapAsync()
    {
        var http = Site.Http();

        await SearchEndpoints.RobotsAsync(http);

        Assert.Equal("text/plain; charset=utf-8", http.Response.ContentType);
        Assert.Equal("User-agent: *\nAllow: /\nDisallow: /_aioros/\nSitemap: http://app.test/sitemap.xml\n", Site.Body(http));
    }

    [Fact]
    [Trait("AC", "SEO-08")]
    public void MissingFaviconsAnswerNoContent()
    {
        var http = Site.Http();

        var result = SearchEndpoints.Favicon();
        Assert.Equal(StatusCodes.Status204NoContent, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Empty(Site.Body(http));
    }

    [Fact]
    [Trait("AC", "SEO-41")]
    public async Task AnIconInTheWebRootIsLinkedFromTheHeadAsync()
    {
        Assert.Equal("/favicon.svg", AppIcon.Find(name => name is "favicon.svg" or "favicon.png"));
        Assert.Equal("/favicon.png", AppIcon.Find(name => name == "favicon.png"));
        Assert.Null(AppIcon.Find(_ => false));
        var options = new SsrOptions { Icon = "/favicon.svg" };
        var page = Site.Http();
        var plain = Site.Http();
        var standIn = Site.Http();

        await new Site(options).Handler.GetAsync(page, Site.Page(() => new Model("Home")));
        await new Site().Handler.GetAsync(plain, Site.Page(() => new Model("Home")));
        await NativeOnlyStandIn.WriteAsync(standIn, "Files", new PageMetadata(), options);

        const string link = "<link rel=\"icon\" href=\"/favicon.svg\" type=\"image/svg+xml\">";
        Assert.Contains(link, Head(Site.Body(page)), StringComparison.Ordinal);
        Assert.Contains(link, Head(Site.Body(standIn)), StringComparison.Ordinal);
        Assert.DoesNotContain("rel=\"icon\"", Site.Body(plain), StringComparison.Ordinal);
        Assert.Contains("type=\"image/png\"", Head(Site.Body(await PageWithIconAsync("/favicon.png"))), StringComparison.Ordinal);
    }

    private static async Task<HttpContext> PageWithIconAsync(string icon)
    {
        var http = Site.Http();
        await new Site(new SsrOptions { Icon = icon }).Handler.GetAsync(http, Site.Page(() => new Model("Home")));
        return http;
    }

    private static string Head(string html) => html[..html.IndexOf("</head>", StringComparison.Ordinal)];

    private sealed class Posts : ISitemapSource
    {
        public async IAsyncEnumerable<string> UrlsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return "/blog/hello";
            yield return "https://cdn.test/external";
        }
    }
}
