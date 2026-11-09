using System.Globalization;
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

public sealed class LiveValueScriptTests
{
    private static readonly RenderPlan Plan = new([], [
        new LiteralNode("<input id=\"a\" type=\"range\" min=\"0\" max=\"9\" data-change name=\"f"), IdNode.Instance, new LiteralNode("\" value=\""), new ValueNode(static model => ((Window)model!).A),
        new LiteralNode("\"><input id=\"b\" type=\"range\" min=\"0\" max=\"9\" data-change name=\"f"), IdNode.Instance, new LiteralNode("\" value=\""), new ValueNode(static model => ((Window)model!).B),
        new LiteralNode("\"><input id=\"note\" type=\"text\" name=\"f"), IdNode.Instance, new LiteralNode("\" value=\"\">"),
    ], []);

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-186")]
    public async Task AnAnswerSetsTheLiveValuesOfTheInputsItChangesAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var window = new Window();
        var site = new Site();
        site.Engine.Plan = Plan;
        site.Engine.Fields = true;
        site.Engine.Command = _ =>
        {
            var posted = site.Engine.Posted!;
            window.A = Math.Min(4, int.Parse(posted["f0"], CultureInfo.InvariantCulture));
            window.B = posted["_chg"] == "0" ? window.A - 2 : int.Parse(posted["f1"], CultureInfo.InvariantCulture);
            return ValueTask.CompletedTask;
        };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        await using var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => window, [])]);
        await app.StartAsync(cancellationToken);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var shown = new List<string>();

        await page.GotoAsync(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First() + "/inbox");
        await MoveAsync(page, "#b", "7", cancellationToken);
        shown.Add(await ValuesAsync(page));
        await page.FillAsync("#note", "draft");
        await page.FocusAsync("#note");
        await MoveAsync(page, "#a", "9", cancellationToken);
        shown.Add(await ValuesAsync(page));

        Assert.Equal(["0,7,", "4,2,draft"], shown);
    }

    private static Task<string> ValuesAsync(IPage page) =>
        page.EvaluateAsync<string>("() => ['#a', '#b', '#note'].map(s => document.querySelector(s).value).join(',')");

    private static async Task MoveAsync(IPage page, string selector, string value, CancellationToken cancellationToken)
    {
        var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.EvalOnSelectorAsync(selector, "(input, value) => { input.value = value; input.dispatchEvent(new Event('change', { bubbles: true })); }", value);
        await answered.WaitAsync(cancellationToken);
        await page.WaitForFunctionAsync("() => !document.querySelector('form').hasAttribute('aria-busy')").WaitAsync(cancellationToken);
    }

    private sealed class Window : IPageModel
    {
        public string Title => "Window";

        public int A { get; set; }

        public int B { get; set; }
    }
}
