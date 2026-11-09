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

public sealed class SplitScriptTests
{
    private const string Group = """
        <style>.g{display:grid;width:420px;height:100px;grid-template-columns:minmax(0,25fr) auto minmax(0,50fr) auto minmax(0,25fr)}.s{width:10px;touch-action:none}</style>
        <div id="g" class="g"><div>a</div>
          <div id="s1" class="s" role="separator" tabindex="0" aria-orientation="vertical" aria-valuenow="25" aria-valuemin="10" aria-valuemax="75" data-aioros-split="c 0 2" data-aioros-tracks="s a s a s" data-aioros-step="5%" data-aioros-collapse="0"></div>
          <div>b</div>
          <div id="s2" class="s" role="separator" tabindex="0" aria-orientation="vertical" aria-valuenow="50" aria-valuemin="0" aria-valuemax="100" data-aioros-split="c 2 4" data-aioros-tracks="s a s a s" data-aioros-step="10px"></div>
          <div>c</div>
        </div>
        """;

    private const string Tracks = "() => getComputedStyle(document.getElementById('g')).gridTemplateColumns.split(' ').filter((_, i) => i % 2 == 0).map(t => Math.round(parseFloat(t))).join() + ':' + document.getElementById('s1').getAttribute('aria-valuenow') + ':' + document.getElementById('s2').getAttribute('aria-valuenow')";

    [Fact]
    [Trait("AC", "LAYOUT-62")]
    public async Task SeparatorsResizeTheirTracksWithThePointerAndTheKeysAsync()
    {
        await using var stage = await Stage.StartAsync(Group);
        var page = stage.Page;
        var box = (await page.Locator("#s1").BoundingBoxAsync())!;
        var (x, y) = (box.X + (box.Width / 2), box.Y + (box.Height / 2));

        var start = await page.EvaluateAsync<string>(Tracks);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 40, y);
        var dragged = await page.EvaluateAsync<string>(Tracks);
        await page.Mouse.MoveAsync(x + 500, y);
        await page.Mouse.UpAsync();
        var far = await page.EvaluateAsync<string>(Tracks);
        await page.FocusAsync("#s1");
        await page.Keyboard.PressAsync("ArrowLeft");
        var left = await page.EvaluateAsync<string>(Tracks);
        await page.Keyboard.PressAsync("ArrowUp");
        var up = await page.EvaluateAsync<string>(Tracks);
        await page.Keyboard.PressAsync("Home");
        var home = await page.EvaluateAsync<string>(Tracks);
        await page.Keyboard.PressAsync("End");
        var end = await page.EvaluateAsync<string>(Tracks);
        await page.Keyboard.PressAsync("Enter");
        var collapsed = await page.EvaluateAsync<string>(Tracks);
        await page.Keyboard.PressAsync("Enter");
        var restored = await page.EvaluateAsync<string>(Tracks);
        await page.Keyboard.PressAsync("F6");
        var next = await page.EvaluateAsync<string>("() => document.activeElement.id");
        await page.Keyboard.PressAsync("F6");
        var wrapped = await page.EvaluateAsync<string>("() => document.activeElement.id");
        await page.Keyboard.PressAsync("Shift+F6");
        var back = await page.EvaluateAsync<string>("() => document.activeElement.id");
        await page.Keyboard.PressAsync("ArrowRight");
        var pixels = await page.EvaluateAsync<string>(Tracks);

        Assert.Equal("100,200,100:25:50", start);
        Assert.Equal("140,160,100:35:40", dragged);
        Assert.Equal("300,0,100:75:0", far);
        Assert.Equal("280,20,100:70:5", left);
        Assert.Equal(left, up);
        Assert.Equal("40,260,100:10:65", home);
        Assert.Equal(far, end);
        Assert.Equal("0,300,100:0:75", collapsed);
        Assert.Equal(far, restored);
        Assert.Equal(("s2", "s1", "s2"), (next, wrapped, back));
        Assert.Equal("300,10,90:75:2.5", pixels);
    }

    [Fact]
    [Trait("AC", "LAYOUT-61")]
    public async Task MovedSeparatorsKeepTheirSizesAcrossAnAnswerAsync()
    {
        await using var stage = await Stage.StartAsync(Group);
        var page = stage.Page;
        await page.FocusAsync("#s1");
        await page.Keyboard.PressAsync("End");
        var moved = await page.EvaluateAsync<string>(Tracks);

        await stage.NextAsync();
        var answered = await page.EvaluateAsync<string>(Tracks);

        Assert.Equal("300,0,100:75:0", moved);
        Assert.Equal(moved, answered);
    }

    [Fact]
    [Trait("AC", "LAYOUT-87")]
    public async Task ADelayedSeparatorPostsItsPositionOnceAfterADragAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <style>.g{display:grid;width:420px;height:100px;grid-template-columns:minmax(0,25fr) auto minmax(0,50fr) auto minmax(0,25fr)}.s{width:10px;touch-action:none}</style>
            <div id="g" class="g"><div>a</div>
              <div id="s1" class="s" role="separator" tabindex="0" aria-orientation="vertical" aria-valuenow="25" aria-valuemin="0" aria-valuemax="100" data-aioros-split="c 0 2" data-aioros-tracks="s a s a s" data-aioros-step="5%"><input type="hidden" name="f7" value="25" data-change data-delay="150"></div>
              <div>b</div>
              <div id="s2" class="s" role="separator" tabindex="0" aria-orientation="vertical" aria-valuenow="50" aria-valuemin="0" aria-valuemax="100" data-aioros-split="c 2 4" data-aioros-tracks="s a s a s" data-aioros-step="5%"><input type="hidden" name="f8" value="50"></div>
              <div>c</div>
            </div>
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
        var box = (await page.Locator("#s1").BoundingBoxAsync())!;
        var (x, y) = (box.X + (box.Width / 2), box.Y + (box.Height / 2));

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 20, y);
        await page.Mouse.MoveAsync(x + 40, y);
        var during = posts.Count;
        await page.Mouse.UpAsync();
        await page.WaitForFunctionAsync("() => document.getElementById('step').dataset.n === '1'", null, new() { PollingInterval = 20 }).WaitAsync(TestContext.Current.CancellationToken);
        var dragged = await page.EvaluateAsync<string>("() => document.getElementsByName('f7')[0].value + ':' + document.getElementById('s1').getAttribute('aria-valuenow')");
        await page.FocusAsync("#s2");
        await page.Keyboard.PressAsync("ArrowRight");
        var keyed = await page.EvaluateAsync<string>("() => document.getElementsByName('f8')[0].value + ':' + document.getElementById('s2').getAttribute('aria-valuenow')");
        var afterKey = posts.Count;
        await stage.NextAsync();

        Assert.Equal(0, during);
        Assert.Single(posts.Take(afterKey));
        Assert.Contains("_chg=7", posts[0], StringComparison.Ordinal);
        Assert.Equal(dragged.Split(':')[0], dragged.Split(':')[1]);
        Assert.NotEqual("25", dragged.Split(':')[0]);
        Assert.Equal(keyed.Split(':')[0], keyed.Split(':')[1]);
        Assert.Contains("f8=" + keyed.Split(':')[0], posts[^1], StringComparison.Ordinal);
    }

    private sealed class Stage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page, Steps steps) : IAsyncDisposable
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
            var app = await HostAsync(site, [new PageDefinition("/split", "Pages/split/Page.axaml", _ => steps, [])]);
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
            var module = page.WaitForResponseAsync(response => response.Url.Contains(SplitScript.Prefix, StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/split");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", SplitScript.Path);
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
        public string Title => "Split";

        public int Index { get; set; }
    }
}
