using System.Globalization;
using System.Text.RegularExpressions;
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

public sealed partial class MorphScriptTests
{
    private static readonly string[] Easings = ["linear", "ease-in", "ease-out", "ease-in-out"];

    private const string Settings = "data-aioros-morph-duration=\"1600\" data-aioros-morph-easing=\"linear\"";

    [Fact]
    [Trait("AC", "MORPH-01")]
    public async Task APathMovesFromItsPreviousPointsByProportionalIndexAsync()
    {
        await using var stage = await Stage.StartAsync(
            Svg($"<g {Settings} data-aioros-morph-map=\"ratio\"><path id=\"l\" data-aioros-morph-path=\"line monotone-x\" data-aioros-morph=\"0 100 50 50 100 80\" d=\"M0,100L50,50L100,80\"/></g>"),
            Svg($"<g {Settings} data-aioros-morph-map=\"ratio\"><path id=\"l\" data-aioros-morph-path=\"line monotone-x\" data-aioros-morph=\"0 90 25 10 50 60 75 20 100 40\" d=\"M0,90L100,40\"/></g>"));

        await stage.NextAsync();
        var start = await stage.AttributeAsync("#l", "d");
        var marked = await stage.AttributeAsync("#l", "data-aioros-morphing");
        await stage.Page.Clock.RunForAsync(800);
        var half = await stage.AttributeAsync("#l", "d");
        await stage.Page.Clock.RunForAsync(816);
        var end = await stage.AttributeAsync("#l", "d");
        var ended = await stage.AttributeAsync("#l", "data-aioros-morphing");

        AssertPath("M0,100C16.666666666666668,75,33.33333333333333,50,50,50C66.66666666666667,50,83.33333333333333,65,100,80", start);
        AssertPath("M0,95C4.166666666666667,75,8.333333333333332,55,12.5,55C25,55,37.5,55,50,55C54.166666666666664,55,58.333333333333336,35,62.5,35C75,35,87.5,47.5,100,60", half);
        Assert.Equal(string.Empty, marked);
        Assert.Equal("M0,90L100,40", end);
        Assert.Null(ended);
    }

    [Fact]
    [Trait("AC", "MORPH-02")]
    public async Task NewNumbersStartFromTheirDeclaredOriginAsync()
    {
        await using var stage = await Stage.StartAsync(
            Svg($"<style>.enter{{animation:fade 5s}}@keyframes fade{{from{{opacity:0}}}}</style><g id=\"g\" {Settings}><path id=\"l\" data-aioros-morph-path=\"line\" data-aioros-morph=\"\" d=\"\"/><circle id=\"swap\"/></g>"),
            Svg($"<style>.enter{{animation:fade 5s}}@keyframes fade{{from{{opacity:0}}}}</style><g id=\"g\" {Settings}><path id=\"l\" data-aioros-morph-path=\"line\" data-aioros-morph=\"0 0 100 50\" data-aioros-morph-from=\"250 150\" d=\"M0,0L100,50\"/>"
                + "<path id=\"swap\" data-aioros-morph-path=\"rect\" data-aioros-morph=\"0 0 10 10\" data-aioros-morph-from=\"0 10 10 0\" d=\"M0,0h10v10h-10Z\"/>"
                + "<path id=\"bar\" data-aioros-morph-path=\"rect\" data-aioros-morph=\"10 20 30 40\" data-aioros-morph-from=\"_ 60 _ 0\" d=\"M10,20h30v40h-30Z\"/>"
                + "<g id=\"wrap\" class=\"enter\"><path class=\"enter\" data-aioros-morph-path=\"rect\" data-aioros-morph=\"1 1 2 2\" data-aioros-morph-from=\"_ _ 0 0\" d=\"M1,1h2v2h-2Z\"/></g><g id=\"other\" class=\"enter\"></g>"
                + "<path id=\"plain\" data-aioros-morph-path=\"rect\" data-aioros-morph=\"1 2 3 4\" d=\"M1,2h3v4h-3Z\"/></g>"));

        await stage.NextAsync();
        var line = await stage.AttributeAsync("#l", "d");
        var bar = await stage.AttributeAsync("#bar", "d");
        var swap = await stage.AttributeAsync("#swap", "d");
        var plain = (await stage.AttributeAsync("#plain", "d"), await stage.AttributeAsync("#plain", "data-aioros-morphing"));
        var entrances = await stage.Page.EvaluateAsync<string[]>("() => ['#wrap', '#wrap path', '#other'].map(s => document.querySelector(s).getAnimations().map(a => a.playState).join())");
        await stage.Page.Clock.RunForAsync(800);
        var halfBar = await stage.AttributeAsync("#bar", "d");
        var halfLine = await stage.AttributeAsync("#l", "d");

        AssertPath("M250,150L250,150", line);
        AssertPath("M10,60h30v0h-30Z", bar);
        AssertPath("M0,10h10v0h-10Z", swap);
        Assert.Equal<(string?, string?)>(("M1,2h3v4h-3Z", null), plain);
        AssertPath("M10,40h30v20h-30Z", halfBar);
        AssertPath("M125,75L175,100", halfLine);
        Assert.Equal(["", "", "running"], entrances);
    }

