using System.Collections.Concurrent;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class OfflineFormScriptTests
{
    private const string Sync = "() => document.querySelector('form').getAttribute('data-aioros-sync')";

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-276")]
    public async Task AChangePostedOfflineIsPostedAgainOnlineAsync()
    {
        await using var stage = await Stage.StartAsync("""<input type="text" name="f0" id="t" value="" data-change>""");
        var page = stage.Page;

        await page.Context.SetOfflineAsync(true);
        await page.FillAsync("#t", "A");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForFunctionAsync(Sync + " === 'offline'", null, new() { PollingInterval = 20 });
        await page.FillAsync("#t", "AB");
        var posted = page.WaitForRequestAsync(request => request.Method == "POST" && request.PostData!.Contains("f0=AB", StringComparison.Ordinal));
        await page.Context.SetOfflineAsync(false);
        var post = await posted.WaitAsync(TestContext.Current.CancellationToken);
        await page.WaitForFunctionAsync(Sync + " === null", null, new() { PollingInterval = 20 });

        Assert.Contains("_chg=0", post.PostData, StringComparison.Ordinal);
        Assert.Single(stage.Posts);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-277")]
    public async Task AButtonPostedOfflineIsNotPostedAgainAsync()
    {
        await using var stage = await Stage.StartAsync("""<input type="text" name="f0" id="t" value="" data-change><button type="submit" name="_btn" value="0|" id="go">Go</button>""");
        var page = stage.Page;

        await page.Context.SetOfflineAsync(true);
        await page.ClickAsync("#go");
        await page.WaitForFunctionAsync(Sync + " === 'unsaved'", null, new() { PollingInterval = 20 }).WaitAsync(TestContext.Current.CancellationToken);
        await page.Context.SetOfflineAsync(false);
        await page.FillAsync("#t", "later");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForFunctionAsync(Sync + " === null", null, new() { PollingInterval = 20 });

        Assert.DoesNotContain("_btn", Assert.Single(stage.Posts), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-278")]
    public async Task LeavingWithUnansweredEditsAsksFirstAsync()
    {
        await using var stage = await Stage.StartAsync("""<input type="text" name="f0" id="t" value="kept"><a href="/offline?away" data-nav id="away">Away</a>""");
        var page = stage.Page;
        var bound = TestContext.Current.CancellationToken;
        var dialogs = System.Threading.Channels.Channel.CreateUnbounded<IDialog>();
        page.Dialog += (_, dialog) => dialogs.Writer.TryWrite(dialog);

        await page.ClickAsync("#t");
        await page.Keyboard.TypeAsync("-edited");
        var leaving = page.GotoAsync("about:blank");
        var unload = await dialogs.Reader.ReadAsync(bound);
        await unload.DismissAsync();
        await Assert.ThrowsAsync<PlaywrightException>(() => leaving);
        var clicked = page.ClickAsync("#away");
        var confirm = await dialogs.Reader.ReadAsync(bound);
        await confirm.DismissAsync();
        await clicked;
        var stayed = await page.EvaluateAsync<string>("() => location.search");
        await page.FillAsync("#t", "kept");
        await page.ClickAsync("#away");
        await page.WaitForURLAsync("**/offline?away", new() { WaitUntil = WaitUntilState.Commit });

        Assert.Equal(("beforeunload", "confirm", ""), (unload.Type, confirm.Type, stayed));
        Assert.False(dialogs.Reader.TryRead(out _));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-293")]
    public async Task FieldsWhoseValuesGoNowhereAreNotTheVisitorsEditsAsync()
    {
        await using var stage = await Stage.StartAsync("""<input type="checkbox" name="f0" id="c" data-aioros-local=""><input type="radio" name="f1" value="0" id="r0" data-aioros-local="" checked><input type="radio" name="f1" value="1" id="r1"><input type="text" name="f2" id="t" value="server"><a href="/offline?away" data-nav id="away">Away</a>""");
        var page = stage.Page;
        var bound = TestContext.Current.CancellationToken;
        var dialogs = System.Threading.Channels.Channel.CreateUnbounded<IDialog>();
        page.Dialog += (_, dialog) => dialogs.Writer.TryWrite(dialog);
        const string Drafted = "() => new Promise(r => { const o = indexedDB.open('aioros-drafts'); o.onsuccess = () => { const d = o.result; if (!d.objectStoreNames.contains('pages')) return r(null); const g = d.transaction('pages').objectStore('pages').get(location.pathname + location.search); g.onsuccess = () => r(g.result ? JSON.stringify(g.result) : null) } })";

        await page.CheckAsync("#c");
        await page.CheckAsync("#r1");
        await page.ClickAsync("#away");
        await page.WaitForURLAsync("**/offline?away", new() { WaitUntil = WaitUntilState.Commit });
        var asked = dialogs.Reader.TryRead(out _);
        await page.GoBackAsync();
        await page.WaitForSelectorAsync("#t");
        await page.CheckAsync("#c");
        await page.FillAsync("#t", "edited");
        await page.WaitForFunctionAsync("() => document.documentElement.getAttribute('data-aioros-draft') === 'saved'", null, new() { PollingInterval = 50 });
        var draft = await page.EvaluateAsync<string>(Drafted);
        var leaving = page.GotoAsync("about:blank");
        var unload = await dialogs.Reader.ReadAsync(bound);
        await unload.DismissAsync();
        await Assert.ThrowsAsync<PlaywrightException>(() => leaving);

        Assert.False(asked);
        Assert.Contains("edited", draft, StringComparison.Ordinal);
        Assert.DoesNotContain("\"f0\"", draft, StringComparison.Ordinal);
        Assert.DoesNotContain("\"f1\"", draft, StringComparison.Ordinal);
        Assert.Equal("beforeunload", unload.Type);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-302")]
    public async Task APostOnItsWayIsNotAnUnsavedChangeAsync()
    {
        await using var stage = await Stage.StartAsync("""<button type="submit" name="_btn" value="0|" id="go">Inbox</button><a href="/offline?away" data-nav id="away">Away</a>""");
        var page = stage.Page;
        var gate = new TaskCompletionSource();
        stage.Engine.Command = async _ => await gate.Task;
        var asked = 0;
        page.Dialog += (_, _) => asked++;

        var pending = page.WaitForRequestAsync(static request => request.Method == "POST");
        await page.ClickAsync("#go");
        await pending.WaitAsync(TestContext.Current.CancellationToken);
        await page.ClickAsync("#away");
        await page.WaitForURLAsync("**/offline?away", new() { WaitUntil = WaitUntilState.Commit });
        gate.SetResult();

        Assert.Equal(0, asked);
    }

    [Fact]
    [Trait("AC", "SSR-303")]
    public async Task LinksToTheirOwnPageFragmentAreTheBrowsersAsync()
    {
        await using var stage = await Stage.StartAsync("""<a href="#" id="top">Top</a><a href="#end" id="down">Down</a><div style="height:3000px"></div><p id="end">End</p>""");
        var page = stage.Page;
        var fetched = 0;
        var urls = new List<string>();
        page.Request += (_, request) => { if (request.ResourceType is "fetch" or "document") { fetched++; urls.Add(request.Url); } };
        await page.EvaluateAsync("() => { window.marker = 1; }");

        await page.ClickAsync("#down");
        await page.WaitForFunctionAsync("() => location.hash === '#end' && scrollY > 1000", null, new() { PollingInterval = 20 });
        await page.ClickAsync("#top");
        await page.WaitForFunctionAsync("() => location.hash === '' || location.hash === '#'", null, new() { PollingInterval = 20 });

        Assert.True(fetched == 0, string.Join(" ", urls));
        Assert.Equal(1, await page.EvaluateAsync<int?>("() => window.marker"));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-279")]
    public async Task EditsKeptInTheBrowserAreOfferedBackAsync()
    {
        await using var stage = await Stage.StartAsync("""<input type="text" name="f0" id="t" value="server"><button type="submit" name="_btn" value="0|" id="go">Save</button>""");
        var page = stage.Page;
        const string Drafted = "() => new Promise(r => { const o = indexedDB.open('aioros-drafts'); o.onsuccess = () => { const d = o.result; if (!d.objectStoreNames.contains('pages')) return r(null); const g = d.transaction('pages').objectStore('pages').get(location.pathname + location.search); g.onsuccess = () => r(g.result ? JSON.stringify(g.result) : null) } })";

        var dialogs = System.Threading.Channels.Channel.CreateUnbounded<IDialog>();
        page.Dialog += (_, dialog) => dialogs.Writer.TryWrite(dialog);
        var accepting = AcceptAsync(dialogs.Reader);
        await page.FillAsync("#t", "draft");
        await page.WaitForFunctionAsync("() => document.documentElement.getAttribute('data-aioros-draft') === 'saved'", null, new() { PollingInterval = 50 });
        var kept = await page.EvaluateAsync<string>(Drafted);
        await page.GotoAsync(page.Url);
        await page.WaitForSelectorAsync(".aioros-draft[role=status]");
        var before = await page.InputValueAsync("#t");
        await page.ClickAsync(".aioros-draft button[data-restore]");
        var restored = await page.InputValueAsync("#t");
        var posted = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#go");
        await posted.WaitAsync(TestContext.Current.CancellationToken);
        await page.WaitForFunctionAsync("() => document.documentElement.getAttribute('data-aioros-draft') === 'none'", null, new() { PollingInterval = 50 });
        var removed = await page.EvaluateAsync<string?>(Drafted);
        await page.GotoAsync(page.Url);
        var offered = await page.Locator(".aioros-draft").CountAsync();

        dialogs.Writer.TryComplete();
        await accepting;

        Assert.Contains("draft", kept, StringComparison.Ordinal);
        Assert.Null(removed);
        Assert.Equal(("server", "draft"), (before, restored));
        Assert.Equal(0, offered);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-280")]
    public async Task AStalePageLoadsAnewKeepingItsEditsAsync()
    {
        await using var stage = await Stage.StartAsync("""<input type="hidden" name="_s0" value="lost"><input type="text" name="f0" id="t" value="server"><button type="submit" name="_btn" value="0|" id="go">Save</button>""", seal: true);
        var page = stage.Page;
        var dialogs = System.Threading.Channels.Channel.CreateUnbounded<IDialog>();
        page.Dialog += (_, dialog) => dialogs.Writer.TryWrite(dialog);

        await page.FillAsync("#t", "draft");
        await page.WaitForFunctionAsync("() => document.documentElement.getAttribute('data-aioros-draft') === 'saved'", null, new() { PollingInterval = 50 });
        var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        var loaded = page.WaitForRequestAsync(request => request.Method == "GET" && request.IsNavigationRequest);
        await page.ClickAsync("#go");
        var answer = await answered.WaitAsync(TestContext.Current.CancellationToken);
        await loaded.WaitAsync(TestContext.Current.CancellationToken);
        await page.WaitForSelectorAsync(".aioros-draft[role=status]");
        var shown = await page.InputValueAsync("#t");
        await page.ClickAsync(".aioros-draft button[data-restore]");

        Assert.Equal((409, false, "server", "draft"), (answer.Status, dialogs.Reader.TryRead(out _), shown, await page.InputValueAsync("#t")));
        Assert.Null(stage.Engine.Posted);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-315")]
    public async Task ASentDraftIsNotOfferedBackOnTheSamePageAsync()
    {
        await using var stage = await Stage.StartAsync("""<input type="text" name="f0" id="t" value=""><button type="submit" name="_btn" value="0|" id="go">Send</button>""");
        var page = stage.Page;

        await page.FillAsync("#t", "Hola");
        await page.WaitForFunctionAsync("() => document.documentElement.getAttribute('data-aioros-draft') === 'saved'", null, new() { PollingInterval = 50 });
        var posted = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#go");
        await posted.WaitAsync(TestContext.Current.CancellationToken);
        await page.WaitForFunctionAsync("() => document.documentElement.getAttribute('data-aioros-draft') === 'none'", null, new() { PollingInterval = 50 });
        var shown = await page.Locator(".aioros-draft").CountAsync();
        await page.GotoAsync(page.Url);
        await page.WaitForFunctionAsync("() => !!window.aioros", null, new() { PollingInterval = 20 });
        var offered = await page.Locator(".aioros-draft").CountAsync();

        Assert.Equal((0, 0), (shown, offered));
    }

    private static async Task AcceptAsync(System.Threading.Channels.ChannelReader<IDialog> dialogs)
    {
        await foreach (var dialog in dialogs.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            await dialog.AcceptAsync();
        }
    }

    [Fact]
    [Trait("AC", "SSR-430")]
    public async Task APasswordIsNeverKeptInADraftAsync()
    {
        await using var stage = await Stage.StartAsync("""<input type="password" name="f0" id="p"><input type="text" name="f1" id="t">""");
        var page = stage.Page;
        const string Drafted = "() => new Promise(r => { const o = indexedDB.open('aioros-drafts'); o.onsuccess = () => { const q = o.result.transaction('pages').objectStore('pages').get(location.pathname + location.search); q.onsuccess = () => r(JSON.stringify(q.result)); }; })";

        await page.FillAsync("#p", "hunter2");
        await page.FillAsync("#t", "kept");
        await page.WaitForFunctionAsync("() => document.documentElement.getAttribute('data-aioros-draft') === 'saved'", null, new() { PollingInterval = 50 });
        var kept = await page.EvaluateAsync<string>(Drafted);

        Assert.Contains("kept", kept, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", kept, StringComparison.Ordinal);
    }

    private sealed class Stage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page, ConcurrentQueue<string> posts, Site site) : IAsyncDisposable
    {
        public IPage Page => page;

        public FakeEngine Engine => site.Engine;

        public ConcurrentQueue<string> Posts => posts;

        public static async Task<Stage> StartAsync(string markup, bool seal = false)
        {
            var posts = new ConcurrentQueue<string>();
            var site = new Site();
            site.Engine.Plan = new RenderPlan([], [new LiteralNode(markup)], []);
            var app = await HostAsync(site, posts, [new PageDefinition("/offline", "Pages/offline/Page.axaml", _ => new Model(), [])], seal);
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
            await page.GotoAsync(Address(app) + "/offline");
            await page.WaitForFunctionAsync("() => !!window.aioros", null, new() { PollingInterval = 20 });
            return new Stage(app, playwright, browser, page, posts, site);
        }

        private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        private static async Task<WebApplication> HostAsync(Site site, ConcurrentQueue<string> posts, PageDefinition[] pages, bool seal)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
            if (seal)
            {
                DataProtectionStateSeal.AddStateSeal(builder.Services);
            }

            var app = builder.Build();
            app.Use(async (http, next) =>
            {
                if (HttpMethods.IsPost(http.Request.Method))
                {
                    http.Request.EnableBuffering();
                    using var reader = new StreamReader(http.Request.Body, leaveOpen: true);
                    posts.Enqueue(await reader.ReadToEndAsync());
                    http.Request.Body.Position = 0;
                }

                await next(http);
            });
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
