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

public sealed class QueuedChangeScriptTests
{
    private static readonly RenderPlan Plan = new([], [
        new LiteralNode("<input id=\"end\" type=\"range\" min=\"0\" max=\"40\" data-change name=\"f"), IdNode.Instance, new LiteralNode("\" value=\""), new ValueNode(static model => ((Brush)model!).End),
        new LiteralNode("\">"),
    ], []);

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-191")]
    public async Task ChangesMadeWhileAPostIsInFlightAreKeptAndPostedOnceAfterItsAnswerAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var brush = new Brush();
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

            brush.End = int.Parse(form["f0"], CultureInfo.InvariantCulture);
        };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        await using var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/brush", "Pages/brush/Page.axaml", _ => brush, [])]);
        await app.StartAsync(cancellationToken);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First() + "/brush");
        await page.FocusAsync("#end");
        var first = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.Keyboard.PressAsync("ArrowLeft");
        await entered.Task.WaitAsync(cancellationToken);
        for (var press = 0; press < 4; press++)
        {
            await page.Keyboard.PressAsync("ArrowLeft");
        }

        gate.SetResult();
        await first.WaitAsync(cancellationToken);
        await page.WaitForFunctionAsync("() => !document.querySelector('form').hasAttribute('aria-busy')").WaitAsync(cancellationToken);
        var shown = await page.EvalOnSelectorAsync<string>("#end", "input => input.value");

        Assert.Equal((15, "15"), (brush.End, shown));
        Assert.Equal(["0=19", "0=15"], posted);
    }

    private sealed class Brush : IPageModel
    {
        public string Title => "Brush";

        public int End { get; set; } = 20;
    }
}
