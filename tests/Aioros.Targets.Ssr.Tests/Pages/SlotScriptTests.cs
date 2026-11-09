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

public sealed class SlotScriptTests
{
    private static readonly RenderPlan Plan = new([], [
        new LiteralNode("<p id=\"outside\">Around</p><div"), SlotRegionNode.Mark, new LiteralNode(">"), SlotRegionNode.Open, new LiteralNode("<svg id=\"chart\" data-from=\""), new ValueNode(static model => ((Dial)model!).From),
        new LiteralNode("\"></svg><input id=\"from\" type=\"range\" min=\"0\" max=\"100\" name=\"f"), IdNode.Instance, new LiteralNode("\" data-change value=\""), new ValueNode(static model => ((Dial)model!).From),
        new LiteralNode("\"><button id=\"area\" type=\"submit\" name=\"f"), IdNode.Instance, new LiteralNode("\" value=\"area\" data-change>Area</button>"), SlotRegionNode.Close, new LiteralNode("</div>"),
    ], []);

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-169")]
    public async Task TheScriptShowsASlotsRegionInPlaceAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var dial = new Dial();
        var site = new Site();
        var posted = new List<string>();
        site.Engine.Plan = Plan;
        site.Engine.Fields = true;
        site.Engine.Command = _ =>
        {
            var form = site.Engine.Posted!;
            posted.Add(string.Join("&", form.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)));
            dial.From = form.TryGetValue("f0", out var from) ? int.Parse(from, CultureInfo.InvariantCulture) : dial.From;
            return ValueTask.CompletedTask;
        };
        await using var app = await StartAsync(site, dial, cancellationToken);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var slots = new List<string?>();

        await page.GotoAsync(Address(app) + "/inbox");
        await page.EvaluateAsync("() => { window.marker = 1; document.querySelector('#outside').dataset.mark = 'kept'; document.querySelector('#chart').kept = 'kept'; const input = document.querySelector('#from'); input.kept = 'kept'; input.focus(); }");
        var kept = new List<string>();
        foreach (var value in (string[])["42", "7", "3"])
        {
            var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
            await page.EvalOnSelectorAsync("#from", "(input, value) => { input.value = value; input.dispatchEvent(new Event('change', { bubbles: true })); }", value);
            slots.Add(await (await answered.WaitAsync(cancellationToken)).HeaderValueAsync("x-aioros-slot"));
            await page.WaitForFunctionAsync($"() => document.querySelector('#chart').dataset.from === '{value}' && !document.querySelector('form').hasAttribute('aria-busy')").WaitAsync(cancellationToken);
            kept.Add(await page.EvaluateAsync<string>("() => [document.querySelector('#chart').kept, document.querySelector('#from').kept, document.activeElement && document.activeElement.id].join(',')"));
        }

        var shown = (await page.EvaluateAsync<string>("() => document.querySelector('#outside').dataset.mark"), await page.EvaluateAsync<int?>("() => window.marker"));
        var chosen = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#area");
        await chosen.WaitAsync(cancellationToken);

        Assert.Equal(["0", "0", "0"], slots);
        Assert.Equal(("kept", 1), shown);
        Assert.Equal(["kept,kept,from", "kept,kept,from", "kept,kept,from"], kept);
        Assert.Equal("_chg=1&f0=3&f1=area", posted[^1]);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-182")]
    public async Task TheScriptDropsItsSlotHashesWhenItPatchesThePageWithoutNewOnesAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<div data-aioros-view=\"0\">View</div>"), .. Plan.Body], []);
        site.Engine.Fields = true;
        await using var app = await StartAsync(site, new Dial(), cancellationToken, live: true);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        using var http = new HttpClient();
        var document = await http.GetStringAsync(new Uri(Address(app) + "/inbox"), cancellationToken);
        const string Open = "<div class=\"aioros-body\" style=\"display:contents\">";
        var start = document.IndexOf(Open, StringComparison.Ordinal) + Open.Length;
        var body = document[start..document.IndexOf("</div></form>", start, StringComparison.Ordinal)].Replace("Around", "Pushed", StringComparison.Ordinal);
        var held = new List<bool>();
        foreach (var events in (string[])[$"data: {body}\n\n", "event: views\ndata: <div data-aioros-view=\"0\">Pushed</div>\n\n", string.Empty])
        {
            var page = await browser.NewPageAsync();
            var posts = new List<string?>();
            await page.RouteAsync("**/inbox/live**", route => events.Length == 0 ? route.AbortAsync() : route.FulfillAsync(new() { Status = 200, ContentType = "text/event-stream", Body = events }));
            await page.RouteAsync("**/inbox", route => PostAsync(route, posts, answersView: events.Length == 0));
            await page.GotoAsync(Address(app) + "/inbox");
            if (events.Length == 0)
            {
                await ChangeAsync(page, "42", cancellationToken);
            }

            await page.WaitForFunctionAsync("() => document.body.textContent.includes('Pushed')").WaitAsync(cancellationToken);
            await ChangeAsync(page, "7", cancellationToken);
            held.Add(posts[^1] is not null);
            await page.CloseAsync();
        }

        Assert.Equal([false, false, false], held);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-185")]
    public async Task ContentCannotImitateASlotRegionAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var dial = new Dial();
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new ValueNode(static _ => "<div data-aioros-slot=\"\"><b id=\"fake\">Fake</b></div>", Raw: true),
            .. Plan.Body,
            new ValueNode(static _ => "<!--s--><i>Imitation</i><!--/s-->", Raw: true),
        ], []);
        site.Engine.Fields = true;
        site.Engine.Command = _ =>
        {
            dial.From = int.Parse(site.Engine.Posted!["f0"], CultureInfo.InvariantCulture);
            return ValueTask.CompletedTask;
        };
        await using var app = await StartAsync(site, dial, cancellationToken);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(Address(app) + "/inbox");
        await page.EvaluateAsync("() => { window.marker = 1; }");
        var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.EvalOnSelectorAsync("#from", "input => { input.value = '42'; input.dispatchEvent(new Event('change', { bubbles: true })); }");
        var slot = await (await answered.WaitAsync(cancellationToken)).HeaderValueAsync("x-aioros-slot");
        await page.WaitForFunctionAsync("() => !document.querySelector('form').hasAttribute('aria-busy')").WaitAsync(cancellationToken);

        Assert.Equal("0", slot);
        Assert.Equal(("42", "Fake", 1), (await page.GetAttributeAsync("#chart", "data-from"), await page.TextContentAsync("#fake"), await page.EvaluateAsync<int?>("() => window.marker")));
    }

    /// <summary>Notes the hashes a post carries; the first one is answered with a view's region when asked, as a view's command would be.</summary>
    private static Task PostAsync(IRoute route, List<string?> posts, bool answersView)
    {
        if (route.Request.Method != "POST")
        {
            return route.ContinueAsync();
        }

        posts.Add(route.Request.Headers.TryGetValue("x-aioros-slots", out var hashes) ? hashes : null);
        return posts.Count == 1 && answersView
            ? route.FulfillAsync(new() { Status = 200, ContentType = "text/html", Headers = new Dictionary<string, string> { ["X-Aioros-View"] = "0" }, Body = "<div data-aioros-view=\"0\">Pushed</div>" })
            : route.ContinueAsync();
    }

    private static async Task ChangeAsync(IPage page, string value, CancellationToken cancellationToken)
    {
        var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.EvalOnSelectorAsync("#from", "(input, value) => { input.value = value; input.dispatchEvent(new Event('change', { bubbles: true })); }", value);
        await answered.WaitAsync(cancellationToken);
        await page.WaitForFunctionAsync("() => !document.querySelector('form').hasAttribute('aria-busy')").WaitAsync(cancellationToken);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> StartAsync(Site site, Dial dial, CancellationToken cancellationToken, bool live = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => dial, [], Live: live)]);
        await app.StartAsync(cancellationToken);
        return app;
    }

    private sealed class Dial : IPageModel
    {
        public string Title => "Dial";

        public int From { get; set; } = 10;
    }
}
