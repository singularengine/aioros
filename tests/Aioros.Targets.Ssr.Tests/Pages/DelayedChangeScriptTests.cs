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

public sealed class DelayedChangeScriptTests
{
    private static readonly RenderPlan Plan = new([], [
        new LiteralNode("<input id=\"filter\" type=\"text\" data-change data-delay=\"300\" name=\"f"), IdNode.Instance, new LiteralNode("\" value=\""), new ValueNode(static model => ((Search)model!).Filter),
        new LiteralNode("\"><p id=\"shown\">"), new ValueNode(static model => ((Search)model!).Filter), new LiteralNode("</p><button id=\"away\" type=\"button\">Away</button>"),
    ], []);

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-199")]
    public async Task ATextFieldWithADelayIsPostedOnceTheVisitorPausesAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var search = new Search();
        var site = new Site();
        var posted = new List<string>();
        var entered = new TaskCompletionSource();
        var gate = new TaskCompletionSource();
        site.Engine.Plan = Plan;
        site.Engine.Fields = true;
        site.Engine.Command = async _ =>
        {
            var form = site.Engine.Posted!;
            posted.Add(form["_chg"] + "=" + form["f0"]);
            if (posted.Count == 1)
            {
                entered.SetResult();
                await gate.Task;
            }

            search.Filter = form["f0"];
        };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        await using var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/search", "Pages/search/Page.axaml", _ => search, [])]);
        await app.StartAsync(cancellationToken);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        await page.Clock.InstallAsync(new() { TimeDate = DateTime.UnixEpoch });
        await page.Clock.PauseAtAsync(DateTime.UnixEpoch.AddMilliseconds(1024));

        await page.GotoAsync(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First() + "/search");
        await page.FocusAsync("#filter");
        foreach (var key in (string[])["a", "b"])
        {
            await page.Keyboard.TypeAsync(key);
            await page.Clock.RunForAsync(100);
        }

        await page.Keyboard.TypeAsync("c");
        await page.Clock.RunForAsync(299);
        var early = posted.Count + (await page.EvaluateAsync<bool>("() => document.querySelector('form').hasAttribute('aria-busy')") ? 1 : 0);
        var first = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.Clock.RunForAsync(1);
        await entered.Task.WaitAsync(cancellationToken);
        await page.Keyboard.TypeAsync("d");
        gate.SetResult();
        await first.WaitAsync(cancellationToken);
        await page.WaitForFunctionAsync("() => !document.querySelector('form').hasAttribute('aria-busy') && document.querySelector('#shown').textContent === 'abc'").WaitAsync(cancellationToken);
        var kept = await page.EvalOnSelectorAsync<string>("#filter", "input => input.value");
        var second = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.Clock.RunForAsync(300);
        await second.WaitAsync(cancellationToken);
        await page.WaitForFunctionAsync("() => !document.querySelector('form').hasAttribute('aria-busy') && document.querySelector('#shown').textContent === 'abcd'").WaitAsync(cancellationToken);
        await page.ClickAsync("#away");
        await page.Clock.RunForAsync(1000);
        var after = await page.EvaluateAsync<bool>("() => document.querySelector('form').hasAttribute('aria-busy')");

        Assert.Equal(0, early);
        Assert.Equal("abcd", kept);
        Assert.Equal(["0=abc", "0=abcd"], posted);
        Assert.False(after);
        Assert.Equal("abcd", search.Filter);
    }

    private sealed class Search : IPageModel
    {
        public string Title => "Search";

        public string Filter { get; set; } = string.Empty;
    }
}
