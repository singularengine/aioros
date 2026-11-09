using System.Globalization;
using Aioros.Runtime.Rendering;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class DocumentWriteTests
{
    [Fact]
    [Trait("AC", "SSR-91")]
    public async Task LargePagesGrowNoArrayAndAllocateNothingPerRowAsync()
    {
        var rows = Enumerable.Range(0, 3000).Select(index => "row " + index.ToString(CultureInfo.InvariantCulture)).ToArray();
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<nav>Shell</nav>"), new LiteralNode(PageRegion.Open), new LiteralNode("<ul>"),
            new RepeatNode(_ => rows, [new LiteralNode("<li class=\"row\">"), new ValueNode(row => row), new LiteralNode("</li>")]),
            new LiteralNode("</ul>"), new LiteralNode(PageRegion.Close),
        ], []);
        var page = Site.Page(() => new Model("Rows"));
        var sized = Site.Http();
        await site.Handler.GetAsync(sized, page);
        var least = long.MaxValue;
        for (var run = 0; run < 4; run++)
        {
            var http = Site.Http(body: Stream.Null);
            var start = GC.GetAllocatedBytesForCurrentThread();
            var get = site.Handler.GetAsync(http, page);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - start);
            Assert.True(get.IsCompletedSuccessfully);
            await get;
        }

        var length = Site.Body(sized).Length;
        TestContext.Current.TestOutputHelper?.WriteLine($"document {length} B, allocated {least} B a request");

        Assert.True(length > 85_000);
        Assert.Equal(length, sized.Response.ContentLength);
        Assert.True(least < length / 4, $"{least} B allocated for a document of {length} B");
    }

    [Fact]
    [Trait("AC", "SSR-189")]
    public async Task FirefoxScrollbarsFollowTheColorSchemeAsync()
    {
        var site = new Site();
        var http = Site.Http(theme: "dark");

        await site.Handler.GetAsync(http, Site.Page(() => new Model("Dark")));

        Assert.Contains("@supports (-moz-appearance:none){:root{scrollbar-color:light-dark(#0006,#fff6) transparent}}", Site.Body(http), StringComparison.Ordinal);
    }
}
