using System.Text;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class PlanMissTests
{
    private static RenderPlan Learned { get; } = new(
        [],
        [new LiteralNode("<p>"), new ValueNode(model => (model as Model)?.Title is "Known" or "Saved" ? "learned" : throw new PlanMissException("an unknown title")), new LiteralNode("</p>"), new IdNode { Field = new SlotSpec("text") }],
        []);

    private static RenderPlan Engine { get; } = new([], [new LiteralNode("<p>engine "), new ValueNode(model => (model as Model)?.Title), new LiteralNode("</p>")], []);

    [Fact]
    [Trait("AC", "SSR-152")]
    public async Task PagesTheirPlanCannotWriteAreRenderedByTheEngineAsync()
    {
        var site = new Site(fields: new FakeFields());
        site.Engine.PlanFor = _ => site.Engine.Renders.Count == 1 ? Learned : Engine;
        var known = Site.Http();
        var unknown = Site.Http();
        var dynamics = Site.Http();
        dynamics.Request.Headers[PageHandler.PlanHeader] = Learned.Template.Fingerprint;
        var posted = Posted("f0=Posted");
        var again = Site.Http();

        await site.Handler.GetAsync(known, Site.Page(() => new Model("Known")));
        await site.Handler.GetAsync(unknown, Site.Page(() => new Model("Other")));
        await site.Handler.GetAsync(dynamics, Site.Page(() => new Model("Other")));
        await site.Handler.PostAsync(posted, Site.Page(() => new Model("Other")));
        var renders = site.Engine.Renders.Count;
        await site.Handler.GetAsync(again, Site.Page(() => new Model("Known")));

        Assert.Contains("<p>learned</p>", Site.Body(known), StringComparison.Ordinal);
        Assert.Equal(200, unknown.Response.StatusCode);
        Assert.Contains("<p>engine Other</p>", Site.Body(unknown), StringComparison.Ordinal);
        Assert.Equal("text/html; charset=utf-8", dynamics.Response.ContentType);
        Assert.Contains("<p>engine Other</p>", Site.Body(dynamics), StringComparison.Ordinal);
        Assert.Equal("Posted", site.Engine.Posted?["f0"]);
        Assert.Contains("<p>learned</p>", Site.Body(again), StringComparison.Ordinal);
        Assert.Equal(renders, site.Engine.Renders.Count);
        Assert.Equal(2, site.Engine.Answers);
    }

    private static DefaultHttpContext Posted(string body)
    {
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return http;
    }
}
