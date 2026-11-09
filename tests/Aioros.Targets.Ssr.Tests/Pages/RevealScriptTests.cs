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

public sealed class RevealScriptTests
{
    private const string Markup = """
        <style>@keyframes f{from{opacity:0}to{opacity:1}}.r{height:40px;animation:f 60s both}</style>
        <div id="s" style="height:500px;overflow-y:auto"><div id="a" class="r" data-aioros-reveal>A</div><div style="height:3000px"></div><div id="b" class="r" data-aioros-reveal>B</div></div>
        """;

    [Fact]
    [Trait("AC", "MOTION-23")]
    public async Task RevealedElementsPlayWhenTheyFirstScrollIntoViewAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Markup)], []);
        await using var app = await HostAsync(site, "/reveal");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" || request.ResourceType == "document" ? 1 : 0;
        var module = page.WaitForResponseAsync(response => response.Url.Contains(RevealScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/reveal");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await page.WaitForFunctionAsync("() => document.getElementById('a').hasAttribute('data-aioros-revealed')", null, new() { PollingInterval = 20 });
        const string Playing = "id => document.getElementById(id).getAnimations()[0].playState";
        const string State = "id => { const e = document.getElementById(id), a = e.getAnimations()[0]; return `${a.playState} ${a.currentTime === 0} ${getComputedStyle(e).opacity === '0'}` }";
        var opened = (await page.EvaluateAsync<string>(Playing, "a"), await page.EvaluateAsync<string>(State, "b"));

        await page.EvaluateAsync("() => document.getElementById('b').scrollIntoView()");
        await page.WaitForFunctionAsync("() => document.getElementById('b').hasAttribute('data-aioros-revealed') && document.getElementById('b').getAnimations()[0].currentTime > 0", null, new() { PollingInterval = 20 });
        var played = await page.EvaluateAsync<string>("() => document.getElementById('b').getAnimations()[0].playState");
        await page.EvaluateAsync("() => document.getElementById('s').scrollTop = 0");
        await page.EvaluateAsync("() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done)))");
        await page.EvaluateAsync("() => document.getElementById('b').scrollIntoView()");
        await page.EvaluateAsync("() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done)))");
        var once = await page.EvaluateAsync<bool>("() => { const a = document.getElementById('b').getAnimations()[0]; return a.playState === 'running' && a.currentTime > 0 }");
        var sent = requests - 1;

        var bare = await browser.NewPageAsync();
        await bare.SetContentAsync(Markup);
        var unscripted = await bare.EvaluateAsync<string>(Playing, "b");

        Assert.Equal(("running", "paused true true"), opened);
        Assert.Equal(("running", true, 0, "running"), (played, once, sent, unscripted));
    }

    [Fact]
    [Trait("AC", "MOTION-24")]
    public async Task RevealedElementsStayRevealedWhenThePageIsMorphedAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Markup.Replace("B</div>", "B</div><button id=\"send\" type=\"submit\" name=\"_btn\" value=\"0|\">Send</button>", StringComparison.Ordinal))], []);
        site.Engine.Command = _ => ValueTask.CompletedTask;
        await using var app = await HostAsync(site, "/reveal");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        var module = page.WaitForResponseAsync(response => response.Url.Contains(RevealScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/reveal");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("() => document.getElementById('b').scrollIntoView()");
        await page.WaitForFunctionAsync("() => document.getElementById('b').hasAttribute('data-aioros-revealed') && document.getElementById('b').getAnimations()[0].currentTime > 0", null, new() { PollingInterval = 20 });
        var before = await page.EvaluateAsync<double>("() => document.getElementById('b').getAnimations()[0].currentTime");

        await page.RunAndWaitForResponseAsync(() => page.ClickAsync("#send"), response => response.Request.Method == "POST");
        await page.EvaluateAsync("() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done)))");
        var after = await page.EvaluateAsync<string>("t => { const e = document.getElementById('b'), a = e.getAnimations()[0]; return `${e.hasAttribute('data-aioros-revealed')} ${a.playState} ${a.currentTime >= t}` }", before);

        Assert.Equal("true running true", after);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> HostAsync(Site site, string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition(path, $"Pages{path}/Page.axaml", _ => new Reveals(), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Reveals : IPageModel
    {
        public string Title => "Reveals";
    }
}
