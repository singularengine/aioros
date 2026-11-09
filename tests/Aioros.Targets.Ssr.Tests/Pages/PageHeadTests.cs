using Aioros.Pages;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class PageHeadTests
{
    private static readonly PageHead Shell = new("Lumen", "%s · Lumen");

    [Fact]
    [Trait("AC", "SEO-10")]
    public async Task StaticTitlesAndMetadataMergeWithLoadersAsync()
    {
        var site = new Site();
        var plain = Site.Http();
        var loaded = Site.Http();

        await site.Handler.GetAsync(plain, Page(new PageHead("About", Metadata: new PageMetadata(null, "/about", Type: "article")), new PageHead("Lumen", Metadata: new PageMetadata("Every issue.", Image: "/og/lumen.png"))));
        await site.Handler.GetAsync(loaded, Page(new PageHead("Static", Metadata: new PageMetadata("Static description.", "/static"))) with
        {
            Loader = new LoaderDefinition((_, _, _) => ValueTask.FromResult(new LoaderOutcome { Title = "Loaded", Metadata = new PageMetadata("Loaded description.") }), (_, _) => null),
        });

        var head = Head(plain);
        Assert.Contains("<title>About</title>", head, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\" content=\"Every issue.\">", head, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"canonical\" href=\"http://app.test/about\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:image\" content=\"http://app.test/og/lumen.png\">", head, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:type\" content=\"article\">", head, StringComparison.Ordinal);
        Assert.Contains("<title>Loaded</title>", Head(loaded), StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\" content=\"Loaded description.\">", Head(loaded), StringComparison.Ordinal);
        Assert.Contains("<link rel=\"canonical\" href=\"http://app.test/static\">", Head(loaded), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SEO-11")]
    public async Task LayoutTitleTemplatesApplyToTheTitlesBelowAsync()
    {
        var titles = new List<string>();
        foreach (var page in new[]
        {
            Page(new PageHead("About"), Shell),
            Page(null, Shell),
            Page(null, Shell) with { Loader = new LoaderDefinition((_, _, _) => ValueTask.FromResult(new LoaderOutcome { Title = "Issue 7" }), (_, _) => null) },
            Page(new PageHead("Profile"), Shell, new PageHead(TitleTemplate: "%s · Settings · Lumen")),
            Page(new PageHead("Lumen, the tracker", AbsoluteTitle: true), Shell),
        })
        {
            var http = Site.Http();
            await new Site().Handler.GetAsync(http, page);
            var head = Head(http);
            var start = head.IndexOf("<title>", StringComparison.Ordinal) + 7;
            titles.Add(head[start..head.IndexOf("</title>", StringComparison.Ordinal)]);
        }

        Assert.Equal(["About · Lumen", "Lumen", "Issue 7 · Lumen", "Profile · Settings · Lumen", "Lumen, the tracker"], titles);
    }

    [Fact]
    [Trait("AC", "SSR-108")]
    public async Task FormControlsInheritTheFontButNotTheLineHeightAsync()
    {
        var site = new Site();
        var http = Site.Http();

        await site.Handler.GetAsync(http, Site.Page(() => new Model("Inbox")));

        var head = Head(http);
        Assert.Contains("button,input,textarea,select{font-family:inherit;font-size:inherit;font-weight:inherit;font-style:inherit;", head, StringComparison.Ordinal);
        Assert.DoesNotContain("font:", head, StringComparison.Ordinal);
        Assert.DoesNotContain("line-height", head, StringComparison.Ordinal);
    }

    private static PageDefinition Page(PageHead? page, params PageHead?[] layouts) =>
        new("/inbox", "Pages/inbox/Page.axaml", _ => null, [.. layouts.Select(layout => new LayoutDefinition("Pages/Layout.axaml", _ => null) { Head = layout })]) { Head = page };

    private static string Head(HttpContext http) => Site.Body(http)[..Site.Body(http).IndexOf("</head>", StringComparison.Ordinal)];
}
