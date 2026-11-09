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

/// <summary>
/// A page for the swipe and carousel modules' tests: its markup is written from the step a post has reached (each post
/// moves to the next step), and the page waits for the module named by <c>prefix</c> to load before a test runs; with
/// <c>clock</c> the browser's clock is Playwright's, paused once the page is ready, which a test moves by hand.
/// </summary>
internal sealed class GestureStage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page, GestureStage.Steps steps) : IAsyncDisposable
{
    public IPage Page => page;

    public int Step => steps.Index;

    public static async Task<GestureStage> StartAsync(Func<int, string> markup, string prefix, bool clock = false)
    {
        var steps = new Steps();
        var site = new Site();
        site.Engine.Command = _ =>
        {
            steps.Index++;
            return ValueTask.CompletedTask;
        };
        site.Engine.Plan = new RenderPlan([], [new ValueNode(model => markup(((Steps)model!).Index), Raw: true)], []);
        var app = await HostAsync(site, [new PageDefinition("/gestures", "Pages/gestures/Page.axaml", _ => steps, [])]);
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        if (clock)
        {
            await page.Clock.InstallAsync();
        }

        var module = page.WaitForResponseAsync(response => response.Url.Contains(prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/gestures");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await page.WaitForFunctionAsync("() => document.readyState === 'complete'", null, new() { PollingInterval = 20 });
        if (clock)
        {
            await page.Clock.PauseAtAsync(DateTime.UtcNow.AddMinutes(1));
        }

        return new GestureStage(app, playwright, browser, page, steps);
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

    internal sealed class Steps : IPageModel
    {
        public string Title => "Gestures";

        public int Index { get; set; }
    }
}
