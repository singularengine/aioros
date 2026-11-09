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

public sealed class CarouselScriptTests
{
    private const string Carousels = """
        <style>.st{display:flex;overflow-x:auto;scroll-snap-type:x mandatory;width:200px;scrollbar-width:none}.cs{flex:0 0 100%;scroll-snap-align:start;height:50px}</style>
        <div id="a" data-aioros-carousel role="region" aria-roledescription="carousel">
          <div id="as" data-aioros-strip class="st"><div class="cs sel"><input type="hidden" name="f0" value="0">A1</div><div class="cs">A2</div><div class="cs">A3</div></div>
          <button type="button" id="ap" data-aioros-slide="-1" disabled>P</button><button type="button" id="an" data-aioros-slide="1">N</button>
          <div><button type="button" id="d0" data-aioros-slide-to aria-current="true" class="on">1</button><button type="button" id="d1" data-aioros-slide-to class="off">2</button><button type="button" id="d2" data-aioros-slide-to class="off">3</button></div>
        </div>
        <div id="b" data-aioros-carousel data-aioros-wrap role="region" aria-roledescription="carousel">
          <div data-aioros-strip class="st"><div class="cs sel">B1</div><div class="cs">B2</div></div>
          <button type="button" id="bp" data-aioros-slide="-1">P</button><button type="button" id="bn" data-aioros-slide="1">N</button>
        </div>
        """;

    private const string State = "id => { const c = document.getElementById(id), s = c.querySelector('[data-aioros-strip]'), l = [...s.children]; "
        + "return l.findIndex(x => x.classList.contains('sel')) + ':' + Math.round(s.scrollLeft) + ':' + [...c.querySelectorAll('[data-aioros-slide]')].map(b => b.disabled ? 'x' : 'o').join('') "
        + "+ ':' + (c.querySelector('input[type=hidden]')?.parentElement === l.find(x => x.classList.contains('sel')) ? c.querySelector('input[type=hidden]').value : '-') "
        + "+ ':' + [...c.querySelectorAll('[data-aioros-slide-to]')].map(d => d.className + (d.hasAttribute('aria-current') ? ' ' + d.getAttribute('aria-current') : '')).join(); }";

