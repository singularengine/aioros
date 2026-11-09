using System.Threading.Channels;
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

public sealed class LiveQueryScriptTests
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-179")]
    public async Task ANavigationAnsweredWithDynamicsReopensTheLiveStreamWithItsQueryAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<h1>"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</h1><a id=\"b\" href=\"/inbox?app=b\">B</a>")], []);
        var models = Channel.CreateUnbounded<Model>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("AIOROS_TEST_URL") ?? "http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        await using var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/inbox", "Pages/inbox/Page.axaml", services =>
        {
            var model = new Model(services.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Query["app"].ToString());
            models.Writer.TryWrite(model);
            return model;
        }, [], Live: true)]);
        await app.StartAsync(bound);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var navigations = new List<string>();
        page.Request += (_, request) =>
        {
            if (request.Headers.ContainsKey("x-aioros-plan"))
            {
                navigations.Add(request.Url);
            }
        };

        var first = page.WaitForRequestAsync(request => request.Url.Contains("/live?app=a&plan=", StringComparison.Ordinal));
        await page.GotoAsync(address + "/inbox?app=a");
        await first.WaitAsync(bound);
        var reopened = page.WaitForResponseAsync(response => response.Url.Contains("/live?app=b&plan=", StringComparison.Ordinal));
        await page.ClickAsync("#b");
        await reopened.WaitAsync(bound);
        await page.WaitForFunctionAsync("() => document.querySelector('h1').textContent === 'b'").WaitAsync(bound);
        while (models.Reader.TryRead(out var model))
        {
            model.Title += " pushed";
        }

        await page.WaitForFunctionAsync("() => document.querySelector('h1').textContent.endsWith('pushed')").WaitAsync(bound);
        var pushed = await page.InnerTextAsync("h1");

        Assert.Equal([address + "/inbox?app=b"], navigations);
        Assert.Equal("b pushed", pushed);
    }
}
