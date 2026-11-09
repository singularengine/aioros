using System.Text;
using Aioros.Runtime.Rendering;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class EngineAnswerCssTests
{
    [Fact]
    [Trait("AC", "SSR-258")]
    public async Task AnEngineAnswerKeepsThePlansCssAsync()
    {
        var planned = new Site();
        planned.Engine.Plan = new RenderPlan([], [new LiteralNode("<p>planned</p>")], []);
        await planned.Handler.GetAsync(Site.Http(), Site.Page(() => new Model()));
        planned.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Css = ".page{}\n.posted{}\n@container c (max-width:1px){.q{a:\"}\"}\n}\n" });
        var answered = Posted("f0=Inbox&_s0=0");
        await planned.Handler.PostAsync(answered, Site.Page(() => new Model()));
        var unplanned = new Site { Engine = { Reusable = false } };
        unplanned.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Css = ".posted{}\n" });
        var alone = Posted("f0=Inbox&_s0=0");
        await unplanned.Handler.PostAsync(alone, Site.Page(() => new Model()));

        Assert.Equal((200, 1, true), (answered.Response.StatusCode, Count(Site.Body(answered), ".page{}"), Site.Body(answered).Contains(".posted{}\n@container c (max-width:1px){.q{a:\"}\"}\n}", StringComparison.Ordinal)));
        Assert.Equal((200, false, true), (alone.Response.StatusCode, Site.Body(alone).Contains(".page{}", StringComparison.Ordinal), Site.Body(alone).Contains(".posted{}", StringComparison.Ordinal)));
    }

    private static int Count(string text, string part) => text.Split(part).Length - 1;

    private static Microsoft.AspNetCore.Http.DefaultHttpContext Posted(string body)
    {
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return http;
    }
}
