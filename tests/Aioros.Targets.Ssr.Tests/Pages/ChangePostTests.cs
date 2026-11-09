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

public sealed class ChangePostTests
{
    private sealed class Model(string title) : IPageModel
    {
        public string Title { get; set; } = title;
    }

    private static Task<IBrowser> LaunchAsync(IPlaywright playwright) =>
        playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });

    private static string AddressOf(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<string> PostAfterAsync(IPage page, Func<Task> act)
    {
        var request = page.WaitForRequestAsync(static request => request.Method == "POST");
        var answer = page.WaitForResponseAsync(static response => response.Request.Method == "POST");
        await act();
        var body = (await request).PostData!;
        await answer;
        return body;
    }

    [Fact]
    [Trait("AC", "SSR-316")]
    public async Task AnExpanderPostsItsOwnToggleOnlyAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("""
            <details id="box" data-change open><summary id="head">Edit</summary><input type="checkbox" hidden name="f0" checked>
            <div><input id="plain" name="f1"><input id="own" type="checkbox" name="f2" data-change></div></details>
            <button id="go" name="_btn" value="1|x">Save</button>
            """)], []);
        await using var app = await StartAsync(site, [new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model("Start"), [])]);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        var posted = new List<string>();
        page.Request += (_, request) =>
        {
            if (request.Method == "POST")
            {
                posted.Add(request.PostData!);
            }
        };

        await page.GotoAsync(AddressOf(app) + "/inbox");
        await page.FillAsync("#plain", "typed");
        var press = await PostAfterAsync(page, () => page.ClickAsync("#go"));
        var tick = await PostAfterAsync(page, () => page.ClickAsync("#own"));
        var toggle = await PostAfterAsync(page, () => page.ClickAsync("#head"));

        Assert.Equal(3, posted.Count);
        Assert.Contains("_btn=", press, StringComparison.Ordinal);
        Assert.DoesNotContain("_chg=", press, StringComparison.Ordinal);
        Assert.Contains("_chg=2", tick, StringComparison.Ordinal);
        Assert.Contains("_chg=0", toggle, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-317")]
    public async Task APressDuringAChangePostIsSentAfterItAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<input id=\"own\" type=\"checkbox\" name=\"f2\" data-change>"),
            new ConditionNode(scope => ((Model)scope!).Title != "Gone", [new LiteralNode("<button id=\"go\" name=\"_btn\" value=\"1|x\">Save</button>")]),
        ], []);
        var gate = new TaskCompletionSource();
        var next = "Kept";
        site.Engine.Command = async models =>
        {
            await gate.Task;
            ((Model)models[0]!).Title = next;
        };
        await using var app = await StartAsync(site, [new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model("Start"), [])]);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        var posted = new List<string>();
        page.Request += (_, request) =>
        {
            if (request.Method == "POST")
            {
                posted.Add(request.PostData!);
            }
        };

        await page.GotoAsync(AddressOf(app) + "/inbox");
        var change = page.WaitForRequestAsync(static request => request.Method == "POST");
        await page.ClickAsync("#own");
        await change;
        await page.ClickAsync("#go");
        var whileWaiting = posted.Count;
        var press = page.WaitForRequestAsync(static request => request.Method == "POST" && request.PostData!.Contains("_btn=", StringComparison.Ordinal));
        gate.SetResult();
        var sent = (await press).PostData!;

        await page.GotoAsync(AddressOf(app) + "/inbox");
        posted.Clear();
        gate = new TaskCompletionSource();
        next = "Gone";
        var second = page.WaitForRequestAsync(static request => request.Method == "POST");
        await page.ClickAsync("#own");
        await second;
        await page.ClickAsync("#go");
        gate.SetResult();
        await page.WaitForSelectorAsync("#go", new() { State = WaitForSelectorState.Detached });
        next = "Start";
        var third = PostAfterAsync(page, () => page.ClickAsync("#own"));
        await third;

        Assert.Equal(1, whileWaiting);
        Assert.DoesNotContain("_chg=", sent, StringComparison.Ordinal);
        Assert.Equal(2, posted.Count);
        Assert.All(posted, static body => Assert.Contains("_chg=2", body, StringComparison.Ordinal));
    }

    [Theory]
    [Trait("AC", "SSR-318")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TextTypedDuringAChangePostIsKeptAsync(bool movesOn)
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<h1>"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</h1><input id=\"own\" type=\"checkbox\" name=\"f2\" data-change><input id=\"typed\" name=\"f1\" value=\""),
            new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("\"><input id=\"next\" name=\"f3\" value=\""), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("\">"),
        ], []);
        var gate = new TaskCompletionSource();
        site.Engine.Command = async models =>
        {
            await gate.Task;
            ((Model)models[0]!).Title = "Answered";
        };
        await using var app = await StartAsync(site, [new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model("Start"), [])]);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();

        await page.GotoAsync(AddressOf(app) + "/inbox");
        await page.WaitForFunctionAsync("() => performance.getEntriesByType('resource').some(entry => entry.name.includes('/_aioros/plan/'))");
        var change = page.WaitForRequestAsync(static request => request.Method == "POST");
        await page.ClickAsync("#own");
        await change;
        await page.FillAsync("#typed", "mine");
        if (movesOn)
        {
            await page.FocusAsync("#next");
        }

        gate.SetResult();
        await page.WaitForFunctionAsync("() => document.querySelector('h1').textContent === 'Answered'");

        Assert.Equal("mine", await page.InputValueAsync("#typed"));
        Assert.Equal(movesOn ? "next" : "typed", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Equal("Answered", await page.InputValueAsync("#next"));
    }

    [Fact]
    [Trait("AC", "SSR-433")]
    public async Task APressHeldDuringAChangeIsDroppedWhenTheListChangedAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<input id=\"own\" type=\"checkbox\" name=\"f2\" data-change><input type=\"hidden\" name=\"_shape\" value=\""), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("\"><h1>"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</h1><button id=\"go\" name=\"_btn\" value=\"1|x\">Delete</button>"),
        ], []);
        var gate = new TaskCompletionSource();
        var answers = 0;
        site.Engine.Command = async models =>
        {
            await gate.Task;
            ((Model)models[0]!).Title = "Changed" + ++answers;
        };
        await using var app = await StartAsync(site, [new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model("Start"), [])]);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        var posted = new List<string>();
        page.Request += (_, request) =>
        {
            if (request.Method == "POST")
            {
                posted.Add(request.PostData!);
            }
        };

        await page.GotoAsync(AddressOf(app) + "/inbox");
        var change = page.WaitForRequestAsync(static request => request.Method == "POST");
        await page.ClickAsync("#own");
        await change;
        await page.ClickAsync("#go");
        gate.SetResult();
        await page.WaitForFunctionAsync("() => document.querySelector('h1').textContent === 'Changed1' && document.querySelector('form').getAttribute('aria-busy') === null");
        await PostAfterAsync(page, () => page.ClickAsync("#own"));

        Assert.Equal(2, posted.Count);
        Assert.All(posted, static body => Assert.Contains("_chg=2", body, StringComparison.Ordinal));
    }

    private static async Task<WebApplication> StartAsync(Site site, IReadOnlyList<PageDefinition> pages)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor().AddSingleton<Runtime.Caching.DataVersions>()
            .AddScoped<Runtime.Pages.PageNavigator>().AddScoped<Aioros.Navigation.INavigator>(services => services.GetRequiredService<Runtime.Pages.PageNavigator>());
        var app = builder.Build();
        app.MapAiorosPages(pages);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
