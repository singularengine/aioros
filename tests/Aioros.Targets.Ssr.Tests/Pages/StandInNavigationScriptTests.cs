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

public sealed class StandInNavigationScriptTests
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-181")]
    public async Task ANavigationToADocumentWithoutTheFormLoadsItWholeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<a id=\"files\" href=\"/files\">Files</a>")], []);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        await using var app = builder.Build();
        app.MapAiorosPages([
            new PageDefinition("/orders", "Pages/orders/Page.axaml", static _ => null, []),
            new PageDefinition("/files", "Pages/files/Page.axaml", static _ => null, []) { Platforms = PagePlatforms.Desktop },
        ]);
        await app.StartAsync(cancellationToken);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var errors = new List<string>();
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.PageError += (_, error) =>
        {
            errors.Add(error);
            settled.TrySetResult();
        };

        await page.GotoAsync(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First() + "/orders");
        await page.EvaluateAsync("() => { window.marker = 1; }");
        page.Load += (_, _) => settled.TrySetResult();
        await page.ClickAsync("#files");
        await settled.Task.WaitAsync(cancellationToken);

        Assert.Empty(errors);
        Assert.Equal("/files", new Uri(page.Url).AbsolutePath);
        Assert.Equal(await page.TitleAsync(), await page.TextContentAsync("h1"));
        Assert.Null(await page.EvaluateAsync<int?>("() => window.marker"));
    }
}
