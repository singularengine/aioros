using System.Buffers;
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

public sealed class PageStyleScriptTests
{
    private static readonly RenderPlan Plan = new([], [
        new LiteralNode("<div id=\"probe\"></div><div"), SlotRegionNode.Mark, new LiteralNode(">"), SlotRegionNode.Open, Chart(),
        new LiteralNode("<input id=\"from\" type=\"range\" min=\"0\" max=\"100\" name=\"f"), IdNode.Instance, new LiteralNode("\" data-change value=\""), new ValueNode(static model => ((Dial)model!).From),
        new LiteralNode("\">"), SlotRegionNode.Close, new LiteralNode("</div><div"), SlotRegionNode.Mark, new LiteralNode(">"), SlotRegionNode.Open, Chart(), SlotRegionNode.Close,
        new LiteralNode("</div><div"), SlotRegionNode.Mark, new LiteralNode(">"), SlotRegionNode.Open, Chart(), SlotRegionNode.Close, new LiteralNode("</div>"),
    ], []);

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-194")]
    public async Task ARegionAnswerCarriesAKeyedStyleOnlyWhenThePageLacksItAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var dial = new Dial();
        var site = new Site();
        site.Engine.Plan = Plan;
        site.Engine.Fields = true;
        site.Engine.Command = _ =>
        {
            dial.From = int.Parse(site.Engine.Posted!["f0"], CultureInfo.InvariantCulture);
            return ValueTask.CompletedTask;
        };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        await using var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/dial", "Pages/dial/Page.axaml", _ => dial, [])]);
        await app.StartAsync(cancellationToken);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var answers = new List<(string?, string?, bool)>();

        await page.GotoAsync(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First() + "/dial");
        var shown = new List<string> { await ShownAsync(page) };
        foreach (var value in (string[])["7", "3"])
        {
            var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
            await page.EvalOnSelectorAsync("#from", "(input, value) => { input.value = value; input.dispatchEvent(new Event('change', { bubbles: true })); }", value);
            var response = await answered.WaitAsync(cancellationToken);
            await page.WaitForFunctionAsync("() => !document.querySelector('form').hasAttribute('aria-busy')").WaitAsync(cancellationToken);
            answers.Add((await response.HeaderValueAsync("x-aioros-slot"), await response.Request.HeaderValueAsync("x-aioros-styles") is { } held ? held[..7] : null, (await response.TextAsync()).StartsWith("<style data-css data-aioros-style=\"sync-a:", StringComparison.Ordinal)));
            shown.Add(await ShownAsync(page));
        }

        Assert.Equal([("0", "sync-a:", false), ("0", "sync-a:", true)], answers);
        Assert.Equal(["20px,1,0", "20px,1,0", "10px,1,0"], shown);
    }

    private static Task<string> ShownAsync(IPage page) => page.EvaluateAsync<string>(
        "() => [getComputedStyle(document.querySelector('#probe')).width, document.head.querySelectorAll('style[data-aioros-style]').length, document.querySelectorAll('.aioros-body style').length].join(',')");

    private static WriterNode Chart() => new(static model => ((Dial)model!).From, static (ref SlotOutput output) =>
    {
        if (output.NeedsStyle("sync-a"))
        {
            output.WriteStyle("sync-a", "#probe{height:1px;width:" + ((int)output.Value! > 5 ? "20" : "10") + "px}");
        }

        output.Buffer.Write("<svg></svg>"u8);
    });

    private sealed class Dial : IPageModel
    {
        public string Title => "Dial";

        public int From { get; set; } = 10;
    }
}
