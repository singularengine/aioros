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

public sealed class KeyboardScriptTests
{
    private const string Tabs = """
        <div data-aioros-tabs id="h"><div role="tablist">
          <button type="button" role="tab" aria-selected="true" tabindex="0" id="a"><input type="hidden" name="f0" value="0">A</button>
          <button type="button" role="tab" aria-selected="false" tabindex="-1" id="b">B</button>
          <button type="button" role="tab" aria-selected="false" tabindex="-1" id="c" disabled>C</button>
          <button type="button" role="tab" aria-selected="false" tabindex="-1" id="d">D</button>
        </div><div role="tabpanel">pa</div><div role="tabpanel" hidden>pb</div><div role="tabpanel" hidden>pc</div><div role="tabpanel" hidden>pd</div></div>
        <div data-aioros-tabs><div role="tablist" aria-orientation="vertical">
          <button type="button" role="tab" aria-selected="true" tabindex="0" id="x">X</button>
          <button type="button" role="tab" aria-selected="false" tabindex="-1" id="y">Y</button>
        </div><div role="tabpanel">px</div><div role="tabpanel" hidden>py</div></div>
        """;

    private const string Shortcuts = """
        <input id="field">
        <button id="k" type="button" popovertarget="p" data-aioros-key="ctrl+KeyK">Search</button><div popover id="p">Find</div>
        <button id="s" type="submit" name="_btn" value="0|" data-aioros-key="ctrl+shift+KeyS">Save</button>
        <button id="slash" type="button" data-aioros-key="Slash">Help</button>
        """;

    private const string Scoped = """
        <button id="outside" type="button">Out</button>
        <button id="dk" type="button" hidden data-aioros-key="ctrl+KeyK"></button>
        <button id="dj" type="button" hidden data-aioros-key="ctrl+KeyJ"></button>
        <button id="sk" type="button" hidden data-aioros-scope data-aioros-key="ctrl+KeyK"></button>
        <button id="sd" type="button" hidden data-aioros-scope data-aioros-key="Delete"></button>
        <div id="box"><input id="typed" value="abc"><button id="inbox" type="button">In</button>
          <button id="sj" type="button" hidden disabled data-aioros-scope data-aioros-key="ctrl+KeyJ"></button>
          <div id="inner"><button id="deep" type="button">Deep</button></div>
        </div>
        """;

    private const string Calendar = """
        <div data-aioros-calendar><input type="hidden" name="f0" value="2025-06-12|Month||"><table role="grid"><tbody>
          <tr hidden><td><button type="submit" name="_btn" value="0|" tabindex="-1"></button></td><td><button type="submit" name="_btn" value="1|" tabindex="-1"></button></td><td><button type="submit" name="_btn" value="2|" tabindex="-1"></button></td></tr>
          <tr><td><div><button type="submit" name="_btn" value="3|" tabindex="-1" aria-hidden="true" id="a1">31</button></div></td><td><div><button type="submit" name="_btn" value="4|" tabindex="-1" id="a2">1</button></div></td><td><div><button type="submit" name="_btn" value="5|" tabindex="0" id="a3">2</button></div></td></tr>
          <tr><td><div><button type="submit" name="_btn" value="6|" tabindex="-1" id="b1">3</button></div></td><td><div><button type="submit" name="_btn" value="7|" tabindex="-1" id="b2">4</button></div></td><td><div><button type="submit" name="_btn" value="8|" tabindex="-1" id="b3">5</button></div></td></tr>
        </tbody></table></div>
        """;

    private const string Selected = "() => [...document.querySelectorAll('#h [role=tab]')].filter(t => t.getAttribute('aria-selected') === 'true').map(t => t.id + (t.tabIndex === 0 ? '0' : '')).join() + ':' + [...document.querySelectorAll('#h [role=tabpanel]')].filter(p => !p.hidden).map(p => p.textContent).join() + ':' + document.querySelector('#h input').value";

