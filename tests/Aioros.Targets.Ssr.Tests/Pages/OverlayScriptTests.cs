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

public sealed class OverlayScriptTests
{
    private const string Styles = """
        <style>
        .ctx,.hv{display:contents}.hv{anchor-scope:--hv}.hv>:first-child:not(.tip),.hv>.tip>:first-child{anchor-name:--hv}
        [popover].cm{position:fixed;inset:auto;left:var(--aioros-x,0);top:var(--aioros-y,0);margin:0}
        [popover].fly{margin:0;position:fixed;inset:auto}
        .tip{display:contents;anchor-scope:--tip}.tip>:first-child{anchor-name:--tip}
        .tt{position:fixed;inset:auto;margin:0;position-anchor:--tip;position-area:bottom;width:max-content;pointer-events:none;opacity:0;visibility:hidden;transition:opacity .15s ease,visibility 0s linear .15s}
        .tip:hover>.tt,.tip:has(:focus-visible)>.tt{opacity:1;visibility:visible;transition:opacity .15s ease var(--tip-delay,.4s),visibility 0s linear var(--tip-delay,.4s)}
        .tip[data-off]>.tt{opacity:0;visibility:hidden;transition:none}[data-aioros-tip-skip] .tip>.tt{--tip-delay:0s}.d4{--tip-delay:400ms}
        </style>
        """;

    private const string Menu = """
        <button id="t" type="button" popovertarget="m1" aria-haspopup="menu" style="anchor-name:--m1">Open</button>
        <div popover id="m1" role="menu" data-aioros-overlay class="menu" style="position-anchor:--m1;position-area:bottom span-right">
          <button type="button" role="menuitem" tabindex="-1" class="mi" id="a">Apple</button>
          <button type="button" role="menuitem" tabindex="-1" class="mi" id="b" disabled>Banana</button>
          <button type="button" role="menuitemcheckbox" aria-checked="true" tabindex="-1" class="mi" id="c">Cherry</button>
          <button type="button" role="menuitem" tabindex="-1" class="mi" id="d">Date</button>
          <button type="button" role="menuitem" tabindex="-1" class="mi" id="e">Dragonfruit</button>
        </div>
        """;

    private const string Focused = "() => new Promise(r => { const c = new MessageChannel(); c.port1.onmessage = () => r(document.activeElement.id); c.port2.postMessage(0); })";

