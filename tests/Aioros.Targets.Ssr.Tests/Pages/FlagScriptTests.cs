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

public sealed class FlagScriptTests
{
    private const string Panels = """
        <style>
        :where([data-aioros-flag="IsOpen"][data-aioros-on]) .fw.fw{width:240px}:where([data-aioros-flag="IsOpen"]:not([data-aioros-on])) .fw.fw{width:48px}
        .p{display:grid;grid-template-columns:auto minmax(0,1fr)}.w{height:20px}
        </style>
        <div id="a" class="p" data-aioros-flag="IsOpen" data-aioros-on><div id="aw" class="w fw"></div><button type="button" id="ab" data-aioros-flips="IsOpen" aria-expanded="true">A</button></div>
        <div id="b" class="p" data-aioros-flag="IsOpen"><div id="bw" class="w fw"></div><button type="button" id="bb" data-aioros-flips="IsOpen" aria-expanded="false">B</button></div>
        """;

    private const string State = "() => ['a', 'b'].map(id => Math.round(document.getElementById(id + 'w').getBoundingClientRect().width) + ':' + document.getElementById(id + 'b').getAttribute('aria-expanded')).join()";

    [Fact]
    [Trait("AC", "SSR-211")]
    public async Task FlagsSwitchInTheBrowserAndSurviveAnAnswerAsync()
    {
        await using var stage = await Stage.StartAsync(Panels);
        var page = stage.Page;
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" || request.ResourceType == "document" ? 1 : 0;

        var start = await page.EvaluateAsync<string>(State);
        await page.ClickAsync("#ab");
        var off = await page.EvaluateAsync<string>(State);
        var sent = requests;
        await page.ClickAsync("#ab");
        var on = await page.EvaluateAsync<string>(State);
        await page.ClickAsync("#ab");
        await stage.NextAsync();
        var answered = await page.EvaluateAsync<string>(State);

        Assert.Equal("240:true,48:false", start);
        Assert.Equal("48:false,48:false", off);
        Assert.Equal(0, sent);
        Assert.Equal("240:true,48:false", on);
        Assert.Equal("48:false,48:false", answered);
    }

