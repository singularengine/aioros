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

public sealed class CodeSlotsScriptTests
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-252")]
    public async Task CodeSlotsShowTheInputsCharactersAndItsCaretAsync()
    {
        var box = new Box();
        var site = new Site();
        var slots = string.Concat(Enumerable.Range(0, 6).Select(_ => "<div><div data-aioros-slot-box><div data-aioros-slot-text></div></div></div>"));
        site.Engine.Plan = new RenderPlan([], [new LiteralNode($"<div><div data-aioros-slots>{slots}</div><input id=\"code\" type=\"text\" name=\"f0\" maxlength=\"6\" data-aioros-slots-input></div><button id=\"other\" type=\"button\">Other</button>")], []);
        await using var app = await StartAsync(site, box);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var posts = 0;
        page.Request += (_, request) => posts += request.Method == "POST" ? 1 : 0;
        const string State = "() => [...document.querySelectorAll('[data-aioros-slot-box]')].map(b => b.querySelector('[data-aioros-slot-text]').textContent + (b.hasAttribute('data-aioros-active') ? '*' : '')).join(',')";

        await page.GotoAsync(Address(app) + "/code").WaitAsync(TestContext.Current.CancellationToken);
        await page.FocusAsync("#code");
        var focused = await page.EvaluateAsync<string>(State);
        await page.Keyboard.TypeAsync("12");
        var two = await page.EvaluateAsync<string>(State);
        await page.Keyboard.TypeAsync("3456");
        var full = await page.EvaluateAsync<string>(State);
        await page.FocusAsync("#other");
        var left = await page.EvaluateAsync<string>(State);

        Assert.Equal("*,,,,,", focused);
        Assert.Equal("1,2,*,,,", two);
        Assert.Equal("1,2,3,4,5,6*", full);
        Assert.Equal("1,2,3,4,5,6", left);
        Assert.Equal(0, posts);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> StartAsync(Site site, Box box)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/code", "Pages/code/Page.axaml", _ => box, [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Box : IPageModel
    {
        public string Title => "Code";
    }
}
