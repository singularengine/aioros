using Aioros.Routing.Tables;
using Aioros.Routing.Templates;

namespace Aioros.Routing.Tests.Tables;

public sealed class RoutePrecedenceTests
{
    [Fact]
    [Trait("AC", "ROUTING-06")]
    public void LiteralsWinOverParametersRegardlessOfOrder()
    {
        var routes = RouteTable.Empty.Add("/issues/{id}").Add("/issues/new");

        Assert.Equal("/issues/new", routes.Match("/issues/new").Template);
    }

    [Fact]
    [Trait("AC", "ROUTING-07")]
    public void ConstraintsFilterParameterValues()
    {
        var routes = RouteTable.Empty.Add("/issues/{id:int}");

        Assert.Equal("42", routes.Match("/issues/42").Values["id"]);
        Assert.False(routes.Match("/issues/abc").IsMatch);
    }

    [Fact]
    [Trait("AC", "ROUTING-08")]
    public void ConstrainedParametersWinOverPlainParameters()
    {
        var routes = RouteTable.Empty.Add("/issues/{key}").Add("/issues/{id:int}");

        Assert.Equal("/issues/{id:int}", routes.Match("/issues/42").Template);
        Assert.Equal("/issues/{key}", routes.Match("/issues/abc").Template);
    }

    [Fact]
    [Trait("AC", "ROUTING-09")]
    public void CatchAllsCaptureTheRestAndLoseToLiterals()
    {
        var routes = RouteTable.Empty.Add("/docs/{*path}").Add("/docs/intro");

        Assert.Equal("guide/routing", routes.Match("/docs/guide/routing").Values["path"]);
        Assert.Equal("/docs/intro", routes.Match("/docs/intro").Template);
    }

    [Fact]
    [Trait("AC", "ROUTING-10")]
    public void EquivalentTemplatesConflict()
    {
        var routes = RouteTable.Empty.Add("/issues/{id}");

        var error = Assert.Throws<ArgumentException>(() => routes.Add("/issues/{key}"));

        Assert.Contains("/issues/{id}", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ROUTING-11")]
    public void CatchAllsMustBeLast()
    {
        Assert.Throws<ArgumentException>(() => RouteTemplate.Parse("/docs/{*path}/edit"));
    }
}
