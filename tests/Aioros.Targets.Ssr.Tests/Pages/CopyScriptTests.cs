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

public sealed class CopyScriptTests
{
    [Fact]
    [Trait("AC", "SSR-232")]
    public async Task ButtonsCopyTheirTextInTheBrowserAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<button type=\"button\" id=\"b\" data-aioros-copies=\"npm install\">Copy</button>")], []);
        var plain = new Site();
        plain.Engine.Plan = new RenderPlan([], [new LiteralNode("<button id=\"p\">Plain</button>")], []);
        await using var app = await HostAsync(site, "/copy");
        await using var other = await HostAsync(plain, "/plain");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var context = await browser.NewContextAsync(new() { Permissions = ["clipboard-read", "clipboard-write"] });
        var page = await context.NewPageAsync();
        await page.Clock.InstallAsync();
        var module = page.WaitForResponseAsync(response => response.Url.Contains(CopyScript.Prefix, StringComparison.Ordinal));
        var plan = page.WaitForResponseAsync(response => response.Url.Contains("/_aioros/plan/", StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/copy");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await plan.WaitAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("u => import(u).then(() => 0)", CopyScript.Path);
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" || request.ResourceType == "document" ? 1 : 0;
        const string Marked = "() => document.getElementById('b').hasAttribute('data-copied')";

        await page.ClickAsync("#b");
        await page.WaitForFunctionAsync(Marked, null, new() { PollingInterval = 20 });
        var copied = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        var timer = await page.EvaluateAsync<string>("() => String(document.getElementById('b').aiorosCopied)");
        await page.Clock.RunForAsync(1500);
        await page.ClickAsync("#b");
        await page.WaitForFunctionAsync("t => String(document.getElementById('b').aiorosCopied) !== t", timer, new() { PollingInterval = 20 });
        await page.Clock.RunForAsync(1000);
        var kept = await page.EvaluateAsync<bool>(Marked);
        await page.Clock.RunForAsync(1100);
        var gone = !await page.EvaluateAsync<bool>(Marked);
        var sent = requests;
        var loads = new List<string>();
        var second = await context.NewPageAsync();
        second.Request += (_, request) => loads.Add(request.Url);
        await second.GotoAsync(Address(other) + "/plain", new() { WaitUntil = WaitUntilState.NetworkIdle });

        Assert.Equal(("npm install", true, true, 0, false), (copied, kept, gone, sent, loads.Exists(url => url.Contains(CopyScript.Prefix, StringComparison.Ordinal))));
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> HostAsync(Site site, string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition(path, $"Pages{path}/Page.axaml", _ => new Copies(), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Copies : IPageModel
    {
        public string Title => "Copies";
    }
}
