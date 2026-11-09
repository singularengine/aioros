using Aioros.Routing.Tables;
using Aioros.Targets.Desktop.Handoff;

namespace Aioros.Targets.Desktop.Tests.Handoff;

public sealed class DeepLinkTests
{
    private static readonly RouteTable Routes = RouteTable.Empty.Add("/").Add("/orders").Add("/orders/{id:int}");

    [Theory]
    [Trait("AC", "INSTALL-01")]
    [InlineData("shop://open?path=%2Forders%2F42", "/orders/42")]
    [InlineData("SHOP://open/?path=/orders/42", "/orders/42")]
    [InlineData("shop://open?path=%2Forders%3Fq%3Dlate", "/orders?q=late")]
    [InlineData("shop://open?path=%2F", "/")]
    [InlineData("mail://open?path=%2Forders", null)]
    [InlineData("shop://orders?path=%2Forders", null)]
    [InlineData("shop://user@open?path=%2Forders", null)]
    [InlineData("shop://open:8080?path=%2Forders", null)]
    [InlineData("shop://open/orders?path=%2Forders", null)]
    [InlineData("shop://open?path=%2Forders#top", null)]
    [InlineData("shop://open?path=%2Forders&path=%2F", null)]
    [InlineData("shop://open?path=%2Forders&token=abc", null)]
    [InlineData("shop://open?code=abc&path=%2Forders", null)]
    [InlineData("shop://open", null)]
    [InlineData("shop://open?path=", null)]
    [InlineData("shop://open?path=orders", null)]
    [InlineData("shop://open?path=%2F%2Fevil.example%2Forders", null)]
    [InlineData("shop://open?path=%2Forders%5C42", null)]
    [InlineData("shop://open?path=%2Forders%0A42", null)]
    [InlineData("shop://open?path=https%3A%2F%2Fevil.example%2F", null)]
    [InlineData("shop://open?path=%2Forders%2Fabc", null)]
    [InlineData("shop://open?path=%2Fadmin", null)]
    [InlineData("not a link", null)]
    public void ADeepLinkCarriesOnlyAKnownPath(string link, string? path) =>
        Assert.Equal(path, DeepLink.Path(link, "shop", Routes));

    [Fact]
    [Trait("AC", "INSTALL-02")]
    public void TheFirstAcceptedDeepLinkChoosesTheFirstPath()
    {
        var linked = DeepLink.Start(["--desktop", "shop://open?path=%2Fadmin", "shop://open?path=%2Forders%2F7", "shop://open?path=%2Forders"], "shop", Routes, "/");
        var plain = DeepLink.Start(["--desktop", "--path=/orders"], "shop", Routes, "/orders");
        var refused = DeepLink.Start(["shop://open?path=%2Fadmin"], "shop", Routes, "/");

        Assert.Equal("/orders/7", linked.Path);
        Assert.Contains("shop://open?path=%2Fadmin", linked.Rejected, StringComparison.Ordinal);
        Assert.Equal(("/orders", (string?)null), plain);
        Assert.Equal("/", refused.Path);
        Assert.Contains("shop://open?path=%2Fadmin", refused.Rejected, StringComparison.Ordinal);
    }
}
