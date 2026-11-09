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

public sealed class KeyScopeScriptTests
{
    [Fact]
    [Trait("AC", "SSR-255")]
    public async Task AKeyScopesButtonsAnswerTheirKeysInTheBrowserAsync()
    {
        await using var stage = await Stage.StartAsync("""
            <div data-aioros-keyscope>
              <label><input type="radio" name="f0" id="radio">A</label><label><input type="checkbox" id="check">B</label><input type="text" id="text">
              <button type="button" id="hidden" style="display:none" data-aioros-keys="ctrl+Enter ArrowRight" onclick="hits.push(this.id)">Next</button>
              <button type="button" id="shown" data-aioros-keys="ctrl+Enter ArrowRight" onclick="hits.push(this.id)">Next</button>
              <button type="button" id="enter" data-aioros-keys="Enter" onclick="hits.push(this.id)">Submit</button>
              <div data-aioros-keyscope><input type="text" id="inner"><button type="button" id="own" data-aioros-keys="ctrl+Enter" onclick="hits.push(this.id)">Own</button></div>
            </div>
            <input type="text" id="outside">
            """);
        var page = stage.Page;
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" || request.ResourceType == "document" ? 1 : 0;
        await page.EvaluateAsync("() => window.hits = []");
        async Task<string> PressAsync(string id, string key)
        {
            await page.FocusAsync("#" + id);
            await page.Keyboard.PressAsync(key);
            return await page.EvaluateAsync<string>("() => window.hits.splice(0).join(',')");
        }

        var presses = new List<string>
        {
            await PressAsync("text", "Control+Enter"),
            await PressAsync("inner", "Control+Enter"),
            await PressAsync("text", "ArrowRight"),
            await PressAsync("radio", "ArrowRight"),
            await PressAsync("check", "ArrowRight"),
            await PressAsync("check", "Enter"),
        };
        await page.EvaluateAsync("() => document.getElementById('check').checked = true");
        presses.Add(await PressAsync("check", "Enter"));
        presses.Add(await PressAsync("text", "Enter"));
        await page.FillAsync("#text", "Another approach");
        presses.Add(await PressAsync("text", "Enter"));
        presses.Add(await PressAsync("outside", "Control+Enter"));

        Assert.Equal(["shown", "own", "", "", "shown", "", "enter", "", "enter", ""], presses);
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
            var app = await HostAsync(site, [new PageDefinition("/keys", "Pages/keys/Page.axaml", _ => steps, [])]);
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
            var module = page.WaitForResponseAsync(response => response.Url.Contains(KeyboardScript.Prefix, StringComparison.Ordinal));
            var plan = page.WaitForResponseAsync(response => response.Url.Contains("/_aioros/plan/", StringComparison.Ordinal));
            await page.GotoAsync(Address(app) + "/keys");
            await module.WaitAsync(TestContext.Current.CancellationToken);
            await plan.WaitAsync(TestContext.Current.CancellationToken);
            await page.EvaluateAsync("u => import(u).then(() => 0)", KeyboardScript.Path);
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
        public string Title => "Keys";

        public int Index { get; set; }
    }
}