    [Fact]
    [Trait("AC", "MORPH-08")]
    public async Task ShapesAppendedWithTheirSeriesPlayTheirEnterAnimationAsync()
    {
        const string Style = "<style>.enter{animation:fade 5s}@keyframes fade{from{opacity:0}}</style>";
        const string Kept = "<g id=\"kept\" data-aioros-morph-path=\"rect\" data-aioros-morph-duration=\"1600\"><path data-aioros-morph=\"0 0 1 1\" d=\"M0,0h1v1h-1Z\"/>";
        await using var stage = await Stage.StartAsync(
            Svg($"{Style}{Kept}</g><g id=\"slot\"></g>"),
            Svg($"{Style}{Kept}<path id=\"item\" class=\"enter\" data-aioros-morph=\"10 20 30 40\" data-aioros-morph-from=\"_ 60 _ 0\" d=\"M10,20h30v40h-30Z\"/></g>"
                + $"<g id=\"slot\"><g id=\"series\" {Settings} data-aioros-morph-path=\"line\"><path id=\"line\" class=\"enter\" data-aioros-morph=\"0 0 100 50\" data-aioros-morph-from=\"250 150\" d=\"M0,0L100,50\"/>"
                + "<path id=\"bar\" class=\"enter\" data-aioros-morph-path=\"rect\" data-aioros-morph=\"10 20 30 40\" data-aioros-morph-from=\"_ 60 _ 0\" d=\"M10,20h30v40h-30Z\"/></g></g>"));

        await stage.NextAsync();
        var shown = await stage.Page.EvaluateAsync<string[]>("() => ['#line', '#bar', '#item'].map(s => { const e = document.querySelector(s); return [e.getAttribute('d'), e.hasAttribute('data-aioros-morphing'), e.getAnimations().map(a => a.playState).join()].join('|'); })");

        Assert.Equal("M0,0L100,50|false|running", shown[0]);
        Assert.Equal("M10,20h30v40h-30Z|false|running", shown[1]);
        Assert.Equal("true|", string.Join('|', shown[2].Split('|')[1..]));
        AssertPath("M10,60h30v0h-30Z", shown[2].Split('|')[0]);
    }

