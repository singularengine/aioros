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

public sealed class LiveStateScriptTests
{
    [Fact]
    [Trait("AC", "SSR-261")]
    public async Task EachTabsLivePageKeepsItsOwnStateAsync()
    {
        var models = new List<Model>();
        Model? pressed = null;
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<p id=\"title\">"), new ValueNode(static model => ((Model)model!).Title),
            new LiteralNode("</p><button type=\"submit\" name=\"_btn\" value=\"0|\" id=\"go\">Go</button>"),
        ], []);
        site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Plan = site.Engine.Plan });
        site.Engine.Command = models =>
        {
            pressed = (Model)models[0]!;
            pressed.Title = "Pressed";
            return ValueTask.CompletedTask;
        };
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ =>
        {
            var model = new Model("Start");
            models.Add(model);
            return model;
        }, [], Live: true);
        await using var app = await HostAsync(site, page);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var first = await OpenAsync(browser, app);
        var second = await OpenAsync(browser, app);
        var streams = 0;
        first.Request += (_, request) => streams += request.Url.Contains("/live", StringComparison.Ordinal) ? 1 : 0;

        var answered = first.WaitForResponseAsync(static response => response.Request.Method == "POST");
        await first.ClickAsync("#go");
        await answered;
        await first.WaitForFunctionAsync("() => document.getElementById('title').textContent === 'Pressed'", null, new() { PollingInterval = 10 });
        pressed!.Title = "Played";
        await first.WaitForFunctionAsync("() => document.getElementById('title').textContent === 'Played'", null, new() { PollingInterval = 10 });

        Assert.Equal("Start", await second.TextContentAsync("#title"));
        Assert.Equal(0, streams);
        Assert.Contains(pressed, models);
        Assert.All(models.Where(model => model != pressed), static model => Assert.Equal("Start", model.Title));
    }

    /// <summary>Opens the page in a tab of its own, once the stream it opens with the page's plan named its connection.</summary>
    private static async Task<IPage> OpenAsync(IBrowser browser, WebApplication app)
    {
        var page = await (await browser.NewContextAsync()).NewPageAsync();
        await page.RunAndWaitForResponseAsync(
            () => page.GotoAsync(Address(app) + "/inbox"),
            static response => response.Url.Contains("/live?", StringComparison.Ordinal) && response.Url.Contains("plan=", StringComparison.Ordinal));
        await page.WaitForFunctionAsync("() => document.documentElement.hasAttribute('data-aioros-live')", null, new() { PollingInterval = 10 });
        return page;
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> HostAsync(Site site, PageDefinition page)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([page]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