    [Fact]
    [Trait("AC", "FOCUS-07")]
    public async Task TabsSwitchWithTheClickAndTheArrowsAsync()
    {
        await using var stage = await Stage.StartAsync(Tabs);
        var page = stage.Page;
        var named = await page.EvaluateAsync<string>("() => { const a = document.getElementById('a'), p = document.getElementById(a.getAttribute('aria-controls')); return p.textContent + ':' + (p.getAttribute('aria-labelledby') === a.id) }");

        await page.ClickAsync("#b");
        var clicked = await page.EvaluateAsync<string>(Selected);
        await page.FocusAsync("#b");
        await page.Keyboard.PressAsync("ArrowRight");
        var skipped = await page.EvaluateAsync<string>(Selected);
        await page.Keyboard.PressAsync("ArrowRight");
        var wrapped = await page.EvaluateAsync<string>(Selected);
        await page.Keyboard.PressAsync("ArrowLeft");
        var back = await page.EvaluateAsync<string>(Selected);
        await page.Keyboard.PressAsync("Home");
        var home = await page.EvaluateAsync<string>(Selected);
        await page.Keyboard.PressAsync("End");
        var end = await page.EvaluateAsync<string>(Selected);
        await page.FocusAsync("#x");
        await page.Keyboard.PressAsync("ArrowRight");
        var across = await page.EvaluateAsync<string>("() => document.activeElement.id");
        await page.Keyboard.PressAsync("ArrowDown");
        var down = await page.EvaluateAsync<string>("() => document.activeElement.id + document.getElementById('y').getAttribute('aria-selected')");
        await page.EvaluateAsync("() => document.querySelector('#h [role=tablist]').setAttribute('data-change', '')");
        var posted = page.WaitForRequestAsync(request => request.Method == "POST");
        await page.ClickAsync("#b");
        var post = await posted;

        Assert.Equal("pa:true", named);
        Assert.Contains("_chg=0", post.PostData, StringComparison.Ordinal);
        Assert.Contains("f0=1", post.PostData, StringComparison.Ordinal);
        Assert.Equal("b0:pb:1", clicked);
        Assert.Equal("d0:pd:3", skipped);
        Assert.Equal("a0:pa:0", wrapped);
        Assert.Equal("d0:pd:3", back);
        Assert.Equal("a0:pa:0", home);
        Assert.Equal("d0:pd:3", end);
        Assert.Equal("x", across);
        Assert.Equal("ytrue", down);
    }

    [Fact]
    [Trait("AC", "FOCUS-08")]
    public async Task ShortcutsClickTheirButtonsAsync()
    {
        await using var stage = await Stage.StartAsync(Shortcuts);
        var page = stage.Page;
        await page.EvaluateAsync("() => { window.clicks = 0; document.getElementById('slash').addEventListener('click', () => window.clicks++); window.left = []; addEventListener('keydown', e => window.left.push(e.code + e.defaultPrevented)); }");
        const string Open = "() => document.getElementById('p').matches(':popover-open')";

        await page.Keyboard.PressAsync("Control+KeyK");
        var opened = await page.EvaluateAsync<bool>(Open);
        await page.Keyboard.PressAsync("Escape");
        await page.Keyboard.PressAsync("Meta+KeyK");
        var meta = await page.EvaluateAsync<bool>(Open);
        await page.Keyboard.PressAsync("Escape");
        var posted = page.WaitForRequestAsync(request => request.Method == "POST");
        await page.Keyboard.PressAsync("Control+Shift+KeyS");
        var post = await posted;
        await page.FocusAsync("#field");
        await page.Keyboard.PressAsync("Slash");
        var typed = (await page.EvaluateAsync<int>("() => window.clicks"), await page.InputValueAsync("#field"));
        await page.Keyboard.PressAsync("Control+KeyK");
        var fromField = await page.EvaluateAsync<bool>(Open);
        await page.Keyboard.PressAsync("Escape");
        await page.EvaluateAsync("() => document.activeElement.blur()");
        await page.Keyboard.PressAsync("Slash");
        var clicks = await page.EvaluateAsync<int>("() => window.clicks");
        await page.Keyboard.PressAsync("Control+KeyJ");
        var left = await page.EvaluateAsync<string[]>("() => window.left");

        Assert.True(opened);
        Assert.True(meta);
        Assert.Contains("_btn=0", post.PostData, StringComparison.Ordinal);
        Assert.Equal((0, "/"), typed);
        Assert.True(fromField);
        Assert.Equal(1, clicks);
        Assert.Equal("KeyJfalse", left[^1]);
        Assert.Contains("KeyKtrue", left);
    }

    [Fact]
    [Trait("AC", "FOCUS-09")]
    public async Task ScopedShortcutsTakeTheirScopesKeysFirstAsync()
    {
        await using var stage = await Stage.StartAsync(Scoped);
        var page = stage.Page;
        await page.EvaluateAsync("() => { window.hits = []; document.querySelectorAll('button[hidden]').forEach(b => b.addEventListener('click', () => window.hits.push(b.id))); }");
        const string Hits = "() => window.hits.splice(0).join()";

        await page.FocusAsync("#typed");
        await page.Keyboard.PressAsync("Control+KeyK");
        var inside = await page.EvaluateAsync<string>(Hits);
        await page.FocusAsync("#outside");
        await page.Keyboard.PressAsync("Control+KeyK");
        var outside = await page.EvaluateAsync<string>(Hits);
        await page.EvaluateAsync("() => document.activeElement.blur()");
        await page.Keyboard.PressAsync("Control+KeyK");
        var nothing = await page.EvaluateAsync<string>(Hits);
        await page.FocusAsync("#deep");
        await page.Keyboard.PressAsync("Control+KeyJ");
        var fallen = await page.EvaluateAsync<string>(Hits);
        await page.FocusAsync("#typed");
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Delete");
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Keyboard.PressAsync("Delete");
        var typing = (await page.EvaluateAsync<string>(Hits), await page.InputValueAsync("#typed"));
        await page.FocusAsync("#inbox");
        await page.Keyboard.PressAsync("Delete");
        var onButton = await page.EvaluateAsync<string>(Hits);

        Assert.Equal("sk", inside);
        Assert.Equal("dk", outside);
        Assert.Equal("dk", nothing);
        Assert.Equal("dj", fallen);
        Assert.Equal((string.Empty, "ab"), typing);
        Assert.Equal("sd", onButton);
    }