    [Fact(Timeout = 60_000)]
    [Trait("AC", "OVERLAY-01")]
    public async Task TheOverlayModuleLoadsOnlyWherePagesDeclareItAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var site = new Site();
        site.Engine.PlanFor = page => new RenderPlan([], [new LiteralNode(page.Route == "/menus" ? Menu : "<a id=\"go\" href=\"/menus\">Menus</a><button id=\"next\">Next</button>")], []);
        await using var app = await HostAsync(site, [new PageDefinition("/plain", "Pages/plain/Page.axaml", _ => new Overlaid(), []), new PageDefinition("/menus", "Pages/menus/Page.axaml", _ => new Overlaid(), [])]);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var requested = new List<string>();
        var page = await browser.NewPageAsync();
        page.Request += (_, request) =>
        {
            if (request.Url.Contains(OverlayScript.Prefix, StringComparison.Ordinal))
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
        var loaded = page.WaitForResponseAsync(response => response.Url.Contains(OverlayScript.Prefix, StringComparison.Ordinal));
        await page.ClickAsync("#go");
        var module = await loaded;
        var headers = await module.AllHeadersAsync();
        var direct = await browser.NewPageAsync();
        var directModule = direct.WaitForRequestAsync(request => request.Url.Contains(OverlayScript.Prefix, StringComparison.Ordinal));
        await direct.GotoAsync(Address(app) + "/menus");
        await directModule.WaitAsync(bound);
        using var http = new HttpClient();
        var missing = await http.GetAsync(new Uri(Address(app) + OverlayScript.Prefix + "0000000000000000.js"), bound);

        Assert.Equal(0, plain);
        Assert.Equal([OverlayScript.Path], requested);
        Assert.Equal("text/javascript; charset=utf-8", headers["content-type"]);
        Assert.Equal("public, max-age=31536000, immutable", headers["cache-control"]);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    [Trait("AC", "OVERLAY-02")]
    public async Task MenusMoveTheFocusWithTheKeyboardAsync()
    {
        await using var stage = await Stage.StartAsync(Menu);
        var page = stage.Page;

        await page.ClickAsync("#t");
        var first = await page.EvaluateAsync<string>(Focused);
        await page.Keyboard.PressAsync("ArrowDown");
        var skipped = await page.EvaluateAsync<string>(Focused);
        await page.Keyboard.PressAsync("ArrowUp");
        await page.Keyboard.PressAsync("ArrowUp");
        var top = await page.EvaluateAsync<string>(Focused);
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("ArrowDown");
        var bottom = await page.EvaluateAsync<string>(Focused);
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("d");
        var letter = await page.EvaluateAsync<string>(Focused);
        await page.Clock.RunForAsync(1000);
        await page.Keyboard.PressAsync("d");
        await page.Keyboard.PressAsync("r");
        var letters = await page.EvaluateAsync<string>(Focused);
        await page.Keyboard.PressAsync("Escape");
        var closed = (await page.EvaluateAsync<bool>("() => document.getElementById('m1').matches(':popover-open')"), await page.EvaluateAsync<string>(Focused));
        await page.Keyboard.PressAsync("ArrowDown");
        var reopened = (await page.EvaluateAsync<bool>("() => document.getElementById('m1').matches(':popover-open')"), await page.EvaluateAsync<string>(Focused));

        Assert.Equal("a", first);
        Assert.Equal("c", skipped);
        Assert.Equal("a", top);
        Assert.Equal("e", bottom);
        Assert.Equal("d", letter);
        Assert.Equal("e", letters);
        Assert.Equal((false, "t"), closed);
        Assert.Equal((true, "a"), reopened);
    }

    [Fact]
    [Trait("AC", "OVERLAY-03")]
    public async Task SubMenusOpenWithThePointerAndTheArrowsAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <button id="t" type="button" popovertarget="m1" style="anchor-name:--m1">Open</button>
            <div popover id="m1" role="menu" data-aioros-overlay class="menu" style="position-anchor:--m1;position-area:bottom span-right">
              <button type="button" role="menuitem" tabindex="-1" id="a">Apple</button>
              <button type="button" role="menuitem" aria-haspopup="menu" tabindex="-1" id="s" popovertarget="m2" style="anchor-name:--m2">More</button>
              <div popover id="m2" role="menu" data-aioros-overlay class="menu" style="position-anchor:--m2;position-area:right span-bottom">
                <button type="button" role="menuitem" tabindex="-1" id="x">Inner</button>
              </div>
              <button type="button" role="menuitem" tabindex="-1" id="z">Zed</button>
            </div>
            """);
        var page = stage.Page;
        const string Sub = "() => document.getElementById('m2').matches(':popover-open')";

        await page.ClickAsync("#t");
        await page.HoverAsync("#z");
        var followed = await page.EvaluateAsync<string>(Focused);
        await page.HoverAsync("#s");
        await page.Clock.RunForAsync(99);
        var early = await page.EvaluateAsync<bool>(Sub);
        await page.Clock.RunForAsync(1);
        var hovered = (await page.EvaluateAsync<bool>(Sub), await page.EvaluateAsync<string>(Focused));
        await page.HoverAsync("#a");
        var left = await page.EvaluateAsync<bool>(Sub);
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("ArrowRight");
        var opened = (await page.EvaluateAsync<bool>(Sub), await page.EvaluateAsync<string>(Focused));
        await page.Keyboard.PressAsync("ArrowLeft");
        var back = (await page.EvaluateAsync<bool>(Sub), await page.EvaluateAsync<string>(Focused));

        Assert.Equal("z", followed);
        Assert.False(early);
        Assert.Equal((true, "s"), hovered);
        Assert.False(left);
        Assert.Equal((true, "x"), opened);
        Assert.Equal((false, "s"), back);
    }

    [Fact]
    [Trait("AC", "OVERLAY-04")]
    public async Task MenuBarsMoveBetweenTheirMenusAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <menu role="menubar" data-aioros-overlay style="display:flex;list-style:none">
              <li role="none"><button type="button" role="menuitem" aria-haspopup="menu" id="f" popovertarget="m1" style="anchor-name:--m1">File</button>
                <div popover id="m1" role="menu" data-aioros-overlay class="menu" style="position-anchor:--m1;position-area:bottom span-right"><button type="button" role="menuitem" tabindex="-1" id="f1">New</button></div></li>
              <li role="none"><button type="button" role="menuitem" aria-haspopup="menu" id="e" popovertarget="m2" style="anchor-name:--m2">Edit</button>
                <div popover id="m2" role="menu" data-aioros-overlay class="menu" style="position-anchor:--m2;position-area:bottom span-right"><button type="button" role="menuitem" tabindex="-1" id="e1">Undo</button></div></li>
            </menu>
            """);
        var page = stage.Page;
        const string Open = "() => [...document.querySelectorAll('[popover]:popover-open')].map(x => x.id).join()";

