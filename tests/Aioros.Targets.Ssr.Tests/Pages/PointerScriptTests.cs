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

public sealed class PointerScriptTests
{
    private const string Markup = """
        <style>#p{width:400px;height:200px;margin:100px}#other{width:100px;height:100px}</style>
        <div id="other"></div>
        <div id="p" data-aioros-pointer></div>
        <svg id="vb" data-aioros-pointer="viewbox" viewBox="0 0 100 50" style="display:block;width:400px;height:200px"></svg>
        <svg id="meet" data-aioros-pointer="viewbox" viewBox="0 0 100 50" style="display:block;width:400px;height:100px"></svg>
        <div style="height:3000px"></div>
        """;

    private const string Counters = """
        () => {
            window.reads = 0;
            window.writes = 0;
            const read = Element.prototype.getBoundingClientRect;
            Element.prototype.getBoundingClientRect = function () { if (this.matches('[data-aioros-pointer]')) reads++; return read.call(this); };
            const write = CSSStyleDeclaration.prototype.setProperty;
            CSSStyleDeclaration.prototype.setProperty = function (n, ...rest) { if (n.startsWith('--aioros-pointer')) writes++; return write.call(this, n, ...rest); };
        }
        """;

    private const string Frame = "() => new Promise(r => requestAnimationFrame(() => r()))";

    private const string Values = "s => { const x = document.querySelector(s).style; return x.getPropertyValue('--aioros-pointer-x') + ',' + x.getPropertyValue('--aioros-pointer-y'); }";

    [Fact]
    [Trait("AC", "POINTER-01")]
    public async Task TheMousePositionIsWrittenOncePerFrameAndRemovedOnLeaveAsync()
    {
        await using var app = await StartAsync(Markup);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await OpenAsync(browser, app, new() { ViewportSize = new() { Width = 800, Height = 600 } });
        await page.EvaluateAsync(Counters);

        await MoveAsync(page, "#p", 60, 40);
        var first = await page.EvaluateAsync<string>(Values, "#p");
        await MoveAsync(page, "#p", 150, 120);
        var second = await page.EvaluateAsync<string>(Values, "#p");
        var reads = await page.EvaluateAsync<int>("() => reads");
        await page.EvaluateAsync("() => { writes = 0; const p = document.getElementById('p'), b = p.getBoundingClientRect(); reads--; [10, 20, 30].forEach(d => p.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerType: 'mouse', clientX: b.left + d, clientY: b.top + d }))); }");
        await page.EvaluateAsync(Frame);
        var coalesced = (await page.EvaluateAsync<int>("() => writes"), await page.EvaluateAsync<string>(Values, "#p"));
        await page.EvaluateAsync("() => scrollBy(0, 50)");
        await page.EvaluateAsync(Frame);
        await MoveAsync(page, "#p", 150, 150);
        var scrolled = (await page.EvaluateAsync<string>(Values, "#p"), await page.EvaluateAsync<int>("() => reads"));
        await MoveAsync(page, "#other", 10, 10);
        var left = await page.EvaluateAsync<string>(Values, "#p");
        await page.EvaluateAsync("() => { const p = document.getElementById('p'), b = p.getBoundingClientRect(); p.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerType: 'mouse', clientX: b.left + 5, clientY: b.top + 5 })); p.dispatchEvent(new PointerEvent('pointerleave', { pointerType: 'mouse' })); }");
        await page.EvaluateAsync(Frame);
        var pending = await page.EvaluateAsync<string>(Values, "#p");
        await page.EvaluateAsync("() => { const p = document.getElementById('p'), b = p.getBoundingClientRect(); p.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerType: 'touch', clientX: b.left + 5, clientY: b.top + 5 })); }");
        await page.EvaluateAsync(Frame);
        var touched = await page.EvaluateAsync<string>(Values, "#p");
        var other = await page.EvaluateAsync<string>(Values, "#other");
        await page.EvaluateAsync("() => { const p = document.getElementById('p'), b = p.getBoundingClientRect(); p.dispatchEvent(new PointerEvent('pointerover', { bubbles: true, pointerType: 'mouse', clientX: b.left + 7, clientY: b.top + 9 })); }");
        await page.EvaluateAsync(Frame);
        var over = await page.EvaluateAsync<string>(Values, "#p");

        Assert.Equal("60px,40px", first);
        Assert.Equal("150px,120px", second);
        Assert.Equal(1, reads);
        Assert.Equal((2, "30px,30px"), coalesced);
        Assert.Equal(("150px,150px", 2), scrolled);
        Assert.Equal(",", left);
        Assert.Equal(",", pending);
        Assert.Equal(",", touched);
        Assert.Equal(",", other);
        Assert.Equal("7px,9px", over);
    }

