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

public sealed class CountScriptTests
{
    private const string Markup = """
        <div id="s" style="height:500px;overflow-y:auto">
          <div id="a" data-aioros-count="1000">85,620</div><div id="d" data-aioros-count="1000">1.234,5 ms</div><div id="w" data-aioros-count="1000">Soon</div>
          <div style="height:3000px"></div><div id="b" data-aioros-count="1000">10.2×</div>
        </div>
        """;

    [Fact]
    [Trait("AC", "MOTION-28")]
    public async Task NumbersCountUpInTheirOwnFormatWhenTheyComeIntoViewAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Markup)], []);
        await using var app = await HostAsync(site, "/count");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        await page.Clock.InstallAsync();
        var module = page.WaitForResponseAsync(response => response.Url.Contains(RevealScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/count");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await page.WaitForFunctionAsync("() => document.getElementById('b').textContent === '0.0×'", null, new() { PollingInterval = 20 });
        const string Texts = "() => ['a', 'd', 'w', 'b'].map(id => document.getElementById(id).textContent).join('|')";
        await page.Clock.RunForAsync(400);
        var halfway = (await page.EvaluateAsync<string>(Texts)).Split('|');
        await page.Clock.RunForAsync(1000);
        var counted = await page.EvaluateAsync<string>(Texts);
        await page.EvaluateAsync("() => document.getElementById('b').scrollIntoView()");
        await page.WaitForFunctionAsync("() => document.getElementById('b').textContent !== '0.0×' || document.getElementById('b').hasAttribute('data-aioros-counting')", null, new() { PollingInterval = 20 });
        await page.Clock.RunForAsync(1500);
        var below = await page.EvaluateAsync<string>("() => document.getElementById('b').textContent");

        var reduced = await browser.NewPageAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        var still = reduced.WaitForResponseAsync(response => response.Url.Contains(RevealScript.Prefix, StringComparison.Ordinal));
        await reduced.GotoAsync(Address(app) + "/count");
        await still.WaitAsync(TestContext.Current.CancellationToken);
        await reduced.EvaluateAsync("u => import(u).then(() => 0)", RevealScript.Path);
        var unmoved = await reduced.EvaluateAsync<string>(Texts);

        Assert.Matches(@"^\d{1,2},\d{3}$|^\d{1,3}$", halfway[0]);
        Assert.NotEqual("85,620", halfway[0]);
        Assert.Matches(@"^(\d{1,3}(\.\d{3})*),\d ms$", halfway[1]);
        Assert.Equal("Soon", halfway[2]);
        Assert.Equal("85,620|1.234,5 ms|Soon|0.0×", counted);
        Assert.Equal("10.2×", below);
        Assert.Equal("85,620|1.234,5 ms|Soon|10.2×", unmoved);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> HostAsync(Site site, string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition(path, $"Pages{path}/Page.axaml", _ => new Counts(), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Counts : IPageModel
    {
        public string Title => "Counts";
    }
}
