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

public sealed class ParallaxScriptTests
{
    private const string Markup = """
        <div id="s" style="height:600px;overflow-y:auto"><div style="height:450px"></div><div id="p" data-aioros-parallax="0.5" style="height:100px;transform:rotate(1deg)">Layer</div><div style="height:2000px"></div></div>
        """;

    [Fact]
    [Trait("AC", "MOTION-34")]
    public async Task LayersShiftAgainstTheScrollByTheirShareAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Markup)], []);
        await using var app = await HostAsync(site, "/parallax");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });

        async Task<(string Opened, string Middle, string Transform)> ShiftsAsync(IPage page)
        {
            var module = page.WaitForResponseAsync(response => response.Url.Contains(RevealScript.Prefix, StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/parallax");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", RevealScript.Path);
            const string Settled = "() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(() => done(document.getElementById('p').style.translate))))";
            var opened = await page.EvaluateAsync<string>(Settled);
            await page.EvaluateAsync("() => document.getElementById('s').scrollTop = 200");
            var middle = await page.EvaluateAsync<string>(Settled);
            return (opened, middle, await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p')).transform"));
        }

        var moving = await ShiftsAsync(await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } }));
        var still = await ShiftsAsync(await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 }, ReducedMotion = ReducedMotion.Reduce }));

        Assert.Equal(("0px -100px", "0px", true), (moving.Opened, moving.Middle, moving.Transform.StartsWith("matrix(", StringComparison.Ordinal)));
        Assert.Equal((string.Empty, string.Empty), (still.Opened, still.Middle));
    }

    [Fact]
    [Trait("AC", "MOTION-36")]
    public async Task EachFrameReadsEveryLayerBeforeWritingAnyAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("""
            <div id="s" style="height:600px;overflow-y:auto"><div style="height:450px"></div><div id="a" data-aioros-parallax="0.5" style="height:100px">A</div><div id="b" data-aioros-parallax="0.2" style="height:100px">B</div><div id="c" data-aioros-parallax="0.8" style="height:100px">C</div><div style="height:2000px"></div></div>
            """)], []);
        await using var app = await HostAsync(site, "/parallax");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        await page.AddInitScriptAsync("""
            { window.shifts = () => [...document.querySelectorAll('[data-aioros-parallax]')].map(e => e.style.translate).join('|'); window.seen = [];
              const read = Element.prototype.getBoundingClientRect; Element.prototype.getBoundingClientRect = function () { if (this.dataset && this.dataset.aiorosParallax !== undefined) window.seen.push(window.shifts()); return read.call(this); }; }
            """);
        var module = page.WaitForResponseAsync(response => response.Url.Contains(RevealScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/parallax");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("u => import(u).then(() => 0)", RevealScript.Path);
        const string Settled = "() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(() => done(window.seen.length))))";
        await page.EvaluateAsync<int>(Settled);
        var before = await page.EvaluateAsync<string>("() => { window.seen = []; const shifts = window.shifts(); document.getElementById('s').scrollTop = 120; return shifts; }");
        await page.EvaluateAsync<int>(Settled);
        var seen = await page.EvaluateAsync<string[]>("() => window.seen");
        var after = await page.EvaluateAsync<string>("() => window.shifts()");

        Assert.Equal([before, before, before], seen);
        Assert.NotEqual(before, after);
        Assert.Equal("translate", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('b')).willChange"));
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> HostAsync(Site site, string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition(path, $"Pages{path}/Page.axaml", _ => new Layers(), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Layers : IPageModel
    {
        public string Title => "Layers";
    }
}
