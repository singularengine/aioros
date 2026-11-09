using Aioros.Routing.Files;

namespace Aioros.Routing.Tests.Files;

public sealed class FileRoutesTests
{
    [Fact]
    [Trait("AC", "ROUTING-12")]
    public void FoldersBecomeSegments()
    {
        Assert.Equal("/", FileRoutes.RouteOf("Pages/Page.axaml"));
        Assert.Equal("/issues", FileRoutes.RouteOf("Pages/issues/Page.axaml"));
    }

    [Fact]
    [Trait("AC", "ROUTING-13")]
    public void BracketFoldersBecomeParameters()
    {
        Assert.Equal("/issues/{id}", FileRoutes.RouteOf("Pages/issues/[id]/Page.axaml"));
        Assert.Equal("/issues/{id:int}", FileRoutes.RouteOf("Pages/issues/[id:int]/Page.axaml"));
    }

    [Fact]
    [Trait("AC", "ROUTING-14")]
    public void SpreadFoldersBecomeCatchAlls()
    {
        Assert.Equal("/docs/{*slug}", FileRoutes.RouteOf("Pages/docs/[...slug]/Page.axaml"));
        Assert.Equal("/shop/{*slug}", FileRoutes.RouteOf("Pages/shop/[[...slug]]/Page.axaml"));
    }

    [Fact]
    [Trait("AC", "ROUTING-15")]
    public void GroupsAreLeftOutOfTheRoute()
    {
        Assert.Equal("/pricing", FileRoutes.RouteOf("Pages/(marketing)/pricing/Page.axaml"));
    }

    [Fact]
    [Trait("AC", "ROUTING-16")]
    public void PrivateFoldersAndOtherFilesAreNotRoutes()
    {
        Assert.Null(FileRoutes.RouteOf("Pages/_components/Page.axaml"));
        Assert.Null(FileRoutes.RouteOf("Pages/issues/Card.axaml"));
        Assert.Null(FileRoutes.RouteOf("Views/issues/Page.axaml"));
    }

    [Fact]
    [Trait("AC", "ROUTING-17")]
    public void LayoutsWrapPagesFromTheOutside()
    {
        var map = FileRoutes.Map(
        [
            "Pages/Layout.axaml",
            "Pages/(app)/Layout.axaml",
            "Pages/(marketing)/Layout.axaml",
            "Pages/(app)/issues/[id]/Page.axaml",
        ]);

        var route = Assert.Single(map.Routes);
        Assert.Equal("/issues/{id}", route.Route);
        Assert.Equal(["Pages/Layout.axaml", "Pages/(app)/Layout.axaml"], route.Layouts);
    }

    [Fact]
    [Trait("AC", "ROUTING-18")]
    public void PagesResolvingToTheSameRouteConflict()
    {
        var map = FileRoutes.Map(["Pages/(a)/about/Page.axaml", "Pages/(b)/about/Page.axaml"]);

        var conflict = Assert.Single(map.Conflicts);
        Assert.Equal("/about", conflict.Route);
        Assert.Equal(["Pages/(a)/about/Page.axaml", "Pages/(b)/about/Page.axaml"], new[] { conflict.First, conflict.Second });
    }

    [Fact]
    [Trait("AC", "ROUTING-19")]
    public void BackslashesAreSeparators()
    {
        var route = Assert.Single(FileRoutes.Map([@"Pages\issues\[id]\Page.axaml", @"Pages\Layout.axaml"]).Routes);

        Assert.Equal("/issues/{id}", route.Route);
        Assert.Equal("Pages/issues/[id]/Page.axaml", route.Source);
        Assert.Equal(["Pages/Layout.axaml"], route.Layouts);
    }

    [Fact]
    [Trait("AC", "ROUTING-20")]
    public void NotFoundPagesAreNotRoutes()
    {
        var map = FileRoutes.Map(["Pages/Layout.axaml", "Pages/NotFound.axaml", "Pages/Page.axaml"]);

        Assert.Equal("/", Assert.Single(map.Routes).Route);
        Assert.Equal("Pages/NotFound.axaml", map.NotFound?.Source);
        Assert.Equal(["Pages/Layout.axaml"], map.NotFound?.Layouts);
    }

    [Fact]
    [Trait("AC", "ROUTING-21")]
    public void ErrorPagesAreNotRoutes()
    {
        var map = FileRoutes.Map(["Pages/Layout.axaml", "Pages/Error.axaml"]);

        Assert.Empty(map.Routes);
        Assert.Equal("Pages/Error.axaml", map.Error?.Source);
        Assert.Equal(["Pages/Layout.axaml"], map.Error?.Layouts);
    }
}