    [Fact]
    [Trait("AC", "MORPH-03")]
    public async Task AttributesMoveByIndexAfterTheirDelayAndEasingAsync()
    {
        static string Group(string easing, string body) => $"<g data-aioros-morph-path=\"cx cy\" data-aioros-morph-duration=\"1600\" data-aioros-morph-delay=\"160\" data-aioros-morph-easing=\"{easing}\">{body}</g>";
        static string Named(string at) => string.Concat(Easings.Select(name => Group(name, $"<circle id=\"{name}\" r=\"3\" cx=\"{at}\" cy=\"0\" data-aioros-morph=\"{at} 0\"/>")));
        await using var stage = await Stage.StartAsync(
            Svg(Group("cubic-bezier(0.25, 0.1, 0.25, 1)", "<circle id=\"a\" r=\"3\" cx=\"0\" cy=\"0\" data-aioros-morph=\"0 0\"/><circle id=\"b\" r=\"3\" cx=\"100\" cy=\"100\" data-aioros-morph=\"100 100\"/>") + Named("0")),
            Svg(Group("cubic-bezier(0.25, 0.1, 0.25, 1)", "<circle id=\"a\" r=\"3\" cx=\"100\" cy=\"50\" data-aioros-morph=\"100 50\"/><circle id=\"b\" r=\"3\" cx=\"0\" cy=\"200\" data-aioros-morph=\"0 200\"/><circle id=\"c\" r=\"3\" cx=\"7\" cy=\"9\" data-aioros-morph=\"7 9\"/>") + Named("100")));

        await stage.NextAsync();
        await stage.Page.Clock.RunForAsync(160);
        var delayed = await CentersAsync(stage, "#a", "#b", "#linear");
        await stage.Page.Clock.RunForAsync(800);
        var half = await CentersAsync(stage, "#a", "#b", "#c");
        var named = await CentersAsync(stage, "#linear", "#ease-in", "#ease-out", "#ease-in-out");

        Assert.Equal([0, 0, 100, 100, 0, 0], delayed);
        Assert.Equal(80.24033875848569, half[0], 6);
        Assert.Equal(40.12016937924284, half[1], 6);
        Assert.Equal(19.75966124151431, half[2], 6);
        Assert.Equal(180.24033875848569, half[3], 6);
        Assert.Equal([7, 9], half[4..]);
        Assert.Equal(50, named[0], 6);
        Assert.Equal(31.535681257253945, named[2], 6);
        Assert.Equal(68.46431874274606, named[4], 6);
        Assert.Equal(50, named[6], 6);
    }

    [Fact]
    [Trait("AC", "MORPH-04")]
    public async Task SectorsPolygonsBandsAndGlyphsAreRebuiltHalfWayAsync()
    {
        await using var stage = await Stage.StartAsync(
            Svg($"<g {Settings}><path id=\"s1\" data-aioros-morph-path=\"sector\" data-aioros-morph=\"200 150 0 80 0 90\" d=\"\"/><path id=\"s2\" data-aioros-morph-path=\"sector\" data-aioros-morph=\"100 100 30 60 0 300\" d=\"\"/>"
                + "<path id=\"p\" data-aioros-morph-path=\"polygon\" data-aioros-morph=\"0 0 100 0 50 100\" d=\"\"/><path id=\"b\" data-aioros-morph-path=\"band monotone-y\" data-aioros-morph=\"0 0 20 0 10 50 25 50 0 100 30 100\" d=\"\"/>"
                + "<path id=\"y\" data-aioros-morph-path=\"glyph\" data-aioros-morph=\"50 50 100\" d=\"M0,0h1v1h-1Z\"/></g>"),
            Svg($"<g {Settings}><path id=\"s1\" data-aioros-morph-path=\"sector\" data-aioros-morph=\"200 150 0 80 0 300\" d=\"\"/><path id=\"s2\" data-aioros-morph-path=\"sector\" data-aioros-morph=\"100 100 30 60 0 420\" d=\"\"/>"
                + "<path id=\"p\" data-aioros-morph-path=\"polygon\" data-aioros-morph=\"20 20 120 40 50 140\" d=\"\"/><path id=\"b\" data-aioros-morph-path=\"band monotone-y\" data-aioros-morph=\"20 20 60 20 50 70 65 70 40 120 70 120\" d=\"\"/>"
                + "<path id=\"y\" data-aioros-morph-path=\"glyph\" data-aioros-morph=\"70 90 400\" d=\"M0,0h1v1h-1Z\"/></g>"));

        await stage.NextAsync();
        await stage.Page.Clock.RunForAsync(800);
        var sector = await stage.AttributeAsync("#s1", "d");
        var ring = await stage.AttributeAsync("#s2", "d");
        var polygon = await stage.AttributeAsync("#p", "d");
        var band = await stage.AttributeAsync("#b", "d");
        var glyph = await stage.AttributeAsync("#y", "transform");
        await stage.Page.Clock.RunForAsync(816);
        var rest = await stage.AttributeAsync("#y", "transform");

        AssertPath("M 280,150 A 80,80,0, 1,0, 122.72593389687454,170.70552360820167 L 200,150 Z", sector);
        AssertPath("M 160,100 A 60,60,0, 1,0, 159.99999999086148,100.00104719755115 L 129.99999999543073,100.00052359877557 A 30,30,0, 1,1, 130,100 Z", ring);
        AssertPath("M10,10L110,20L50,120Z", polygon);
        AssertPath("M10,10C20,26.666666666666668,30,43.33333333333333,30,60C30,76.66666666666667,25,93.33333333333333,20,110L50,110C48.333333333333336,93.33333333333333,46.666666666666664,76.66666666666667,45,60C43.333333333333336,43.33333333333333,41.666666666666664,26.666666666666668,40,10Z", band);
        var s = Math.Sqrt(250.0 / 400);
        AssertPath(string.Create(CultureInfo.InvariantCulture, $"matrix({s},0,0,{s},{60 - (s * 70)},{70 - (s * 90)})"), glyph);
        Assert.Null(rest);
    }