    [Fact]
    [Trait("AC", "ITEMS-106")]
    public async Task CarouselsMoveWithTheirButtonsIndicatorsKeysAndScrollAsync()
    {
        await using var stage = await Stage.StartAsync(Carousels);
        var page = stage.Page;

        var start = await page.EvaluateAsync<string>(State, "a");
        await page.ClickAsync("#an");
        var next = await page.EvaluateAsync<string>(State, "a");
        await page.ClickAsync("#an");
        var last = await page.EvaluateAsync<string>(State, "a");
        await page.ClickAsync("#d0");
        var first = await page.EvaluateAsync<string>(State, "a");
        await page.FocusAsync("#an");
        await page.Keyboard.PressAsync("ArrowRight");
        var right = await page.EvaluateAsync<string>(State, "a");
        await page.Keyboard.PressAsync("ArrowLeft");
        var left = await page.EvaluateAsync<string>(State, "a");
        await page.ClickAsync("#bp");
        var wrapped = await page.EvaluateAsync<string>(State, "b");
        await page.ClickAsync("#bn");
        var around = await page.EvaluateAsync<string>(State, "b");
        await page.EvaluateAsync("() => document.getElementById('as').scrollTo({ left: 400, behavior: 'instant' })");
        await page.WaitForFunctionAsync("() => document.querySelector('#as > .sel')?.textContent === 'A3'", null, new() { PollingInterval = 20 }).WaitAsync(TestContext.Current.CancellationToken);
        var scrolled = await page.EvaluateAsync<string>(State, "a");
        await page.EvaluateAsync("() => document.getElementById('a').setAttribute('data-change', '')");
        var posted = page.WaitForRequestAsync(request => request.Method == "POST");
        await page.ClickAsync("#ap");
        var post = await posted;

        Assert.Equal("0:0:xo:0:on true,off,off", start);
        Assert.Equal("1:200:oo:1:off,on true,off", next);
        Assert.Equal("2:400:ox:2:off,off,on true", last);
        Assert.Equal("0:0:xo:0:on true,off,off", first);
        Assert.Equal("1:200:oo:1:off,on true,off", right);
        Assert.Equal(first, left);
        Assert.Equal("1:200:oo:-:", wrapped);
        Assert.Equal("0:0:oo:-:", around);
        Assert.Equal("2:400:ox:2:off,off,on true", scrolled);
        Assert.Contains("f0=1", post.PostData, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-146")]
    public async Task TheMouseDragsASwipeableCarouselAsEmblaDoesAsync()
    {
        const string Strip = """
            <style>.st{display:flex;overflow-x:auto;scroll-snap-type:x mandatory;width:200px;scrollbar-width:none}.cs{flex:0 0 100%;scroll-snap-align:start;height:80px}</style>
            <div id="c" data-aioros-carousel data-aioros-drag role="region" aria-roledescription="carousel"><div id="s" data-aioros-strip class="st">
            <div class="cs sel"><button type="button" id="hit" onclick="window.hits=(window.hits||0)+1">One</button></div><div class="cs">Two</div><div class="cs">Three</div><div class="cs">Four</div></div></div>
            <div id="p" data-aioros-carousel role="region" aria-roledescription="carousel"><div id="ps" data-aioros-strip class="st"><div class="cs sel">P1</div><div class="cs">P2</div></div></div>
            """;
        await using var stage = await GestureStage.StartAsync(_ => Strip, CarouselScript.Prefix, clock: true);
        var page = stage.Page;
        var strip = (await page.Locator("#s").BoundingBoxAsync())!;
        var (x, y) = (strip.X + 150, strip.Y + 60);
        async Task<string> SettledAsync(int index)
        {
            await page.WaitForFunctionAsync($"() => document.querySelectorAll('#s > div')[{index}].classList.contains('sel') && Math.abs(document.getElementById('s').scrollLeft - {index * 200}) < 1", null, new() { PollingInterval = 20 });
            return await page.EvaluateAsync<string>("() => [...document.querySelectorAll('#s > div')].findIndex(x => x.classList.contains('sel')) + ':' + Math.round(document.getElementById('s').scrollLeft)");
        }

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x - 80, y, new() { Steps = 4 });
        var dragged = await page.EvaluateAsync<int>("() => Math.round(document.getElementById('s').scrollLeft)");
        await page.Clock.RunForAsync(200);
        await page.Mouse.UpAsync();
        var held = await SettledAsync(0);
        await page.Clock.RunForAsync(20);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x - 92, y, new() { Steps = 4 });
        await page.Clock.RunForAsync(171);
        await page.Mouse.MoveAsync(x - 92.5f, y);
        await page.Clock.RunForAsync(100);
        await page.Mouse.MoveAsync(x - 95.5f, y);
        await page.Mouse.UpAsync();
        var slow = await SettledAsync(0);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Clock.RunForAsync(50);
        await page.Mouse.MoveAsync(x - 60, y, new() { Steps = 4 });
        await page.Mouse.UpAsync();
        var flicked = await SettledAsync(1);
        await page.Clock.RunForAsync(20);
        await page.Mouse.MoveAsync(x - 100, y);
        await page.Mouse.DownAsync();
        await page.Clock.RunForAsync(50);
        await page.Mouse.MoveAsync(x + 20, y, new() { Steps = 4 });
        await page.Mouse.UpAsync();
        var backward = await SettledAsync(0);
        await page.Clock.RunForAsync(20);
        var button = (await page.Locator("#hit").BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(button.X + 5, button.Y + 5);
        await page.Mouse.DownAsync();
        await page.Clock.RunForAsync(200);
        await page.Mouse.MoveAsync(button.X + 25, button.Y + 5, new() { Steps = 4 });
        await page.Clock.RunForAsync(200);
        await page.Mouse.UpAsync();
        await SettledAsync(0);
        await page.Clock.RunForAsync(20);
        var dragHits = await page.EvaluateAsync<int>("() => window.hits || 0");
        await page.ClickAsync("#hit");
        var clickHits = await page.EvaluateAsync<int>("() => window.hits || 0");
        var plain = (await page.Locator("#ps").BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(plain.X + 150, plain.Y + 40);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(plain.X + 50, plain.Y + 40, new() { Steps = 4 });
        await page.Mouse.UpAsync();
        var still = await page.EvaluateAsync<int>("() => Math.round(document.getElementById('ps').scrollLeft)");

        Assert.Equal(80, dragged);
        Assert.Equal("0:0", held);
        Assert.Equal("0:0", slow);
        Assert.Equal("1:200", flicked);
        Assert.Equal("0:0", backward);
        Assert.Equal(0, dragHits);
        Assert.Equal(1, clickHits);
        Assert.Equal(0, still);
    }

    private sealed class Paged : IPageModel
    {
        public string Title => "Carousel";
    }

    private sealed class Stage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page) : IAsyncDisposable
    {
        public IPage Page => page;

        public static async Task<Stage> StartAsync(string markup)
        {
            var site = new Site();
            site.Engine.Plan = new RenderPlan([], [new LiteralNode(markup)], []);
            var app = await HostAsync(site, [new PageDefinition("/carousel", "Pages/carousel/Page.axaml", _ => new Paged(), [])]);
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 }, ReducedMotion = ReducedMotion.Reduce });
            var module = page.WaitForResponseAsync(response => response.Url.Contains(CarouselScript.Prefix, StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/carousel");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", CarouselScript.Path);
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
}
