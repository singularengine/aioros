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

public sealed class FormBusyScriptTests
{
    [Fact]
    [Trait("AC", "SSR-243")]
    public async Task AFormStaysBusyUntilItsQueuedChangesAreAnsweredAsync()
    {
        var model = new Choices();
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var site = new Site();
        site.Engine.Command = async _ =>
        {
            var posted = site.Engine.Posted;
            model.First = posted?.ContainsKey("f0") == true;
            model.Second = posted?.ContainsKey("f1") == true;
            if (model.First && !model.Second)
            {
                await held.Task;
            }
        };
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<div data-change><input type=\"checkbox\" id=\"a\" name=\"f0\" value=\"on\""), new ConditionNode(m => ((Choices)m!).First, [new LiteralNode(" checked")]),
            new LiteralNode("><input type=\"checkbox\" id=\"b\" name=\"f1\" value=\"on\""), new ConditionNode(m => ((Choices)m!).Second, [new LiteralNode(" checked")]),
            new LiteralNode("></div>"),
        ], []);
        await using var app = await HostAsync(site, model);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        await page.GotoAsync(Address(app) + "/choices");
        await page.EvaluateAsync("() => { const f = document.querySelector('form'); window.idle = []; new MutationObserver(() => f.hasAttribute('aria-busy') || window.idle.push([document.getElementById('a').checked, document.getElementById('b').checked].join())).observe(f, { attributes: true, attributeFilter: ['aria-busy'] }) }");
        var posts = 0;
        page.Request += (_, request) => posts += request.Method == "POST" ? 1 : 0;

        await page.CheckAsync("#a");
        await page.WaitForFunctionAsync("() => document.querySelector('form').hasAttribute('aria-busy')").WaitAsync(TestContext.Current.CancellationToken);
        await page.CheckAsync("#b");
        held.SetResult();
        await page.WaitForFunctionAsync("() => !document.querySelector('form').hasAttribute('aria-busy')").WaitAsync(TestContext.Current.CancellationToken);
        var idle = await page.EvaluateAsync<string[]>("() => window.idle");
        await page.RouteAsync("**/choices", route => route.Request.Method == "POST" ? route.AbortAsync() : route.ContinueAsync());
        await page.UncheckAsync("#b");
        await page.WaitForFunctionAsync("n => !document.querySelector('form').hasAttribute('aria-busy') && window.idle.length > n", idle.Length).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["true,true"], idle);
        Assert.Equal(3, posts);
        Assert.True(model.First && model.Second);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> HostAsync(Site site, Choices model)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/choices", "Pages/choices/Page.axaml", _ => model, [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Choices : IPageModel
    {
        public string Title => "Choices";

        public bool First { get; set; }

        public bool Second { get; set; }
    }
}
