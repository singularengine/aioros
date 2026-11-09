using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class DetailsScriptTests
{
    [Fact]
    [Trait("AC", "SSR-235")]
    public async Task BoundDetailsArePostedAndFollowTheirModelAsync()
    {
        var steps = new Steps();
        var site = new Site();
        site.Engine.Command = _ =>
        {
            steps.Index++;
            return ValueTask.CompletedTask;
        };
        static bool Open(object? model) => ((Steps)model!).Index is 1 or 3;
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<button id=\"next\">Next</button><i id=\"step\" data-n=\""), new ValueNode(static model => ((Steps)model!).Index),
            new LiteralNode("\"></i><details id=\"a\" data-change"), new ConditionNode(static model => Open(model), [new LiteralNode(" open")]),
            new LiteralNode("><summary id=\"as\">A</summary><input type=\"checkbox\" hidden name=\"f0\""), new ConditionNode(static model => Open(model), [new LiteralNode(" checked")]),
            new LiteralNode("><p>A body</p></details><details id=\"b\"><summary id=\"bs\">B</summary><p>B body</p></details>"),
        ], []);
        await using var app = await HostAsync(site, steps);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var plan = page.WaitForResponseAsync(response => response.Url.Contains("/_aioros/plan/", StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/details");
        await plan.WaitAsync(TestContext.Current.CancellationToken);
        var posts = new List<string>();
        page.Request += (_, request) =>
        {
            if (request.Method == "POST")
            {
                posts.Add(Uri.UnescapeDataString(request.PostData ?? string.Empty));
            }
        };
        const string State = "() => [document.getElementById('a').open, document.getElementById('b').open].join()";
        async Task StepAsync(int n) => await page.WaitForFunctionAsync("n => document.getElementById('step').dataset.n === String(n)", n, new() { PollingInterval = 20 }).WaitAsync(TestContext.Current.CancellationToken);

        await page.ClickAsync("#as");
        await StepAsync(1);
        var opened = (await page.EvaluateAsync<string>(State), posts.Count);
        await page.ClickAsync("#bs");
        await page.ClickAsync("#next");
        await StepAsync(2);
        var closed = await page.EvaluateAsync<string>(State);
        await page.ClickAsync("#next");
        await StepAsync(3);
        var reopened = (await page.EvaluateAsync<string>(State), posts.Count);

        Assert.Equal(("true,false", 1), opened);
        Assert.Contains("f0=on", posts[0], StringComparison.Ordinal);
        Assert.Contains("_chg=0", posts[0], StringComparison.Ordinal);
        Assert.Equal("false,true", closed);
        Assert.Equal(("true,true", 3), reopened);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> HostAsync(Site site, Steps steps)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/details", "Pages/details/Page.axaml", _ => steps, [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Steps : IPageModel
    {
        public string Title => "Details";

        public int Index { get; set; }
    }
}
