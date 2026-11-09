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

public sealed class ScrollScriptTests
{
    [Fact]
    [Trait("AC", "SSR-230")]
    public async Task ViewersFollowTheirEndAndButtonsScrollThereAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <style>.v{display:flex;flex-direction:column-reverse;overflow-y:auto;height:100px}.v::before{content:"";flex:1 0 0}[data-aioros-scrolls][data-aioros-at-end]{display:none!important}</style>
            <div><div id="v" class="v" data-aioros-follow data-aioros-scroll-name="Log"><div id="c"><div id="m0" style="height:50px">0</div><div id="m1" style="height:50px">1</div><div id="m2" style="height:50px">2</div><div id="m3" style="height:50px">3</div><div id="m4" style="height:50px">4</div><div id="m5" style="height:50px">5</div><div id="m6" style="height:50px">6</div><div id="m7" style="height:50px">7</div></div></div>
            <button type="submit" name="_btn" value="0|" id="b" data-aioros-scrolls="Log">End</button></div>
            """);
        var page = stage.Page;
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" || request.ResourceType == "document" ? 1 : 0;
        const string Hidden = "() => document.getElementById('b').hasAttribute('data-aioros-at-end') && getComputedStyle(document.getElementById('b')).display === 'none'";
        const string Place = "() => Math.round(document.getElementById('m3').getBoundingClientRect().top - document.getElementById('v').getBoundingClientRect().top)";
        const string Grow = "() => { const m = document.createElement('div'); m.style.height = '60px'; m.textContent = 'new'; document.getElementById('c').append(m) }";
        const string Last = "() => { const v = document.getElementById('v').getBoundingClientRect(), c = document.getElementById('c').lastElementChild.getBoundingClientRect(); return Math.round(v.bottom - c.bottom) }";

        await page.WaitForFunctionAsync(Hidden, null, new() { PollingInterval = 20 });
        var opened = await page.EvaluateAsync<int>(Last);
        await page.EvaluateAsync("() => document.getElementById('v').scrollTop = -150");
        await page.WaitForFunctionAsync("() => !document.getElementById('b').hasAttribute('data-aioros-at-end')", null, new() { PollingInterval = 20 });
        var place = await page.EvaluateAsync<int>(Place);
        await page.EvaluateAsync(Grow);
        await page.WaitForFunctionAsync("() => document.getElementById('c').childElementCount === 9", null, new() { PollingInterval = 20 });
        var kept = await page.EvaluateAsync<int>(Place);
        await page.ClickAsync("#b");
        await page.WaitForFunctionAsync(Hidden, null, new() { PollingInterval = 20 });
        await page.WaitForFunctionAsync("() => document.getElementById('v').scrollTop === 0", null, new() { PollingInterval = 20 });
        var clicked = await page.EvaluateAsync<int>(Last);
        await page.EvaluateAsync(Grow);
        await page.WaitForFunctionAsync($"() => document.getElementById('c').childElementCount === 10 && ({Last})() === 0 && ({Hidden})()", null, new() { PollingInterval = 20 });
        var followed = await page.EvaluateAsync<int>(Last);

        Assert.Equal(0, opened);
        Assert.Equal(place, kept);
        Assert.Equal((0, 0, 0), (clicked, followed, requests));
    }

    [Fact]
    [Trait("AC", "SSR-241")]
    public async Task AFollowingViewerReturnsToItsEndWhenItsContentGrowsAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <style>.v{display:flex;flex-direction:column-reverse;overflow-y:auto;height:100px}.v::before{content:"";flex:1 0 0}</style>
            <div><div id="v" class="v" data-aioros-follow data-aioros-scroll-name="Log"><div id="c"><div style="height:50px">0</div><div style="height:50px">1</div><div style="height:50px">2</div><div style="height:50px">3</div></div></div>
            <button type="submit" name="_btn" value="0|" id="b" data-aioros-scrolls="Log">End</button></div>
            """);
        var page = stage.Page;
        const string Settled = "() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(() => done(document.getElementById('v').scrollTop))))";
        const string Grow = "() => { const m = document.createElement('div'); m.style.height = '60px'; m.textContent = 'new'; document.getElementById('c').append(m) }";

        await page.EvaluateAsync("() => document.getElementById('v').scrollTop = -1");
        await page.EvaluateAsync(Settled);
        var near = await page.EvaluateAsync<bool>("() => document.getElementById('b').hasAttribute('data-aioros-at-end')");
        await page.EvaluateAsync(Grow);
        var followed = await page.EvaluateAsync<double>(Settled);
        await page.EvaluateAsync("() => document.getElementById('v').scrollTop = -90");
        await page.WaitForFunctionAsync("() => !document.getElementById('b').hasAttribute('data-aioros-at-end')", null, new() { PollingInterval = 20 });
        await page.EvaluateAsync("() => { const m = document.createElement('div'); m.style.height = '60px'; document.getElementById('c').append(m) }");
        var away = await page.EvaluateAsync<double>(Settled);

        Assert.True(near);
        Assert.Equal(0, followed);
        Assert.True(away <= -90, $"{away}");
    }

    private sealed class Stage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page) : IAsyncDisposable
    {
        public IPage Page => page;

        public static async Task<Stage> StartAsync(string markup)
        {
            var steps = new Steps();
            var site = new Site();
            site.Engine.Command = _ =>
            {
                steps.Index++;
                return ValueTask.CompletedTask;
            };
            site.Engine.Plan = new RenderPlan([], [
                new LiteralNode("<button id=\"next\">Next</button><i id=\"step\" data-n=\""), new ValueNode(static model => ((Steps)model!).Index),
                new LiteralNode("\"></i>" + markup),
            ], []);
            var app = await HostAsync(site, [new PageDefinition("/scroll", "Pages/scroll/Page.axaml", _ => steps, [])]);
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
            var module = page.WaitForResponseAsync(response => response.Url.Contains(ScrollScript.Prefix, StringComparison.Ordinal));
            var plan = page.WaitForResponseAsync(response => response.Url.Contains("/_aioros/plan/", StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/scroll");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await plan.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", ScrollScript.Path);
            return new Stage(app, playwright, browser, page);
        }

        private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        private static async Task<WebApplication> HostAsync(Site site, PageDefinition[] pages)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
            var app = builder.Build();
            app.MapAiorosPages(pages);
            await app.StartAsync(TestContext.Current.CancellationToken);
            return app;
        }

        public async ValueTask DisposeAsync()
        {
            await browser.DisposeAsync();
            playwright.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class Steps : IPageModel
    {
        public string Title => "Scroll";

        public int Index { get; set; }
    }
}
