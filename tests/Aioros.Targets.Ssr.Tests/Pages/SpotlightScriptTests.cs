using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class SpotlightScriptTests
{
    [Fact]
    [Trait("AC", "MOTION-32")]
    public async Task TheGlowFollowsThePointerAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("""<div id="c" data-aioros-spotlight style="position:absolute;left:100px;top:50px;width:300px;height:200px">Card</div><div id="o" style="position:absolute;left:100px;top:300px;width:100px;height:100px">Other</div>""")], []);
        await using var app = await HostAsync(site, "/lit");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" || request.ResourceType == "document" ? 1 : 0;
        var module = page.WaitForResponseAsync(response => response.Url.Contains(RevealScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/lit");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("u => import(u).then(() => 0)", RevealScript.Path);
        const string Place = "id => { const s = document.getElementById(id).style; return s.getPropertyValue('--x') + ' ' + s.getPropertyValue('--y') }";

        await page.Mouse.MoveAsync(150, 300 + 10);
        var other = await page.EvaluateAsync<string>(Place, "o");
        await page.Mouse.MoveAsync(130, 90);
        await page.WaitForFunctionAsync("() => document.getElementById('c').style.getPropertyValue('--x') !== ''", null, new() { PollingInterval = 20 });
        var first = await page.EvaluateAsync<string>(Place, "c");
        await page.Mouse.MoveAsync(350, 200);
        await page.WaitForFunctionAsync("() => document.getElementById('c').style.getPropertyValue('--x') === '250px'", null, new() { PollingInterval = 20 });
        var moved = await page.EvaluateAsync<string>(Place, "c");

        Assert.Equal((" ", "30px 40px", "250px 150px", 1), (other, first, moved, requests));
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> HostAsync(Site site, string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition(path, $"Pages{path}/Page.axaml", _ => new Lit(), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Lit : IPageModel
    {
        public string Title => "Lit";
    }
}
