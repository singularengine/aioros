using System.Collections.Immutable;
using System.Globalization;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class BoundCarouselScriptTests
{
    [Fact]
    [Trait("AC", "ITEMS-124")]
    public async Task ABoundCarouselPostsEachMoveOnceAsync()
    {
        var model = new Slides();
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var site = new Site();
        site.Engine.Command = async _ =>
        {
            if (site.Engine.Posted?.TryGetValue("f0", out var posted) == true)
            {
                model.Index = int.Parse(posted, CultureInfo.InvariantCulture);
                if (posted == "2")
                {
                    await held.Task;
                }
            }
        };
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<style>.st{display:flex;overflow-x:auto;scroll-snap-type:x mandatory;width:200px;scrollbar-width:none}.cs{flex:0 0 100%;scroll-snap-align:start;height:50px}</style>"
                + "<div id=\"c\" data-aioros-carousel data-change role=\"region\" aria-roledescription=\"carousel\"><div id=\"s\" data-aioros-strip class=\"st\">"),
            .. Enumerable.Range(0, 4).SelectMany(Slide),
            new LiteralNode("</div><button type=\"button\" id=\"n\" data-aioros-slide=\"1\">N</button></div>"),
        ], []);
        await using var app = await HostAsync(site, model);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        var module = page.WaitForResponseAsync(response => response.Url.Contains(CarouselScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/slides");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("u => import(u).then(() => 0)", CarouselScript.Path);
        await page.EvaluateAsync("() => { window.ends = 0; document.getElementById('s').addEventListener('scrollend', () => window.ends++) }");
        var posts = new List<string>();
        page.Request += (_, request) =>
        {
            if (request.Method == "POST")
            {
                posts.Add(System.Web.HttpUtility.ParseQueryString(request.PostData ?? string.Empty)["f0"] ?? "-");
            }
        };
        async Task SettledAsync(int index, int ends)
        {
            await page.WaitForFunctionAsync("([i, e]) => window.ends >= e && Math.round(document.getElementById('s').scrollLeft) === i * 200 && !document.querySelector('form').hasAttribute('aria-busy')", new[] { index, ends }, new() { PollingInterval = 20 }).WaitAsync(TestContext.Current.CancellationToken);
        }

        const string Selected = "() => [...document.getElementById('s').children].findIndex(x => x.classList.contains('sel')) + ':' + document.querySelector('#s input').value";

        await page.ClickAsync("#n");
        await SettledAsync(1, 1);
        var once = (string.Join(',', posts), await page.EvaluateAsync<string>(Selected), model.Index);
        posts.Clear();
        await page.ClickAsync("#n");
        await page.ClickAsync("#n");
        await page.WaitForFunctionAsync("() => document.getElementById('s').scrollLeft > 410").WaitAsync(TestContext.Current.CancellationToken);
        held.SetResult();
        await SettledAsync(3, 2);
        var twice = (posts.LastOrDefault(), await page.EvaluateAsync<string>(Selected), model.Index, posts.Count);

        Assert.Equal(("1", "1:1", 1), once);
        Assert.Equal(("3", "3:3", 3, 2), twice);
    }

    private static IEnumerable<IPlanNode> Slide(int index) =>
    [
        new LiteralNode("<div class=\"cs"), new ConditionNode(model => ((Slides)model!).Index == index, [new LiteralNode(" sel")]), new LiteralNode("\">"),
        new ConditionNode(model => ((Slides)model!).Index == index, [new LiteralNode($"<input type=\"hidden\" name=\"f0\" value=\"{index}\">")]),
        new LiteralNode($"S{index}</div>"),
    ];

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> HostAsync(Site site, Slides model)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/slides", "Pages/slides/Page.axaml", _ => model, [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Slides : IPageModel
    {
        public string Title => "Slides";

        public int Index { get; set; }
    }
}
