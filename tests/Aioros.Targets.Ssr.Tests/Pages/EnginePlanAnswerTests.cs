using System.Text;
using Aioros.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class EnginePlanAnswerTests
{
    [Fact]
    [Trait("AC", "SSR-305")]
    public async Task TheEngineRunsThePressAndThePlanAnswersAsync()
    {
        var site = new Site();
        site.Engine.OwnsCommands = true;
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<p>"), new ValueNode(static model => ((Model)model!).Title), new LiteralNode("</p><button type=\"submit\" name=\"_btn\" value=\"0|\">Go</button>")], []);
        site.Engine.Command = static models =>
        {
            ((Model)models[0]!).Title = "Pressed";
            return ValueTask.CompletedTask;
        };
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", static _ => new Model("Start"), []);

        var first = await PostAsync(site, page);
        var unplanned = site.Engine.PlanAnswered;
        await site.Handler.GetAsync(Site.Http(), page);
        var second = await PostAsync(site, page);

        Assert.False(unplanned);
        Assert.Contains("<p>Pressed</p>", Site.Body(first), StringComparison.Ordinal);
        Assert.True(site.Engine.PlanAnswered);
        Assert.Contains("<p>Pressed</p>", Site.Body(second), StringComparison.Ordinal);
    }

    private static async Task<Microsoft.AspNetCore.Http.HttpContext> PostAsync(Site site, PageDefinition page)
    {
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("_btn=0%7C"));
        await site.Handler.PostAsync(http, page);
        return http;
    }
}