    [Fact]
    [Trait("AC", "MORPH-05")]
    public async Task AMorphRestartsFromWhereItIsAndHonoursReducedMotionAsync()
    {
        string Line(string values, string key = "k") => Svg($"<g {Settings} data-aioros-morph-path=\"line\"><path id=\"l\" data-aioros-morph=\"{values}\" data-aioros-morph-key=\"{key}\" d=\"M{values.Replace(' ', ',')}\"/></g>");
        var steps = new[] { Line("0 0 100 0"), Line("0 100 100 100"), Line("0 200 100 0"), Line("0 200 100 0"), Line("0 200 100 0"), Line("0 200 100 0", "other") };
        await using var stage = await Stage.StartAsync(steps);

        await stage.NextAsync();
        await stage.Page.Clock.RunForAsync(800);
        await stage.NextAsync();
        var restarted = await stage.AttributeAsync("#l", "d");
        await stage.Page.Clock.RunForAsync(400);
        var moving = await stage.AttributeAsync("#l", "d");
        await stage.NextAsync();
        var kept = await stage.AttributeAsync("#l", "data-aioros-morphing");
        await stage.Page.Clock.RunForAsync(400);
        var carried = await stage.AttributeAsync("#l", "d");
        await stage.Page.Clock.RunForAsync(1600);
        await stage.NextAsync();
        var unchanged = (await stage.AttributeAsync("#l", "d"), await stage.AttributeAsync("#l", "data-aioros-morphing"));
        await stage.NextAsync();
        var keyed = await stage.AttributeAsync("#l", "data-aioros-morphing");

        await using var reduced = await Stage.StartAsync(ReducedMotion.Reduce, Line("0 0 100 0"), Line("0 100 100 100"));
        await reduced.NextAsync();
        var jumped = (await reduced.AttributeAsync("#l", "d"), await reduced.AttributeAsync("#l", "data-aioros-morphing"));

        AssertPath("M0,50L100,50", restarted);
        AssertPath("M0,87.5L100,37.5", moving);
        AssertPath("M0,125L100,25", carried);
        Assert.Equal(string.Empty, kept);
        Assert.Equal<(string?, string?)>(("M0,200,100,0", null), unchanged);
        Assert.Equal(string.Empty, keyed);
        Assert.Equal<(string?, string?)>(("M0,100,100,100", null), jumped);
    }

