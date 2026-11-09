using Aioros.Pages;

namespace Aioros.Generators.Tests.Pages;

public sealed class PageHeadGeneratorTests
{
    [Fact]
    [Trait("AC", "GENERATOR-37")]
    public void StaticTitlesAndMetadataAreReadFromTheXaml()
    {
        var result = new GeneratorHarness()
            .Source("namespace App.Models; public sealed class ShellModel { }")
            .Xaml("Pages/Layout.axaml", "<a:Outlet />", "ShellModel", " a:Page.Title=\"Lumen\" a:Page.TitleTemplate=\"%s · Lumen\" a:Page.Description=\"Every issue.\" a:Page.Indexable=\"False\"")
            .Xaml("Pages/about/Page.axaml", "<TextBlock />", extra: " a:Page.Title=\"About\" a:Page.Canonical=\"/about\" a:Page.Image=\"/og/about.png\" a:Page.Type=\"article\"")
            .Xaml("Pages/home/Page.axaml", "<TextBlock />", extra: " a:Page.AbsoluteTitle=\"Lumen, the tracker\"")
            .Xaml("Pages/plain/Page.axaml", "<TextBlock />")
            .Run();
        var pages = result.Pages(out _);
        var about = pages.Single(page => page.Route == "/about");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new PageHead("Lumen", "%s · Lumen", false, new PageMetadata("Every issue.", Indexable: false)), about.Layouts[0].Head);
        Assert.Equal(new PageHead("About", Metadata: new PageMetadata(null, "/about", "/og/about.png", "article")), about.Head);
        Assert.Equal(new PageHead("Lumen, the tracker", AbsoluteTitle: true), pages.Single(page => page.Route == "/home").Head);
        Assert.Null(pages.Single(page => page.Route == "/plain").Head);
    }
}