    [Fact]
    [Trait("AC", "SSR-253")]
    public async Task ExpandersFollowTheirFlagAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <div id="p" data-aioros-flag="IsOpen"><details id="d" data-aioros-follows="IsOpen" data-aioros-open-on="true"><summary id="s">Output</summary><div>10.33.4</div></details><button type="button" id="b" data-aioros-flips="IsOpen" aria-expanded="false">Show</button></div>
            """);
        var page = stage.Page;
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" || request.ResourceType == "document" ? 1 : 0;
        const string Followed = "() => document.getElementById('d').open + ':' + document.getElementById('p').hasAttribute('data-aioros-on') + ':' + document.getElementById('b').getAttribute('aria-expanded')";

        await page.ClickAsync("#b");
        await page.WaitForFunctionAsync("() => document.getElementById('d').open", null, new() { PollingInterval = 20 });
        var opened = await page.EvaluateAsync<string>(Followed);
        await page.ClickAsync("#s");
        await page.WaitForFunctionAsync("() => !document.getElementById('p').hasAttribute('data-aioros-on')", null, new() { PollingInterval = 20 });
        var closed = await page.EvaluateAsync<string>(Followed);

        Assert.Equal(("true:true:true", "false:false:false", 0), (opened, closed, requests));
    }

    [Fact]
    [Trait("AC", "SSR-227")]
    public async Task BoundFlagsArePostedWhenSwitchedAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <div id="p" data-aioros-flag="IsOpen" data-aioros-on data-change><input type="checkbox" hidden name="f0" checked id="c"><button type="button" id="b" data-aioros-flips="IsOpen" aria-expanded="true">B</button></div>
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

        var closed = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#b");
        await closed;
        var opened = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#b");
        await opened;

        Assert.Equal(2, posts.Count);
        Assert.Contains("_chg=0", posts[0], StringComparison.Ordinal);
        Assert.DoesNotContain("f0=on", posts[0], StringComparison.Ordinal);
        Assert.Contains("f0=on", posts[1], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-228")]
    public async Task FlagsKeepTheirStateInTheirCookieAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <div id="p" data-aioros-flag="IsOpen" data-aioros-on data-aioros-cookie="sidebar_state"><button type="button" id="b" data-aioros-flips="IsOpen" aria-expanded="true">B</button></div>
            <div id="q" data-aioros-flag="IsOpen" data-aioros-on><button type="button" id="c" data-aioros-flips="IsOpen" aria-expanded="true">C</button></div>
            """);
        var page = stage.Page;

        await page.ClickAsync("#c");
        await page.WaitForFunctionAsync("() => document.getElementById('c').getAttribute('aria-expanded') === 'false'", null, new() { PollingInterval = 20 });
        var none = await page.Context.CookiesAsync();
        await page.ClickAsync("#b");
        await page.WaitForFunctionAsync("() => document.cookie.includes('sidebar_state=false')", null, new() { PollingInterval = 20 });
        var closed = (await page.Context.CookiesAsync()).Single(cookie => cookie.Name == "sidebar_state");
        await page.ClickAsync("#b");
        await page.WaitForFunctionAsync("() => document.cookie.includes('sidebar_state=true')", null, new() { PollingInterval = 20 });
        var opened = (await page.Context.CookiesAsync()).Single(cookie => cookie.Name == "sidebar_state");
        var days = (DateTimeOffset.FromUnixTimeSeconds((long)opened.Expires) - DateTimeOffset.UtcNow).TotalDays;

        Assert.DoesNotContain(none, cookie => cookie.Name == "sidebar_state");
        Assert.Equal(("false", "/"), (closed.Value, closed.Path));
        Assert.Equal("true", opened.Value);
        Assert.InRange(days, 6.9, 7.01);
    }

    [Fact]
    [Trait("AC", "SSR-271")]
    public async Task AFlagsCookieIsSecureInASecureContextAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <div id="p" data-aioros-flag="IsOpen" data-aioros-on data-aioros-cookie="sidebar_state"><button type="button" id="b" data-aioros-flips="IsOpen" aria-expanded="true">B</button></div>
            """);
        var page = stage.Page;

        var secure = await page.EvaluateAsync<bool>("() => isSecureContext");
        await page.ClickAsync("#b");
        var cookie = (await page.Context.CookiesAsync()).Single(cookie => cookie.Name == "sidebar_state");

        Assert.True(secure);
        Assert.Equal((true, SameSiteAttribute.Lax), (cookie.Secure, cookie.SameSite));
    }

    [Fact]
    [Trait("AC", "SSR-229")]
    public async Task FlagsOpenTheirSheetBelowTheirBreakpointAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <style>@media (min-width:600px){dialog.sh600{display:contents}}@media (max-width:599.98px){dialog.sh600{position:fixed;inset:0;width:100%;height:100%;max-width:none;max-height:none;margin:0;padding:0;border:0}dialog.sh600:not([open]){display:none}}</style>
            <div id="p" data-aioros-flag="IsOpen" data-aioros-on data-aioros-breakpoint="600" data-aioros-cookie="sheet_state" data-change><input type="checkbox" hidden name="f0" checked>
              <dialog data-aioros-sheet class="sh600"><nav><div id="mask" data-aioros-flips="IsOpen" style="height:40px">Mask</div><a id="link" href="#side">Side</a></nav></dialog>
              <button type="button" id="t" data-aioros-flips="IsOpen" aria-expanded="true">T</button>
            </div>
            """);
        var page = stage.Page;
        var posts = 0;
        page.Request += (_, request) => posts += request.Method == "POST" ? 1 : 0;
        const string Sheet = "() => { const s = document.querySelector('dialog'); return [s.open && s.matches(':modal'), document.getElementById('p').hasAttribute('data-aioros-on'), document.getElementById('t').getAttribute('aria-expanded'), document.activeElement && s.contains(document.activeElement)].join() }";

        await page.SetViewportSizeAsync(390, 700);
        await page.ClickAsync("#t");
        var opened = await page.EvaluateAsync<string>(Sheet);
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForFunctionAsync("() => document.getElementById('t').getAttribute('aria-expanded') === 'false'", null, new() { PollingInterval = 20 });
        var escaped = await page.EvaluateAsync<string>(Sheet);
        await page.ClickAsync("#t");
        await page.ClickAsync("#mask");
        await page.WaitForFunctionAsync("() => document.getElementById('t').getAttribute('aria-expanded') === 'false'", null, new() { PollingInterval = 20 });
        var masked = await page.EvaluateAsync<string>(Sheet);
        var narrowCookies = await page.Context.CookiesAsync();
        var narrowPosts = posts;
        await page.ClickAsync("#t");
        await page.SetViewportSizeAsync(1000, 700);
        await page.WaitForFunctionAsync("() => !document.querySelector('dialog').open && document.getElementById('t').getAttribute('aria-expanded') === 'true'", null, new() { PollingInterval = 20 });
        var widened = await page.EvaluateAsync<string>(Sheet);
        var posted = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#t");
        await posted;
        var wide = await page.EvaluateAsync<string>(Sheet);

        Assert.Equal("true,true,true,true", opened);
        Assert.Equal("false,true,false,false", escaped);
        Assert.Equal("false,true,false,false", masked);
        Assert.Equal((false, 0), (narrowCookies.Any(cookie => cookie.Name == "sheet_state"), narrowPosts));
        Assert.Equal("false,true,true,false", widened);
        Assert.Equal("false,false,false,false", wide);
        Assert.Contains(await page.Context.CookiesAsync(), cookie => cookie.Name == "sheet_state" && cookie.Value == "false");
    }

    [Fact]
    [Trait("AC", "SSR-256")]
    public async Task AnAnswerChangingAFlagsValueSwitchesItAsync()
    {
        static ConditionNode While(bool on, string text) => new(model => ((Steps)model!).Index is 0 or 2 or 3 == on, [new LiteralNode(text)]);
        await using var stage = await Stage.StartAsync(
            new LiteralNode("<div id=\"p\" data-aioros-flag=\"IsOpen\""), While(true, " data-aioros-on"),
            new LiteralNode(" data-aioros-cookie=\"sidebar_state\"><button type=\"button\" id=\"b\" data-aioros-flips=\"IsOpen\" aria-expanded=\""),
            While(true, "true"), While(false, "false"), new LiteralNode("\">B</button></div>"));
        var page = stage.Page;
        async Task<string> StateAsync() =>
            await page.EvaluateAsync<string>("() => document.getElementById('p').hasAttribute('data-aioros-on') + ':' + document.getElementById('b').getAttribute('aria-expanded')")
            + ":" + (await page.Context.CookiesAsync()).SingleOrDefault(cookie => cookie.Name == "sidebar_state")?.Value;

        await page.ClickAsync("#b");
        await stage.NextAsync();
        var off = await StateAsync();
        await stage.NextAsync();
        var opened = await StateAsync();
        await page.ClickAsync("#b");
        await stage.NextAsync();
        var kept = await StateAsync();

        Assert.Equal(("false:false:false", "true:true:true", "false:false:false"), (off, opened, kept));
    }

    private sealed class Stage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page, Steps steps) : IAsyncDisposable
    {
        public IPage Page => page;

        public static Task<Stage> StartAsync(string markup) => StartAsync(new LiteralNode(markup));

        public static async Task<Stage> StartAsync(params IPlanNode[] markup)
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
                new LiteralNode("\"></i>"), .. markup,
            ], []);
            var app = await HostAsync(site, [new PageDefinition("/flags", "Pages/flags/Page.axaml", _ => steps, [])]);
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
            var module = page.WaitForResponseAsync(response => response.Url.Contains(FlagScript.Prefix, StringComparison.Ordinal));
            var plan = page.WaitForResponseAsync(response => response.Url.Contains("/_aioros/plan/", StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/flags");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await plan.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", FlagScript.Path);
            return new Stage(app, playwright, browser, page, steps);
        }

        public async Task NextAsync()
        {
            var next = steps.Index + 1;
            await page.ClickAsync("#next");
            await page.WaitForFunctionAsync("n => document.getElementById('step').dataset.n === String(n)", next, new() { PollingInterval = 20 }).WaitAsync(TestContext.Current.CancellationToken);
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
        public string Title => "Flags";

        public int Index { get; set; }
    }
}