    [Fact]
    [Trait("AC", "SSR-212")]
    public async Task CalendarArrowsMoveTheFocusOverTheShownDaysAsync()
    {
        await using var stage = await Stage.StartAsync(Calendar);
        var page = stage.Page;
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" ? 1 : 0;
        var moves = new List<string>();
        async Task PressAsync(string key)
        {
            await page.Keyboard.PressAsync(key);
            moves.Add(await page.EvaluateAsync<string>("() => document.activeElement.id + ':' + [...document.querySelectorAll('[data-aioros-calendar] button')].filter(b => b.tabIndex === 0).map(b => b.id).join()"));
        }

        await page.FocusAsync("#a3");
        foreach (var key in new[] { "ArrowRight", "ArrowLeft", "ArrowLeft", "ArrowLeft", "ArrowUp", "ArrowDown", "End", "Home", "ArrowUp", "Home" })
        {
            await PressAsync(key);
        }

        await page.FocusAsync("#a3");
        await PressAsync("Home");

        Assert.Equal(["b1:b1", "a3:a3", "a2:a2", "a2:a2", "a2:a2", "b2:b2", "b3:b3", "b1:b1", "b1:b1", "b1:b1", "a2:a2"], moves);
        Assert.Equal(0, requests);
    }

    [Fact]
    [Trait("AC", "SSR-213")]
    public async Task ControlClicksOnCalendarDaysArePostedAsync()
    {
        await using var stage = await Stage.StartAsync(Calendar);
        var page = stage.Page;

        var held = page.WaitForRequestAsync(request => request.Method == "POST");
        await page.ClickAsync("#b2", new() { Modifiers = [KeyboardModifier.Control] });
        var withControl = Uri.UnescapeDataString((await held).PostData ?? string.Empty);
        await page.EvaluateAsync("() => { const h = document.querySelector('[data-aioros-calendar] input'); h.value = '2025-06-12|Month||'; }");
        var plain = page.WaitForRequestAsync(request => request.Method == "POST");
        await page.ClickAsync("#b3");
        var withoutModifiers = Uri.UnescapeDataString((await plain).PostData ?? string.Empty);

        Assert.Contains("f0=2025-06-12|Month||c", withControl, StringComparison.Ordinal);
        Assert.Contains("_btn=7|", withControl, StringComparison.Ordinal);
        Assert.Contains("f0=2025-06-12|Month||&", withoutModifiers + "&", StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-226")]
    public async Task EnterInADatePickersFieldPostsItsChangeAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <button type="submit" name="_btn" value="0|" id="first">First</button>
            <div data-aioros-picker><input name="f0" data-change value="June 01, 2025" id="date"><div data-aioros-calendar></div></div>
            """);
        var page = stage.Page;

        var posts = new List<string>();
        page.Request += (_, request) =>
        {
            if (request.Method == "POST")
            {
                posts.Add(Uri.UnescapeDataString(request.PostData ?? string.Empty));
            }
        };

        await page.FillAsync("#date", "June 20, 2025");
        var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.PressAsync("#date", "Enter");
        await answered;
        await page.EvaluateAsync("() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done)))");

        var post = Assert.Single(posts);
        Assert.Contains("_chg=0", post, StringComparison.Ordinal);
        Assert.Contains("f0=June+20,+2025", post, StringComparison.Ordinal);
        Assert.DoesNotContain("_btn=", post, StringComparison.Ordinal);
    }

    private sealed class Keyed : IPageModel
    {
        public string Title => "Keys";
    }

    private sealed class Stage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page) : IAsyncDisposable
    {
        public IPage Page => page;

        public static async Task<Stage> StartAsync(string markup)
        {
            var site = new Site();
            site.Engine.Plan = new RenderPlan([], [new LiteralNode(markup)], []);
            var app = await HostAsync(site, [new PageDefinition("/keys", "Pages/keys/Page.axaml", _ => new Keyed(), [])]);
            var playwright = await Playwright.CreateAsync();
            var browser = await LaunchAsync(playwright);
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
            var module = page.WaitForResponseAsync(response => response.Url.Contains(KeyboardScript.Prefix, StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/keys");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", KeyboardScript.Path);
            return new Stage(app, playwright, browser, page);
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

        public async ValueTask DisposeAsync()
        {
            await browser.DisposeAsync();
            playwright.Dispose();
            await app.DisposeAsync();
        }
    }
}
