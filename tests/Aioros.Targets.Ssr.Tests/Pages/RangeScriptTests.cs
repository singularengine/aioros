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

public sealed class RangeScriptTests
{
    private const string Group = """
        <style>#g{display:grid;width:400px;height:6px;margin:40px;touch-action:none}#g>input{grid-area:1/1;width:400px;height:6px;margin:0}</style>
        <div id="g" data-aioros-range><input id="lo" type="range" name="f0" min="0" max="1000" step="10" value="200"><input id="hi" type="range" name="f1" min="0" max="1000" step="10" value="800"><input type="hidden" name="_range" value="f0 f1"></div>
        """;

    private const string State = "() => { const g = document.getElementById('g'), a = document.activeElement; return document.getElementById('lo').value + '-' + document.getElementById('hi').value + (a && a.parentElement === g ? ' ' + a.id : '') + ' ' + (window.__inputs ?? 0) }";

    [Fact]
    [Trait("AC", "RANGES-31")]
    public async Task ARangeTakesThePointerAndTheKeysAsRadixDoesAsync()
    {
        await using var stage = await Stage.StartAsync(Group);
        var page = stage.Page;
        await page.EvaluateAsync("() => { window.__inputs = 0; document.getElementById('g').addEventListener('input', () => window.__inputs++); document.getElementById('g').addEventListener('change', () => window.__inputs += 100) }");
        var box = (await page.Locator("#g").BoundingBoxAsync())!;
        float At(double value) => box.X + (float)(value / 1000 * box.Width);
        var y = box.Y + (box.Height / 2);
        var steps = new List<string>();
        async Task StepAsync() => steps.Add(await page.EvaluateAsync<string>(State));

        await page.Mouse.MoveAsync(At(703), y);
        await page.Mouse.DownAsync();
        await StepAsync();
        await page.Mouse.MoveAsync(At(750), y);
        await page.Mouse.UpAsync();
        await StepAsync();
        await page.Mouse.MoveAsync(At(100), y);
        await page.Mouse.DownAsync();
        await page.Mouse.UpAsync();
        await StepAsync();
        await page.Mouse.MoveAsync(At(100), y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(At(900), y);
        await page.Mouse.UpAsync();
        await StepAsync();
        await page.FocusAsync("#lo");
        await page.Keyboard.PressAsync("ArrowRight");
        await StepAsync();
        await page.Keyboard.PressAsync("Shift+ArrowRight");
        await StepAsync();
        await page.Keyboard.PressAsync("PageDown");
        await StepAsync();
        await page.Keyboard.PressAsync("PageUp");
        await page.Keyboard.PressAsync("PageUp");
        await StepAsync();
        await page.Keyboard.PressAsync("Home");
        await StepAsync();
        await page.Keyboard.PressAsync("End");
        await StepAsync();

        Assert.Equal(
            ["200-700 hi 101", "200-750 hi 202", "100-750 lo 303", "750-900 hi 505", "760-900 lo 606", "860-900 lo 707", "760-900 lo 808", "900-960 hi 1111", "0-960 lo 1212", "0-1000 hi 1313"],
            steps);
    }

    private sealed class Stage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page) : IAsyncDisposable
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
            var app = await HostAsync(site, [new PageDefinition("/range", "Pages/range/Page.axaml", _ => steps, [])]);
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
            var module = page.WaitForResponseAsync(response => response.Url.Contains(RangeScript.Prefix, StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/range");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", RangeScript.Path);
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

    private sealed class Steps : IPageModel
    {
        public string Title => "Range";

        public int Index { get; set; }
    }
}
