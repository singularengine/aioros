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

public sealed class FocusScriptTests
{
    private const string Markup = """
        <div style="height:200px"></div>
        <p id="outside" style="height:60px;margin:0">Outside</p>
        <div id="chart" data-aioros-follow data-aioros-roving="horizontal" style="display:flex;width:500px;height:100px">
        <div id="pad" style="width:100px"></div>
        <div id="c0" tabindex="0" style="flex:1"></div><div id="c1" tabindex="-1" style="flex:1"></div>
        <div id="c2" tabindex="-1" style="flex:1"><span id="s2">two</span></div><div id="c3" tabindex="-1" style="flex:1"></div>
        <input id="field" style="width:60px">
        </div>
        <div id="cap" data-aioros-follow="capture" style="display:flex;width:500px;height:100px">
        <div id="kpad" style="width:100px"></div>
        <div id="k0" tabindex="-1" style="flex:1"></div><div id="k1" tabindex="-1" style="flex:1"><span id="ks1">one</span></div><div id="k2" tabindex="-1" style="flex:1"></div>
        </div>
        <div id="v" data-aioros-roving="vertical"><div id="v0" tabindex="0">a</div><div id="v1" tabindex="-1">b</div><div id="v2" tabindex="-1">c</div></div>
        <div id="b" data-aioros-roving="both"><div id="b0" tabindex="0">a</div><div id="b1" tabindex="-1">b</div><div id="b2" tabindex="-1">c</div></div>
        <div id="e" data-aioros-roving="horizontal" data-aioros-roving-escape><div id="e0" tabindex="0">a</div><div id="e1" tabindex="-1">b</div></div>
        <div id="n" data-aioros-roving="horizontal" data-aioros-roving-ends="off"><div id="n0" tabindex="0">a</div><div id="n1" tabindex="-1">b</div><div id="n2" tabindex="-1">c</div></div>
        <div id="w" data-aioros-roving="horizontal" data-aioros-roving-ends="off" data-aioros-roving-tabs="keep"><button id="w0" type="button">a</button><a id="w1" href="#x">b</a><button id="w2" type="button" popovertarget="wp">c</button><div id="wp" popover><a id="wp0" href="#y">in</a></div><button id="w3" type="button" disabled>d</button></div>
        <div style="height:2000px"></div>
        """;

    private const string Active = "() => document.activeElement.id || document.activeElement.tagName";

    [Fact]
    [Trait("AC", "FOCUS-01")]
    public async Task TheFocusFollowsTheFingerAsync()
    {
        await using var app = await StartAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await TouchPageAsync(browser, app);
        var finger = await Finger.OnAsync(page);
        var seen = new List<string>();

        await finger.TouchAsync("touchStart", await CenterAsync(page, "#c0"));
        seen.Add(await page.EvaluateAsync<string>(Active));
        await finger.TouchAsync("touchMove", await CenterAsync(page, "#s2"));
        seen.Add(await page.EvaluateAsync<string>(Active));
        await finger.TouchAsync("touchMove", await CenterAsync(page, "#c3"));
        seen.Add(await page.EvaluateAsync<string>(Active));
        await finger.TouchAsync("touchMove", await CenterAsync(page, "#pad"));
        seen.Add(await page.EvaluateAsync<string>(Active));
        await finger.TouchAsync("touchEnd", null);
        seen.Add(await page.EvaluateAsync<string>(Active));
        var outside = await CenterAsync(page, "#outside");
        await finger.TouchAsync("touchStart", outside);
        await finger.TouchAsync("touchMove", (outside.X + 30, outside.Y));
        seen.Add(await page.EvaluateAsync<string>(Active));

        Assert.Equal(["c0", "c2", "c3", "c3", "c3", "c3"], seen);
    }

    [Fact]
    [Trait("AC", "FOCUS-02")]
    public async Task TouchListenersLetThePageScrollUnlessCapturedAsync()
    {
        await using var app = await StartAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await TouchPageAsync(browser, app);
        var finger = await Finger.OnAsync(page);

        var actions = await page.EvaluateAsync<string>("() => ['chart', 'cap'].map(id => getComputedStyle(document.getElementById(id)).touchAction).join(',')");
        var start = await CenterAsync(page, "#k0");
        var next = await CenterAsync(page, "#k1");
        await finger.TouchAsync("touchStart", start);
        await finger.TouchAsync("touchMove", (start.X, start.Y - 40));
        await finger.TouchAsync("touchMove", (next.X, start.Y - 40));
        await finger.TouchAsync("touchEnd", null);
        var captured = (await page.EvaluateAsync<double>("() => scrollY"), await page.EvaluateAsync<string>(Active));
        await finger.DragUpAsync(await CenterAsync(page, "#c2"));
        await page.WaitForFunctionAsync("() => scrollY > 0").WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal("auto,none", actions);
        Assert.Equal((0d, "k1"), captured);
    }

    [Fact]
    [Trait("AC", "FOCUS-03")]
    public async Task HorizontalRovingFocusMovesWithTheArrowsAsync()
    {
        await using var app = await StartAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        await page.GotoAsync(Address(app) + "/focus");
        await page.EvaluateAsync("() => { window.prevented = []; addEventListener('keydown', e => prevented.push(e.defaultPrevented)); }");

        await page.FocusAsync("#c1");
        var seen = await PressAsync(page, "ArrowRight", "ArrowLeft", "End", "ArrowRight", "Home", "ArrowLeft", "ArrowUp", "ArrowDown");
        var prevented = await page.EvaluateAsync<bool[]>("() => prevented");
        var stops = await page.EvaluateAsync<string>("() => [...document.querySelectorAll('#chart [tabindex]')].map(x => x.getAttribute('tabindex')).join(',')");
        await page.ClickAsync("#c2");
        var clicked = await page.EvaluateAsync<string>("() => [...document.querySelectorAll('#chart [tabindex]')].map(x => x.getAttribute('tabindex')).join(',')");
        await page.FocusAsync("#field");
        await page.EvaluateAsync("() => { prevented.length = 0; }");
        var typed = await PressAsync(page, "ArrowLeft", "Home");
        var left = await page.EvaluateAsync<bool[]>("() => prevented");

        Assert.Equal(["c2", "c1", "c3", "c3", "c0", "c0", "c0", "c0"], seen);
        Assert.Equal([true, true, true, true, true, true, false, false], prevented);
        Assert.Equal("0,-1,-1,-1", stops);
        Assert.Equal("-1,-1,0,-1", clicked);
        Assert.Equal(["field", "field"], typed);
        Assert.Equal([false, false], left);
    }

