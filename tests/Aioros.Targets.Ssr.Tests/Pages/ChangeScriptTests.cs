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

public sealed class ChangeScriptTests
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-110")]
    public async Task AChangedSelectionIsPostedAndItsAnswerShownAsync()
    {
        var tabs = new Tabs();
        var site = new Site();
        var posted = new List<string>();
        site.Engine.Command = _ =>
        {
            posted.Add(string.Join("&", site.Engine.Posted!.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)));
            tabs.Active = int.Parse(site.Engine.Posted!["f0"], System.Globalization.CultureInfo.InvariantCulture);
            return ValueTask.CompletedTask;
        };
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<p id=\"active\">Active page is "), new ValueNode(static model => ((Tabs)model!).Active + 1), new LiteralNode("</p><div role=\"radiogroup\" data-change>"),
            new LiteralNode("<label><input type=\"radio\" name=\"f0\" value=\"0\""), new ConditionNode(static model => ((Tabs)model!).Active == 0, [new LiteralNode(" checked")]), new LiteralNode(">One</label>"),
            new LiteralNode("<label id=\"two\"><input type=\"radio\" name=\"f0\" value=\"1\""), new ConditionNode(static model => ((Tabs)model!).Active == 1, [new LiteralNode(" checked")]), new LiteralNode(">Two</label>"),
            new LiteralNode("</div><select name=\"f1\"><option value=\"0\">Plain</option><option value=\"1\">Other</option></select>"),
        ], []);
        await using var app = await StartAsync(site, tabs);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(Address(app) + "/tabs");
        await page.EvaluateAsync("() => { window.marker = 1; }");
        await page.SelectOptionAsync("select[name=f1]", "1");
        await page.ClickAsync("#two");
        await page.WaitForFunctionAsync("() => document.querySelector('#active').textContent === 'Active page is 2'").WaitAsync(TestContext.Current.CancellationToken);
        var scripted = (await page.EvaluateAsync<int?>("() => window.marker"), await page.IsCheckedAsync("input[value='1'][name=f0]"));
        var sent = posted.ToList();

        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var still = await context.NewPageAsync();
        var requests = 0;
        still.Request += (_, request) => requests += request.Method == "POST" ? 1 : 0;
        await still.GotoAsync(Address(app) + "/tabs");
        await still.ClickAsync("label:not(#two)");

        Assert.Equal(["_chg=0&f0=1&f1=1"], sent);
        Assert.Equal((1, true), scripted);
        Assert.Equal(0, requests);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> StartAsync(Site site, Tabs tabs)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/tabs", "Pages/tabs/Page.axaml", _ => tabs, [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Tabs : IPageModel
    {
        public string Title => "Tabs";

        public int Active { get; set; }
    }
}