    [Fact]
    [Trait("AC", "MORPH-06")]
    public async Task TheMorphModuleLoadsOnlyWherePagesDeclareItAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        const string chart = "<svg><path data-aioros-morph-path=\"line\" data-aioros-morph=\"0 0 1 1\" d=\"M0,0L1,1\"/></svg>";
        var site = new Site();
        site.Engine.PlanFor = page => new RenderPlan([], [new LiteralNode(page.Route == "/chart" ? chart : "<a id=\"go\" href=\"/chart\">Chart</a><button id=\"next\">Next</button>")], []);
        await using var app = await HostAsync(site, [new PageDefinition("/plain", "Pages/plain/Page.axaml", _ => new Model("Plain"), []), new PageDefinition("/chart", "Pages/chart/Page.axaml", _ => new Model("Chart"), [])]);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var requested = new List<string>();
        var page = await browser.NewPageAsync();
        page.Request += (_, request) =>
        {
            if (request.Url.Contains(MorphScript.Prefix, StringComparison.Ordinal))
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
        var loaded = page.WaitForResponseAsync(response => response.Url.Contains(MorphScript.Prefix, StringComparison.Ordinal));
        await page.ClickAsync("#go");
        var module = await loaded;
        var headers = await module.AllHeadersAsync();
        var direct = await browser.NewPageAsync();
        var directModule = direct.WaitForRequestAsync(request => request.Url.Contains(MorphScript.Prefix, StringComparison.Ordinal));
        var hit = await direct.GotoAsync(Address(app) + "/chart");
        await directModule.WaitAsync(bound);
        using var http = new HttpClient();
        var missing = await http.GetAsync(new Uri(Address(app) + MorphScript.Prefix + "0000000000000000.js"), bound);

        Assert.Equal(0, plain);
        Assert.Equal([MorphScript.Path], requested);
        Assert.Equal("text/javascript; charset=utf-8", headers["content-type"]);
        Assert.Equal("public, max-age=31536000, immutable", headers["cache-control"]);
        Assert.True(hit!.Ok);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "MORPH-07")]
    public async Task AppendedMorphsRecalculateStylesOnceAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var rects = string.Concat(Enumerable.Range(0, 200).Select(index => $"<rect class=\"enter\" data-aioros-morph-path=\"rect\" data-aioros-morph=\"{index} 10 1 30\" data-aioros-morph-from=\"_ 40 _ 0\" data-aioros-morph-duration=\"400\" d=\"M{index},10h1v30h-1Z\"/>"));
        var chart = "<style>@keyframes grow{from{opacity:0}to{opacity:1}}.enter{animation:grow 10s}</style>" + Svg(rects);
        var site = new Site();
        site.Engine.PlanFor = page => new RenderPlan([], [new LiteralNode(page.Route == "/chart" ? chart : "<a id=\"go\" href=\"/chart\">Chart</a><svg><path data-aioros-morph-path=\"line\" data-aioros-morph=\"0 0 1 1\" d=\"M0,0L1,1\"/></svg>")], []);
        await using var app = await HostAsync(site, [new PageDefinition("/plain", "Pages/plain/Page.axaml", _ => new Model("Plain"), []), new PageDefinition("/chart", "Pages/chart/Page.axaml", _ => new Model("Chart"), [])]);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        await page.Clock.InstallAsync(new() { TimeDate = DateTime.UnixEpoch });
        await page.Clock.PauseAtAsync(DateTime.UnixEpoch.AddMilliseconds(1024));
        var module = page.WaitForResponseAsync(response => response.Url.Contains(MorphScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/plain");
        await module.WaitAsync(bound);
        await page.EvaluateAsync("u => import(u).then(() => 0)", MorphScript.Path);
        var cdp = await page.Context.NewCDPSessionAsync(page);
        await cdp.SendAsync("Performance.enable");
        var before = await RecalcsAsync(cdp);

        await page.ClickAsync("#go");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('rect.enter').length === 200", null, new() { PollingInterval = 20 }).WaitAsync(bound);
        await page.ScreenshotAsync();
        var recalcs = await RecalcsAsync(cdp) - before;
        var shown = await page.EvaluateAsync<string>("() => { const r = [...document.querySelectorAll('rect.enter')]; return [r.every(e => e.hasAttribute('data-aioros-morphing')), r.every(e => e.getAnimations().length === 0)].join(','); }");

        Assert.Equal("true,true", shown);
        Assert.InRange(recalcs, 1, 19);
    }

    private static async Task<double> RecalcsAsync(ICDPSession cdp)
    {
        var metrics = (await cdp.SendAsync("Performance.getMetrics"))!.Value.GetProperty("metrics");
        return metrics.EnumerateArray().First(metric => metric.GetProperty("name").GetString() == "RecalcStyleCount").GetProperty("value").GetDouble();
    }

    private static string Svg(string body) => "<svg width=\"300\" height=\"200\">" + body + "</svg>";

    private static async Task<double[]> CentersAsync(Stage stage, params string[] selectors) =>
        await stage.Page.EvaluateAsync<double[]>("s => s.flatMap(x => { const e = document.querySelector(x); return [+e.getAttribute('cx'), +e.getAttribute('cy')]; })", selectors);

    private static void AssertPath(string expected, string? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(Letters(expected), Letters(actual));
        var (a, b) = (Numbers(expected), Numbers(actual));
        Assert.Equal(a.Length, b.Length);
        for (var i = 0; i < a.Length; i++)
        {
            Assert.Equal(a[i], b[i], 6);
        }
    }

    private static string Letters(string path) => Commands().Replace(NumberPattern().Replace(path, string.Empty), string.Empty);

    private static double[] Numbers(string path) =>
        [.. NumberPattern().Matches(path).Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))];

    [GeneratedRegex("[^A-Za-z]")]
    private static partial Regex Commands();

    [GeneratedRegex(@"-?\d+(\.\d+)?(e-?\d+)?")]
    private static partial Regex NumberPattern();

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

    /// <summary>
    /// A page that shows one markup per step, each post moving to the next, in Chromium with Playwright's clock paused on
    /// one of its 16 ms animation frames, so a frame falls on every time the tests run the clock to.
    /// </summary>
    private sealed class Stage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page, Steps steps) : IAsyncDisposable
    {
        public IPage Page => page;

        public static Task<Stage> StartAsync(params string[] markups) => StartAsync(ReducedMotion.NoPreference, markups);

        public static async Task<Stage> StartAsync(ReducedMotion motion, params string[] markups)
        {
            var steps = new Steps(markups);
            var site = new Site();
            site.Engine.Command = _ =>
            {
                steps.Index++;
                return ValueTask.CompletedTask;
            };
            site.Engine.Plan = new RenderPlan([], [
                new LiteralNode("<button id=\"next\">Next</button><i id=\"step\" data-n=\""), new ValueNode(static model => ((Steps)model!).Index),
                new LiteralNode("\"></i>"), new ValueNode(static model => ((Steps)model!).Markup, Raw: true),
            ], []);
            var app = await HostAsync(site, [new PageDefinition("/morph", "Pages/morph/Page.axaml", _ => steps, [])]);
            var playwright = await Playwright.CreateAsync();
            var browser = await LaunchAsync(playwright);
            var page = await browser.NewPageAsync(new() { ReducedMotion = motion });
            await page.Clock.InstallAsync(new() { TimeDate = DateTime.UnixEpoch });
            await page.Clock.PauseAtAsync(DateTime.UnixEpoch.AddMilliseconds(1024));
            var module = page.WaitForResponseAsync(response => response.Url.Contains(MorphScript.Prefix, StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/morph");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", MorphScript.Path);
            var now = await page.EvaluateAsync<double>("() => performance.now()");
            await page.Clock.RunForAsync((long)((16 - (now % 16)) % 16));
            return new Stage(app, playwright, browser, page, steps);
        }

        public async Task NextAsync()
        {
            var next = steps.Index + 1;
            await page.ClickAsync("#next");
            await page.WaitForFunctionAsync("n => document.getElementById('step').dataset.n === String(n)", next, new() { PollingInterval = 20 }).WaitAsync(TestContext.Current.CancellationToken);
        }

        public Task<string?> AttributeAsync(string selector, string name) =>
            page.EvaluateAsync<string?>("([s, n]) => document.querySelector(s).getAttribute(n)", new[] { selector, name });

        public async ValueTask DisposeAsync()
        {
            await browser.DisposeAsync();
            playwright.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class Steps(string[] markups) : IPageModel
    {
        public string Title => "Morph";

        public int Index { get; set; }

        public string Markup => markups[Math.Min(Index, markups.Length - 1)];
    }
}