    [Fact]
    [Trait("AC", "FOCUS-04")]
    public async Task VerticalAndBothRovingFocusFollowTheirArrowsAsync()
    {
        await using var app = await StartAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        await page.GotoAsync(Address(app) + "/focus");

        await page.FocusAsync("#v0");
        var vertical = await PressAsync(page, "ArrowDown", "ArrowUp", "ArrowRight", "ArrowLeft");
        await page.FocusAsync("#b0");
        var both = await PressAsync(page, "ArrowDown", "ArrowUp", "ArrowRight", "ArrowLeft");

        Assert.Equal(["v1", "v0", "v0", "v0"], vertical);
        Assert.Equal(["b1", "b0", "b1", "b0"], both);
    }

    [Fact]
    [Trait("AC", "FOCUS-05")]
    public async Task EscapeBlursOnlyWhenTheRovingElementAsksAsync()
    {
        await using var app = await StartAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        await page.GotoAsync(Address(app) + "/focus");

        await page.FocusAsync("#e1");
        var asked = await PressAsync(page, "Escape");
        await page.FocusAsync("#c1");
        var kept = await PressAsync(page, "Escape");

        Assert.Equal(["BODY"], asked);
        Assert.Equal(["c1"], kept);
    }

    [Fact]
    [Trait("AC", "FOCUS-06")]
    public async Task RovingEndsOffLeavesHomeAndEndAloneAsync()
    {
        await using var app = await StartAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        await page.GotoAsync(Address(app) + "/focus");
        await page.EvaluateAsync("() => { window.prevented = []; addEventListener('keydown', e => prevented.push(e.defaultPrevented)); }");

        await page.FocusAsync("#n1");
        var seen = await PressAsync(page, "Home", "End", "ArrowRight", "ArrowLeft");
        var prevented = await page.EvaluateAsync<bool[]>("() => prevented");

        Assert.Equal(["n1", "n1", "n2", "n1"], seen);
        Assert.Equal([false, false, true, true], prevented);
    }

    [Fact]
    [Trait("AC", "FOCUS-11")]
    public async Task RovingThatKeepsTabsMovesOverFocusableItemsAsync()
    {
        await using var app = await StartAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        await page.GotoAsync(Address(app) + "/focus");

        await page.FocusAsync("#w0");
        var seen = await PressAsync(page, "ArrowRight", "ArrowRight", "ArrowRight", "ArrowLeft");
        var tabs = await page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('#w [id]')].map(x => x.id + ':' + (x.getAttribute('tabindex') ?? ''))");

        Assert.Equal(["w1", "w2", "w2", "w1"], seen);
        Assert.All(tabs, tab => Assert.EndsWith(":", tab, StringComparison.Ordinal));
    }

    private static async Task<List<string>> PressAsync(IPage page, params string[] keys)
    {
        var seen = new List<string>();
        foreach (var key in keys)
        {
            await page.Keyboard.PressAsync(key);
            seen.Add(await page.EvaluateAsync<string>(Active));
        }

        return seen;
    }

    private static async Task<IPage> TouchPageAsync(IBrowser browser, WebApplication app)
    {
        var context = await browser.NewContextAsync(new() { HasTouch = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(Address(app) + "/focus");
        return page;
    }

    private static async Task<(double X, double Y)> CenterAsync(IPage page, string selector)
    {
        var box = (await page.Locator(selector).BoundingBoxAsync())!;
        return (box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    private sealed class Finger(IPage page, ICDPSession session)
    {
        private int _handled;

        public static async Task<Finger> OnAsync(IPage page)
        {
            await page.EvaluateAsync("() => { window.touched = 0; ['touchstart', 'touchmove', 'touchend'].forEach(n => addEventListener(n, () => touched++)); }");
            return new Finger(page, await page.Context.NewCDPSessionAsync(page));
        }

        public async Task TouchAsync(string type, (double X, double Y)? point)
        {
            await SendAsync(type, point);
            await page.WaitForFunctionAsync("n => window.touched >= n", ++_handled).WaitAsync(TestContext.Current.CancellationToken);
        }

        public async Task DragUpAsync((double X, double Y) start)
        {
            await SendAsync("touchStart", start);
            for (var step = 1; step <= 10; step++)
            {
                await SendAsync("touchMove", (start.X, start.Y - step * 15));
            }

            await SendAsync("touchEnd", null);
        }

        private Task<System.Text.Json.JsonElement?> SendAsync(string type, (double X, double Y)? point) =>
            session.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
            {
                ["type"] = type,
                ["touchPoints"] = point is { } at ? new[] { new Dictionary<string, object> { ["x"] = at.X, ["y"] = at.Y } } : Array.Empty<object>(),
            });
    }

    private static Task<IBrowser> LaunchAsync(IPlaywright playwright) =>
        playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> StartAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Markup)], []);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/focus", "Pages/focus/Page.axaml", _ => new Model("Focus"), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
