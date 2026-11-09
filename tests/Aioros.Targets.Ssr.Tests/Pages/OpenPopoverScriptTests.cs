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

public sealed class OpenPopoverScriptTests
{
    private static string Markup(bool open, int step) =>
        $"<p id=\"step\">{step}</p><button type=\"button\" popovertarget=\"p\">Toggle</button><div popover id=\"p\"{(open ? " data-aioros-open" : string.Empty)}>Inside</div>"
        + "<button type=\"submit\" name=\"_btn\" value=\"0|\" id=\"go\">Go</button>";

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-198")]
    public async Task MarkedPopoversFollowTheirOpenStateAsync()
    {
        var site = new Site();
        bool[] answers = [true, false, true];
        var posts = 0;
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Markup(true, 0))], []);
        site.Engine.Command = _ =>
        {
            var step = ++posts;
            site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Plan = new RenderPlan([], [new LiteralNode(Markup(answers[step - 1], step))], []) });
            return ValueTask.CompletedTask;
        };
        await using var app = await StartAsync(site);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var bound = TestContext.Current.CancellationToken;
        const string Open = "() => document.getElementById('p').matches(':popover-open')";

        await page.GotoAsync(Address(app) + "/open");
        await page.WaitForFunctionAsync(Open).WaitAsync(bound);
        await page.EvaluateAsync("() => document.getElementById('p').hidePopover()");
        var states = new List<bool>();
        foreach (var step in (int[])[1, 2, 3])
        {
            await page.EvaluateAsync("() => document.getElementById('go').click()");
            await page.WaitForFunctionAsync($"() => document.getElementById('step').textContent === '{step}'").WaitAsync(bound);
            states.Add(await page.EvaluateAsync<bool>(Open));
        }

        Assert.Equal([false, false, true], states);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-268")]
    public async Task APlannedAnswerResetsTheOpenPopoversAsync()
    {
        var site = new Site(fields: new Retitle());
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<p id=\"step\">"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</p><button type=\"button\" popovertarget=\"p\" id=\"toggle\">Toggle</button><div popover id=\"p\">Inside</div><div popover=\"manual\" id=\"q\" data-aioros-open>Kept</div><button type=\"submit\" name=\"_btn\" value=\"0|go\" id=\"go\">Go</button>"), new IdNode { Field = new SlotSpec("command"), Button = true }], []);
        await using var app = await StartAsync(site, () => new Model("Open"));
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var bound = TestContext.Current.CancellationToken;
        var answers = new List<string>();
        page.Response += (_, response) =>
        {
            if (response.Request.Method == "POST")
            {
                answers.Add(response.Headers.GetValueOrDefault("content-type", string.Empty));
            }
        };

        await page.GotoAsync(Address(app) + "/open");
        await page.ClickAsync("#toggle");
        await page.WaitForFunctionAsync("() => document.getElementById('p').matches(':popover-open')").WaitAsync(bound);
        await page.EvaluateAsync("() => document.getElementById('go').click()");
        await page.WaitForFunctionAsync("() => document.getElementById('step').textContent === 'Posted'").WaitAsync(bound);
        var open = await page.EvaluateAsync<bool[]>("() => ['p', 'q'].map(id => document.getElementById(id).matches(':popover-open'))");

        Assert.Null(site.Engine.Posted);
        Assert.Contains("application/json", Assert.Single(answers), StringComparison.Ordinal);
        Assert.Equal([false, true], open);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static Task<WebApplication> StartAsync(Site site) => StartAsync(site, () => new Opened());

    private static async Task<WebApplication> StartAsync(Site site, Func<object> model)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/open", "Pages/open/Page.axaml", _ => model(), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Retitle : IFieldResolver
    {
        public FieldBinding? Binding(SlotSpec field) => null;

        public FieldCommand? Command(SlotSpec field) => new(_ => "go", (scope, _) =>
        {
            ((Model)scope!).Title = "Posted";
            return ValueTask.CompletedTask;
        });
    }

    private sealed class Opened : IPageModel
    {
        public string Title => "Open";
    }
}
