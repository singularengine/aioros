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

public sealed class AnswerScriptTests
{
    [Fact]
    [Trait("AC", "SSR-254")]
    public async Task AButtonWaitsForAnAnswerInTheBrowserAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <div data-aioros-answers>
              <div id="shown"><label><input type="radio" name="f0" id="a">A</label><label><input type="radio" name="f0" id="b">B</label><input type="text" id="t"></div>
              <div style="display:none"><label><input type="radio" name="f1" checked>Earlier</label></div>
              <button type="submit" name="_btn" value="0|" id="go" data-aioros-needs-answer disabled>Next</button>
            </div>
            """);
        var page = stage.Page;
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" || request.ResourceType == "document" ? 1 : 0;
        const string Disabled = "() => document.getElementById('go').disabled";

        var start = await page.EvaluateAsync<bool>(Disabled);
        await page.ClickAsync("#a");
        await page.WaitForFunctionAsync("() => !document.getElementById('go').disabled", null, new() { PollingInterval = 20 });
        await page.EvaluateAsync("() => { const a = document.getElementById('a'); a.checked = false; a.dispatchEvent(new Event('change', { bubbles: true })) }");
        await page.WaitForFunctionAsync(Disabled, null, new() { PollingInterval = 20 });
        await page.FillAsync("#t", "Another approach");
        await page.WaitForFunctionAsync("() => !document.getElementById('go').disabled", null, new() { PollingInterval = 20 });

        Assert.True(start);
        Assert.Equal(0, requests);
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
            var app = await HostAsync(site, [new PageDefinition("/answer", "Pages/answer/Page.axaml", _ => steps, [])]);
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
            var module = page.WaitForResponseAsync(response => response.Url.Contains(AnswerScript.Prefix, StringComparison.Ordinal));
            var plan = page.WaitForResponseAsync(response => response.Url.Contains("/_aioros/plan/", StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/answer");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await plan.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", AnswerScript.Path);
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
        public string Title => "Answer";

        public int Index { get; set; }
    }
}
