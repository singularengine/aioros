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

public sealed class LayoutPostScriptTests
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-172")]
    public async Task AnEnginePostAnswerKeepsTheLayoutAsync()
    {
        var model = new Model("start");
        var site = new Site();
        site.Engine.Reusable = false;
        site.Engine.Plan = Plan(IdNode.Instance);
        site.Engine.Command = _ =>
        {
            model.Title = site.Engine.Posted!["f0"];
            return ValueTask.CompletedTask;
        };

        var (links, title, marker, _) = await ChangeAsync(site, model, TestContext.Current.CancellationToken);

        Assert.Equal((1, "moved", 1), (links, title, marker));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-180")]
    public async Task APageAnswerKeepsThePagesOwnElementsAsync()
    {
        var model = new Model("start");
        var site = new Site();
        site.Engine.Reusable = false;
        site.Engine.Plan = Plan(IdNode.Instance);
        site.Engine.Command = _ =>
        {
            model.Title = site.Engine.Posted!["f0"];
            return ValueTask.CompletedTask;
        };

        var (_, title, _, kept) = await ChangeAsync(site, model, TestContext.Current.CancellationToken);

        Assert.Equal(("moved", true), (title, kept));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-173")]
    public async Task APlannedPostAnswerKeepsTheLayoutAsync()
    {
        var model = new Model("start");
        var site = new Site(fields: new FakeFields());
        site.Engine.Fields = true;
        site.Engine.Plan = Plan(new IdNode { Field = new SlotSpec("text", "Title") });

        var (links, title, marker, _) = await ChangeAsync(site, model, TestContext.Current.CancellationToken);

        Assert.Equal((1, "moved", 1), (links, title, marker));
        Assert.Null(site.Engine.Posted);
    }

    private static RenderPlan Plan(IdNode field) => new([], [
        new LiteralNode("<nav><a id=\"home\" href=\"/\">Home</a></nav>" + PageRegion.Open + "<p id=\"title\">"),
        new ValueNode(static model => ((Model)model!).Title),
        new LiteralNode("</p><input id=\"field\" data-change name=\"f"),
        field,
        new LiteralNode("\" value=\"\">" + PageRegion.Close),
    ], []);

    private static async Task<(int Links, string Title, int? Marker, bool Kept)> ChangeAsync(Site site, Model model, CancellationToken cancellationToken)
    {
        await using var app = await StartAsync(site, model);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var answered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Response += (_, response) =>
        {
            if (response.Request.Method == "POST")
            {
                answered.TrySetResult();
            }
        };

        await page.GotoAsync(Address(app) + "/inbox");
        await page.EvaluateAsync("() => { window.marker = 1; document.querySelector('#title').kept = true; }");
        await page.FillAsync("#field", "moved");
        await page.DispatchEventAsync("#field", "change");
        await answered.Task.WaitAsync(cancellationToken);
        await page.WaitForFunctionAsync("() => !document.querySelector('form').hasAttribute('aria-busy') && document.querySelector('#title')?.textContent === 'moved' || document.querySelectorAll('nav a').length === 0").WaitAsync(cancellationToken);
        return (await page.EvaluateAsync<int>("() => document.querySelectorAll('nav a').length"), await page.EvaluateAsync<string>("() => document.querySelector('#title')?.textContent ?? ''"), await page.EvaluateAsync<int?>("() => window.marker"), await page.EvaluateAsync<bool>("() => document.querySelector('#title')?.kept === true"));
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> StartAsync(Site site, Model model)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => model, [new LayoutDefinition("Pages/Layout.axaml", _ => model)])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
