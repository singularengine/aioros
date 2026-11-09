using Aioros.Routing.Tables;

namespace Aioros.Routing.Tests.Tables;

public sealed class RouteTableTests
{
    private static readonly RouteTable Routes = RouteTable.Empty
        .Add("/issues")
        .Add("/issues/{id}")
        .Add("/projects/{project}/boards/{board}");

    [Fact]
    [Trait("AC", "ROUTING-01")]
    public void ALiteralPathMatchesItsRoute()
    {
        var match = Routes.Match("/issues");

        Assert.True(match.IsMatch);
        Assert.Equal("/issues", match.Template);
        Assert.Equal(0, match.Values.Count);
    }

    [Fact]
    [Trait("AC", "ROUTING-02")]
    public void ParametersAreCapturedAndUnescaped()
    {
        var match = Routes.Match("/issues/LUM%2D105");

        Assert.True(match.IsMatch);
        Assert.Equal("/issues/{id}", match.Template);
        Assert.Equal("LUM-105", match.Values["id"]);
    }

    [Fact]
    [Trait("AC", "ROUTING-03")]
    public void SeveralParametersAreCaptured()
    {
        var match = Routes.Match("/projects/lumen/boards/v2");

        Assert.Equal("lumen", match.Values["project"]);
        Assert.Equal("v2", match.Values["board"]);
    }

    [Fact]
    [Trait("AC", "ROUTING-04")]
    public void LiteralsMatchWithoutCaseAndTrailingSlashes()
    {
        Assert.True(Routes.Match("/ISSUES/").IsMatch);
    }

    [Fact]
    [Trait("AC", "ROUTING-05")]
    public void AnUnknownPathMatchesNothing()
    {
        var match = Routes.Match("/settings");

        Assert.False(match.IsMatch);
        Assert.Equal(string.Empty, match.Template);
    }
}