        await page.FocusAsync("#f");
        await page.Keyboard.PressAsync("ArrowRight");
        var next = await page.EvaluateAsync<string>(Focused);
        await page.Keyboard.PressAsync("ArrowRight");
        var wrapped = await page.EvaluateAsync<string>(Focused);
        await page.Keyboard.PressAsync("ArrowDown");
        var down = (await page.EvaluateAsync<string>(Open), await page.EvaluateAsync<string>(Focused));
        await page.Keyboard.PressAsync("ArrowRight");
        var across = (await page.EvaluateAsync<string>(Open), await page.EvaluateAsync<string>(Focused));

        Assert.Equal("e", next);
        Assert.Equal("f", wrapped);
        Assert.Equal(("m1", "f1"), down);
        Assert.Equal(("m2", "e1"), across);
    }

    [Fact]
    [Trait("AC", "OVERLAY-05")]
    public async Task ContextMenusOpenAtThePointerAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <span class="ctx" data-aioros-overlay data-aioros-context="m1"><div id="area" tabindex="0" style="position:absolute;left:100px;top:100px;width:200px;height:100px;background:#eee">Area</div>
              <div popover id="m1" role="menu" data-aioros-overlay class="menu cm" style="width:150px;height:80px;padding:0;border:0"><button type="button" role="menuitem" tabindex="-1" id="c1">Copy</button></div></span>
            <span class="ctx" data-aioros-overlay data-aioros-context="m2"><div id="corner" style="position:fixed;right:0;bottom:0;width:60px;height:60px;background:#ccc">Corner</div>
              <div popover id="m2" role="menu" data-aioros-overlay class="menu cm" style="width:150px;height:80px;padding:0;border:0"><button type="button" role="menuitem" tabindex="-1" id="c2">Paste</button></div></span>
            """);
        var page = stage.Page;
        const string Box = "id => { const r = document.getElementById(id).getBoundingClientRect(); return [r.left, r.top, r.right, r.bottom].map(Math.round).join(); }";

        var prevented = await page.EvaluateAsync<bool>("() => { let p = null; document.addEventListener('contextmenu', e => p = e.defaultPrevented); document.getElementById('area').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 150, clientY: 130 })); return p; }");
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.ClickAsync(160, 140, new() { Button = MouseButton.Right });
        var atPointer = (await page.EvaluateAsync<string>(Box, "m1"), await page.EvaluateAsync<string>(Focused));
        await page.Mouse.ClickAsync(170, 150, new() { Button = MouseButton.Right });
        var inside = await page.EvaluateAsync<string>(Box, "m1");
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.ClickAsync(770, 580, new() { Button = MouseButton.Right });
        var kept = await page.EvaluateAsync<string>(Box, "m2");
        await page.Keyboard.PressAsync("Escape");
        await page.FocusAsync("#area");
        await page.Keyboard.PressAsync("Shift+F10");
        var keyboard = (await page.EvaluateAsync<string>(Box, "m1"), await page.EvaluateAsync<string>(Focused));

        Assert.True(prevented);
        Assert.Equal(("160,140,310,220", "c1"), atPointer);
        Assert.Equal("160,140,310,220", inside);
        Assert.Equal("620,500,770,580", kept);
        Assert.Equal(("100,200,250,280", "c1"), keyboard);
    }

    [Fact]
    [Trait("AC", "OVERLAY-06")]
    public async Task HoverCardsOpenAndCloseAfterTheirDelaysAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <button type="button" id="before">Before</button>
            <span class="hv" data-aioros-overlay data-aioros-hover="m1" data-open="700" data-close="300"><a id="trigger" href="#x" style="display:inline-block;padding:10px">@nextjs</a>
              <div popover="hint" id="m1" role="dialog" class="fly" style="position-anchor:--hv;position-area:bottom;width:200px;height:80px">Card</div></span>
            <button type="button" id="other" style="margin-top:300px">Other</button>
            """);
        var page = stage.Page;
        const string Open = "() => document.getElementById('m1').matches(':popover-open')";

        await page.HoverAsync("#trigger");
        await page.Clock.RunForAsync(699);
        var early = await page.EvaluateAsync<bool>(Open);
        await page.Clock.RunForAsync(1);
        var opened = await page.EvaluateAsync<bool>(Open);
        await page.HoverAsync("#m1");
        await page.Clock.RunForAsync(400);
        var kept = await page.EvaluateAsync<bool>(Open);
        await page.HoverAsync("#other");
        await page.Clock.RunForAsync(299);
        var lingering = await page.EvaluateAsync<bool>(Open);
        await page.Clock.RunForAsync(1);
        var closed = await page.EvaluateAsync<bool>(Open);
        await page.FocusAsync("#before");
        await page.Keyboard.PressAsync("Tab");
        await page.Clock.RunForAsync(700);
        var focused = await page.EvaluateAsync<bool>(Open);
        await page.EvaluateAsync("() => document.getElementById('other').focus()");
        await page.Clock.RunForAsync(300);
        var blurred = await page.EvaluateAsync<bool>(Open);
        await page.FocusAsync("#before");
        await page.Keyboard.PressAsync("Tab");
        await page.Clock.RunForAsync(700);
        await page.Keyboard.PressAsync("Escape");
        var escaped = await page.EvaluateAsync<bool>(Open);

        Assert.False(early);
        Assert.True(opened);
        Assert.True(kept);
        Assert.True(lingering);
        Assert.False(closed);
        Assert.True(focused);
        Assert.False(blurred);
        Assert.False(escaped);
    }

    [Fact]
    [Trait("AC", "OVERLAY-07")]
    public async Task ClickedFlyoutsToggleWithTheirControlAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <span class="hv" data-aioros-overlay data-aioros-click="m1"><div id="box" style="width:100px;height:40px;background:#ccc">Box</div>
              <div popover id="m1" role="dialog" class="fly" style="position-anchor:--hv;position-area:bottom"><button type="button" id="in">In</button></div></span>
            """);
        var page = stage.Page;
        const string Open = "() => document.getElementById('m1').matches(':popover-open')";

        await page.ClickAsync("#box");
        var opened = await page.EvaluateAsync<bool>(Open);
        await page.ClickAsync("#in");
        var kept = await page.EvaluateAsync<bool>(Open);
        await page.ClickAsync("#box");
        var closed = await page.EvaluateAsync<bool>(Open);

        Assert.True(opened);
        Assert.True(kept);
        Assert.False(closed);
    }

    [Fact]
    [Trait("AC", "OVERLAY-08")]
    public async Task ToolTipsHideOnEscapeAndSkipTheirDelayAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <div style="display:flex;gap:40px;padding:40px">
              <span class="tip" data-aioros-overlay data-between="300"><button type="button" id="one">One</button><span role="tooltip" id="t1" class="tt d4">First tip</span></span>
              <span class="tip" data-aioros-overlay data-between="300"><button type="button" id="two">Two</button><span role="tooltip" id="t2" class="tt d4">Second tip</span></span>
              <button type="button" id="three">Three</button>
            </div>
            """);
        var page = stage.Page;
        const string Shown = "id => { const t = document.getElementById(id); t.getAnimations().forEach(a => a.finish()); return getComputedStyle(t).visibility === 'visible'; }";
        const string Delay = "id => getComputedStyle(document.getElementById(id)).transitionDelay";
        const string Skipping = "() => document.documentElement.hasAttribute('data-aioros-tip-skip')";

        await page.HoverAsync("#one");
        var delay = await page.EvaluateAsync<string>(Delay, "t1");
        var shown = await page.EvaluateAsync<bool>(Shown, "t1");
        await page.HoverAsync("#two");
        var skipped = (await page.EvaluateAsync<bool>(Skipping), await page.EvaluateAsync<string>(Delay, "t2"), await page.EvaluateAsync<bool>(Shown, "t2"));
        await page.Keyboard.PressAsync("Escape");
        var escaped = await page.EvaluateAsync<bool>(Shown, "t2");
        await page.HoverAsync("#three");
        await page.Clock.RunForAsync(301);
        await page.HoverAsync("#one");
        var delayed = (await page.EvaluateAsync<bool>(Skipping), await page.EvaluateAsync<string>(Delay, "t1"));
        await page.ClickAsync("#two");
        await page.HoverAsync("#three");
        var clicked = await page.EvaluateAsync<bool>(Shown, "t2");
        await page.FocusAsync("#one");
        await page.Keyboard.PressAsync("Tab");
        var tabbed = await page.EvaluateAsync<bool>(Shown, "t2");

        Assert.Equal("0.4s, 0.4s", delay);
        Assert.True(shown);
        Assert.Equal((true, "0s, 0s", true), skipped);
        Assert.False(escaped);
        Assert.Equal((false, "0.4s, 0.4s"), delayed);
        Assert.False(clicked);
        Assert.True(tabbed);
    }

    [Fact]
    [Trait("AC", "OVERLAY-09")]
    public async Task HoverTriggersOpenTheirPopoversAfterTheirDelaysAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <button type="button" id="start">Start</button>
            <nav style="display:flex;gap:4px;padding:20px">
              <button type="button" id="a" popovertarget="m1" data-aioros-overlay data-hover-open="200" data-hover-close="150" style="anchor-name:--m1;padding:8px 16px"><span id="home">Home</span><b>v</b></button>
              <div popover id="m1" role="dialog" class="fly" style="position-anchor:--m1;position-area:bottom span-right;margin-top:6px;width:200px;height:120px"><button type="button">One</button></div>
              <button type="button" id="b" popovertarget="m2" data-aioros-overlay data-hover-open="200" data-hover-close="150" style="anchor-name:--m2;padding:8px 16px">Docs</button>
              <div popover id="m2" role="dialog" class="fly" style="position-anchor:--m2;position-area:bottom span-right;margin-top:6px;width:200px;height:120px"><button type="button">Two</button></div>
            </nav>
            <button type="button" id="away" style="margin-top:300px">Away</button>
            """);
        var page = stage.Page;
        const string Open = "id => document.getElementById(id).matches(':popover-open')";

        await page.HoverAsync("#a");
        await page.Clock.RunForAsync(199);
        var early = await page.EvaluateAsync<bool>(Open, "m1");
        await page.Clock.RunForAsync(1);
        var opened = await page.EvaluateAsync<bool>(Open, "m1");
        await page.HoverAsync("#m1");
        await page.Clock.RunForAsync(400);
        var kept = await page.EvaluateAsync<bool>(Open, "m1");
        await page.HoverAsync("#b");
        var moved = (await page.EvaluateAsync<bool>(Open, "m2"), await page.EvaluateAsync<bool>(Open, "m1"));
        await page.HoverAsync("#away");
        await page.Clock.RunForAsync(149);
        var lingering = await page.EvaluateAsync<bool>(Open, "m2");
        await page.Clock.RunForAsync(1);
        var closed = await page.EvaluateAsync<bool>(Open, "m2");
        await page.HoverAsync("#a");
        await page.Clock.RunForAsync(200);
        await page.ClickAsync("#a");
        await page.HoverAsync("#home");
        await page.Clock.RunForAsync(500);
        var clicked = await page.EvaluateAsync<bool>(Open, "m1");
        await page.HoverAsync("#away");
        await page.Clock.RunForAsync(500);
        await page.FocusAsync("#start");
        await page.Keyboard.PressAsync("Tab");
        await page.Clock.RunForAsync(500);
        var tabbed = await page.EvaluateAsync<bool>(Open, "m1");

        Assert.False(early);
        Assert.True(opened);
        Assert.True(kept);
        Assert.Equal((true, false), moved);
        Assert.True(lingering);
        Assert.False(closed);
        Assert.False(clicked);
        Assert.False(tabbed);
    }

    [Fact]
    [Trait("AC", "OVERLAY-15")]
    public async Task SwitchingPopoversCarryTheirMotionAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <nav style="display:flex;gap:4px;padding:20px">
              <button type="button" id="a" popovertarget="m1" data-aioros-overlay data-hover-open="200" data-hover-close="150" data-switch style="anchor-name:--m1;padding:8px 16px">Home</button>
              <div popover id="m1" role="dialog" class="fly" style="position-anchor:--m1;position-area:bottom span-right;width:200px;height:120px">One</div>
              <button type="button" id="b" popovertarget="m2" data-aioros-overlay data-hover-open="200" data-hover-close="150" data-switch style="anchor-name:--m2;padding:8px 16px">Docs</button>
              <div popover id="m2" role="dialog" class="fly" style="position-anchor:--m2;position-area:bottom span-right;width:200px;height:120px">Two</div>
              <button type="button" id="c" popovertarget="m3" data-aioros-overlay data-hover-open="200" data-hover-close="150" style="anchor-name:--m3;padding:8px 16px">Plain</button>
              <div popover id="m3" role="dialog" class="fly" style="position-anchor:--m3;position-area:bottom span-right;width:200px;height:120px">Three</div>
            </nav>
            <button type="button" id="away" style="margin-top:300px">Away</button>
            """);
        var page = stage.Page;
        const string Motions = "() => ['m1','m2','m3'].map(id => { const p = document.getElementById(id); return (p.matches(':popover-open') ? 'open ' : 'shut ') + (p.dataset.motion ?? '-') })";

        await page.HoverAsync("#a");
        await page.Clock.RunForAsync(200);
        var first = await page.EvaluateAsync<string[]>(Motions);
        await page.HoverAsync("#b");
        var forward = await page.EvaluateAsync<string[]>(Motions);
        await page.HoverAsync("#a");
        var back = await page.EvaluateAsync<string[]>(Motions);
        await page.HoverAsync("#c");
        var plain = await page.EvaluateAsync<string[]>(Motions);
        await page.HoverAsync("#away");
        await page.Clock.RunForAsync(500);
        await page.EvaluateAsync("() => document.getElementById('a').click()");
        await page.EvaluateAsync("() => document.getElementById('b').click()");
        var clicked = await page.EvaluateAsync<string[]>(Motions);
        await page.ClickAsync("#away");
        await page.Clock.RunForAsync(500);
        await page.HoverAsync("#b");
        await page.Clock.RunForAsync(200);
        var fresh = await page.EvaluateAsync<string[]>(Motions);

        Assert.Equal(["open -", "shut -", "shut -"], first);
        Assert.Equal(["shut to-start", "open from-end", "shut -"], forward);
        Assert.Equal(["open from-start", "shut to-end", "shut -"], back);
        Assert.Equal("open -", plain[2]);
        Assert.Equal(["shut to-start", "open from-end", "shut -"], clicked);
        Assert.Equal("open -", fresh[1]);
    }

    private static Task<IBrowser> LaunchAsync(IPlaywright playwright) =>
        playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });

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

    private sealed class Overlaid : IPageModel
    {
        public string Title => "Overlays";
    }

    private sealed class Stage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page) : IAsyncDisposable
    {
        public IPage Page => page;

        public static async Task<Stage> StartAsync(string markup)
        {
            var site = new Site();
            site.Engine.Plan = new RenderPlan([], [new LiteralNode(Styles + markup)], []);
            var app = await HostAsync(site, [new PageDefinition("/overlays", "Pages/overlays/Page.axaml", _ => new Overlaid(), [])]);
            var playwright = await Playwright.CreateAsync();
            var browser = await LaunchAsync(playwright);
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
            await page.Clock.InstallAsync(new() { TimeDate = DateTime.UnixEpoch });
            await page.Clock.PauseAtAsync(DateTime.UnixEpoch.AddMilliseconds(1024));
            var module = page.WaitForResponseAsync(response => response.Url.Contains(OverlayScript.Prefix, StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/overlays");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", OverlayScript.Path);
            return new Stage(app, playwright, browser, page);
        }

        public async ValueTask DisposeAsync()
        {
            await browser.DisposeAsync();
            playwright.Dispose();
            await app.DisposeAsync();
        }
    }
}