    [Fact]
    [Trait("AC", "POINTER-02")]
    public async Task AnSvgCanAskForItsViewBoxUnitsAsync()
    {
        await using var app = await StartAsync(Markup);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await OpenAsync(browser, app, new());

        await MoveAsync(page, "#vb", 200, 100);
        var scaled = await page.EvaluateAsync<string>(Values, "#vb");
        await MoveAsync(page, "#meet", 200, 50);
        var met = await page.EvaluateAsync<string>(Values, "#meet");

        Assert.Equal("50px,25px", scaled);
        Assert.Equal("50px,25px", met);
    }

    [Fact]
    [Trait("AC", "POINTER-03")]
    public async Task AMorphKeepsThePointerPropertiesAsync()
    {
        var steps = new Steps();
        var site = new Site();
        site.Engine.Command = _ =>
        {
            steps.Index++;
            return ValueTask.CompletedTask;
        };
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<button id=\"next\">Next</button><style>#p{width:400px;height:200px}</style><div id=\"p\" data-aioros-pointer data-n=\""),
            new ValueNode(static model => ((Steps)model!).Index),
            new LiteralNode("\"></div>"),
        ], []);
        await using var app = await HostAsync(site, steps);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await OpenAsync(browser, app, new());

        await MoveAsync(page, "#p", 30, 20);
        var before = await page.EvaluateAsync<string>(Values, "#p");
        await page.EvaluateAsync("() => document.getElementById('next').click()");
        await page.WaitForFunctionAsync("() => document.getElementById('p').dataset.n === '1'").WaitAsync(TestContext.Current.CancellationToken);
        var after = await page.EvaluateAsync<string>(Values, "#p");

        Assert.Equal("30px,20px", before);
        Assert.Equal("30px,20px", after);
    }

    [Fact]
    [Trait("AC", "POINTER-04")]
    public async Task ThePointerModuleLoadsOnlyWherePagesDeclareItAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var site = new Site();
        site.Engine.PlanFor = page => new RenderPlan([], [new LiteralNode(page.Route == "/chart" ? "<svg data-aioros-pointer></svg>" : "<a id=\"go\" href=\"/chart\">Chart</a><button id=\"next\">Next</button>")], []);
        await using var app = await HostAsync(site, [new PageDefinition("/plain", "Pages/plain/Page.axaml", _ => new Steps(), []), new PageDefinition("/chart", "Pages/chart/Page.axaml", _ => new Steps(), [])]);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var requested = new List<string>();
        var page = await browser.NewPageAsync();
        page.Request += (_, request) =>
        {
            if (request.Url.Contains(PointerScript.Prefix, StringComparison.Ordinal))
            {
                requested.Add(new Uri(request.Url).AbsolutePath);
            }
        };

        await page.GotoAsync(Address(app) + "/plain");
        var posted = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#next");
        await posted;
        await page.GotoAsync(Address(app) + "/plain");
        var plain = requested.Count;
        var loaded = page.WaitForResponseAsync(response => response.Url.Contains(PointerScript.Prefix, StringComparison.Ordinal));
        await page.ClickAsync("#go");
        var module = await loaded;
        var headers = await module.AllHeadersAsync();
        var direct = await browser.NewPageAsync();
        var directModule = direct.WaitForRequestAsync(request => request.Url.Contains(PointerScript.Prefix, StringComparison.Ordinal));
        var hit = await direct.GotoAsync(Address(app) + "/chart");
        await directModule.WaitAsync(bound);
        using var http = new HttpClient();
        var missing = await http.GetAsync(new Uri(Address(app) + PointerScript.Prefix + "0000000000000000.js"), bound);

        Assert.Equal(0, plain);
        Assert.Equal([PointerScript.Path], requested);
        Assert.Equal("text/javascript; charset=utf-8", headers["content-type"]);
        Assert.Equal("public, max-age=31536000, immutable", headers["cache-control"]);
        Assert.True(hit!.Ok);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static async Task<IPage> OpenAsync(IBrowser browser, WebApplication app, BrowserNewPageOptions options)
    {
        var page = await browser.NewPageAsync(options);
        var module = page.WaitForResponseAsync(response => response.Url.Contains(PointerScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/pointer");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("u => import(u).then(() => 0)", PointerScript.Path);
        return page;
    }

    private static async Task MoveAsync(IPage page, string selector, float x, float y)
    {
        await page.Locator(selector).ScrollIntoViewIfNeededAsync();
        var box = (await page.Locator(selector).BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(box.X + x, box.Y + y);
        await page.EvaluateAsync(Frame);
    }

    private static Task<IBrowser> LaunchAsync(IPlaywright playwright) =>
        playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static Task<WebApplication> StartAsync(string markup)
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(markup)], []);
        return HostAsync(site, new Steps());
    }

    private static Task<WebApplication> HostAsync(Site site, Steps steps) =>
        HostAsync(site, [new PageDefinition("/pointer", "Pages/pointer/Page.axaml", _ => steps, [])]);

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

    private sealed class Steps : IPageModel
    {
        public string Title => "Pointer";

        public int Index { get; set; }
    }
}
