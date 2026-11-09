using System.Collections.Immutable;
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

public sealed class DragScriptTests
{
    private const string Order = "id => [...document.querySelectorAll('#' + id + ' [data-drag]')].map(e => e.getAttribute('data-drag')).join(',')";

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DRAG-04")]
    public async Task DraggedItemsMoveAtOnceAndTheServerWinsAsync()
    {
        var lists = new Lists();
        var site = Site();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        site.Engine.Command = async _ =>
        {
            started.SetResult();
            await answer.Task;
            lists.Apply(site.Engine.Posted!["_btn"], serverIndex: 0);
        };
        await using var app = await StartAsync(site, lists);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();

        await page.GotoAsync(Address(app) + "/lists");
        await page.EvaluateAsync("() => { window.marker = 1; }");
        await DragAsync(page, "[data-drag=\"b\"]", "[data-drag=\"e\"]", above: true);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var optimistic = (await page.EvaluateAsync<string>(Order, "todo"), await page.EvaluateAsync<string>(Order, "done"));
        var locked = await page.EvaluateAsync<string>("() => ['todo', 'done'].map(id => document.getElementById(id).getAttribute('aria-busy')).join(',')");
        await DragAsync(page, "[data-drag=\"a\"]", "[data-drag=\"d\"]", above: true);
        var refused = await page.EvaluateAsync<string>(Order, "todo");
        answer.SetResult();
        await page.WaitForFunctionAsync("() => !document.querySelector('[aria-busy]') && document.querySelector('#done').textContent.startsWith('b')");
        var settled = (await page.EvaluateAsync<string>(Order, "todo"), await page.EvaluateAsync<string>(Order, "done"));

        Assert.Equal(("a,c", "d,b,e"), optimistic);
        Assert.Equal("true,true", locked);
        Assert.Equal("a,c", refused);
        Assert.Equal("1|1|b", site.Engine.Posted!["_btn"]);
        Assert.Equal(("a,c", "b,d,e"), settled);
        Assert.Equal(1, await page.EvaluateAsync<int?>("() => window.marker"));
    }

    [Fact]
    [Trait("AC", "DRAG-05")]
    public async Task AFailedDropRollsTheMoveBackAsync()
    {
        var lists = new Lists();
        var site = Site();
        site.Engine.Outcome = new PostOutcome(PostStatus.BadRequest, null);
        await using var app = await StartAsync(site, lists);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        var posts = new List<int>();
        page.Response += (_, response) =>
        {
            if (response.Request.Method == "POST")
            {
                posts.Add(response.Status);
            }
        };

        await page.GotoAsync(Address(app) + "/lists");
        await page.EvaluateAsync("() => { window.marker = 1; }");
        await DragAsync(page, "[data-drag=\"b\"]", "[data-drag=\"e\"]", above: true);
        await page.WaitForFunctionAsync("() => document.querySelector('[aria-live]') && document.querySelector('[aria-live]').textContent.includes('failed')");
        var state = (await page.EvaluateAsync<string>(Order, "todo"), await page.EvaluateAsync<string>(Order, "done"));

        Assert.Equal(("a,b,c", "d,e"), state);
        Assert.Equal([400], posts);
        Assert.Null(await page.EvaluateAsync<string?>("() => document.querySelector('[aria-busy]') && 'busy'"));
        Assert.Equal(1, await page.EvaluateAsync<int?>("() => window.marker"));
    }

    [Fact]
    [Trait("AC", "DRAG-06")]
    public async Task TheKeyboardMovesItemsWithAnnouncementsAsync()
    {
        var lists = new Lists();
        var site = Site();
        var posted = new List<string>();
        site.Engine.Command = _ =>
        {
            posted.Add(site.Engine.Posted!["_btn"]);
            lists.Apply(site.Engine.Posted["_btn"]);
            return ValueTask.CompletedTask;
        };
        await using var app = await StartAsync(site, lists);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        var spoken = new List<string>();
        async Task PressAsync(string key)
        {
            await page.Keyboard.PressAsync(key);
            spoken.Add(await page.EvaluateAsync<string>("() => document.querySelector('[aria-live]').textContent"));
        }

        await page.GotoAsync(Address(app) + "/lists");
        await page.FocusAsync("[data-drag=\"a\"]");
        await PressAsync("Space");
        await PressAsync("ArrowDown");
        var down = await page.EvaluateAsync<string>(Order, "todo");
        await PressAsync("ArrowRight");
        await PressAsync("Space");
        await page.WaitForFunctionAsync("() => !document.querySelector('[aria-busy]') && document.querySelector('#done').textContent.includes('a')");
        await page.FocusAsync("[data-drag=\"c\"]");
        await PressAsync("Space");
        await PressAsync("ArrowUp");
        var lifted = await page.EvaluateAsync<string>(Order, "todo");
        await PressAsync("Escape");
        var restored = await page.EvaluateAsync<string>(Order, "todo");

        Assert.Equal("b,a,c", down);
        Assert.Equal(["1|1|a"], posted);
        Assert.Equal(("d,a,e", "b,c"), (await page.EvaluateAsync<string>(Order, "done"), await page.EvaluateAsync<string>(Order, "todo")));
        Assert.Equal("c,b", lifted);
        Assert.Equal("b,c", restored);
        Assert.Equal(
        [
            "Picked up a. Use the arrow keys to move, Space to drop, Escape to cancel.",
            "a, position 2 of 3 in Todo",
            "a, position 2 of 3 in Done",
            "Dropped a, position 2 of 3 in Done.",
            "Picked up c. Use the arrow keys to move, Space to drop, Escape to cancel.",
            "c, position 1 of 2 in Todo",
            "Movement cancelled. c is back in its place.",
        ], spoken);
    }

    [Fact]
    [Trait("AC", "DRAG-07")]
    public async Task WithoutJavaScriptMoveButtonsPostTheFormAsync()
    {
        var lists = new Lists();
        var site = Site();
        await using var app = await StartAsync(site, lists);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var page = await context.NewPageAsync();

        await page.GotoAsync(Address(app) + "/lists");
        var native = page.WaitForResponseAsync(response => response.Request.IsNavigationRequest && response.Request.Method == "POST");
        await page.ClickAsync("[data-drag=\"a\"] button:text('Move down')");
        var status = (await native).Status;

        Assert.Equal(200, status);
        Assert.Equal("0|down|a", site.Engine.Posted!["_btn"]);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DRAG-11")]
    public async Task HandlesInTableRowsMoveTheirRowsAsync()
    {
        var lists = new Lists();
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<div id=\"todo\" data-drop=\"0\" aria-label=\"Sections\"><table><tbody>"),
            new RepeatNode(scope => ((Lists)scope!).Todo, [
                new LiteralNode("<tr><td><div data-drag=\""), new ValueNode(static item => item), new LiteralNode("\" tabindex=\"0\" aria-roledescription=\"draggable\" aria-label=\"Drag to reorder\">::</div></td><td>"),
                new ValueNode(static item => item), new LiteralNode("</td></tr>"),
            ]),
            new LiteralNode("</tbody></table></div>"),
        ], []);
        var posted = new List<string>();
        site.Engine.Command = _ =>
        {
            posted.Add(site.Engine.Posted!["_btn"]);
            lists.Apply(site.Engine.Posted["_btn"]);
            return ValueTask.CompletedTask;
        };
        await using var app = await StartAsync(site, lists);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        await page.GotoAsync(Address(app) + "/lists");
        await page.FocusAsync("[data-drag=\"a\"]");
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("ArrowDown");
        var moved = await page.EvaluateAsync<string>(Order, "todo");
        await page.Keyboard.PressAsync("Space");
        var spoken = await page.EvaluateAsync<string>("() => document.querySelector('[aria-live]').textContent");
        await page.WaitForFunctionAsync("() => !document.querySelector('[aria-busy]')").WaitAsync(TestContext.Current.CancellationToken);
        var answered = await page.EvaluateAsync<string>("() => [...document.querySelectorAll('#todo tbody tr')].map((row) => row.cells[1].textContent).join()");

        Assert.Equal(("b,c,a", "Dropped Drag to reorder, position 3 of 3 in Sections."), (moved, spoken));
        Assert.Equal(["0|2|a"], posted);
        Assert.Equal("b,c,a", answered);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DRAG-12")]
    public async Task ALiftedCopyFollowsThePointerWhileTheOthersMakeRoomAsync()
    {
        var lists = new Lists();
        var site = Site();
        site.Engine.Command = _ =>
        {
            lists.Apply(site.Engine.Posted!["_btn"]);
            return ValueTask.CompletedTask;
        };
        await using var app = await StartAsync(site, lists);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var page = await browser.NewPageAsync();
        await page.AddInitScriptAsync("{ const animate = Element.prototype.animate; window.made = []; Element.prototype.animate = function (...args) { window.made.push(this); return animate.apply(this, args); }; }");
        await page.GotoAsync(Address(app) + "/lists");
        var source = (await page.Locator("[data-drag=\"a\"]").BoundingBoxAsync())!;
        var target = (await page.Locator("[data-drag=\"e\"]").BoundingBoxAsync())!;

        await page.Mouse.MoveAsync(source.X + 10, source.Y + source.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(source.X + 10, source.Y + source.Height / 2 + 30, new() { Steps = 3 });
        var moving = await page.EvaluateAsync<bool>("() => window.made.some(e => e.matches('#todo [data-drag]:not([data-dragging])'))");
        await page.Mouse.MoveAsync(target.X + 40, target.Y + 2, new() { Steps = 8 });
        var copy = await page.EvaluateAsync<string>("""
            () => { const o = document.querySelector('[data-aioros-drag-overlay]'); if (!o) return 'none'; const r = o.getBoundingClientRect();
              return [o.textContent.trim().startsWith('a'), !!o.closest('form'), o.querySelectorAll('[name],[id]').length + (o.hasAttribute('id') ? 1 : 0), getComputedStyle(o).position, getComputedStyle(o).pointerEvents].join(',') + '|' + Math.round(r.left) + ',' + Math.round(r.top); }
            """);
        var dimmed = await page.EvaluateAsync<string?>("() => document.querySelector('[data-dragging]')?.getAttribute('data-drag')");
        var posted = page.WaitForResponseAsync(static response => response.Request.Method == "POST");
        await page.Mouse.UpAsync();
        await posted.WaitAsync(TestContext.Current.CancellationToken);
        var move = site.Engine.Posted!["_btn"];
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-aioros-drag-overlay]')");
        await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await page.EvaluateAsync("() => { window.made = []; }");
        var still = (await page.Locator("[data-drag=\"b\"]").BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(still.X + 10, still.Y + still.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(still.X + 10, still.Y + still.Height / 2 + 40, new() { Steps = 4 });
        var calm = await page.EvaluateAsync<int>("() => window.made.length");
        await page.Mouse.UpAsync();

        Assert.True(moving);
        Assert.StartsWith("true,false,0,fixed,none|", copy, StringComparison.Ordinal);
        Assert.Equal("a", dimmed);
        Assert.Equal("1|1|a", move);
        Assert.Equal(0, calm);
    }

    private static Site Site()
    {
        var site = new Site();
        IPlanNode[] Items(Func<Lists, ImmutableList<string>> items, string token) =>
        [
            new RepeatNode(scope => items((Lists)scope!), [
                new LiteralNode("<li data-drag=\""), new ValueNode(static item => item), new LiteralNode("\" tabindex=\"0\" aria-roledescription=\"draggable\">"),
                new ValueNode(static item => item),
                new LiteralNode("<noscript><button type=\"submit\" name=\"_btn\" value=\"" + token + "|down|"), new ValueNode(static item => item), new LiteralNode("\">Move down</button></noscript></li>"),
            ]),
        ];
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<ul id=\"todo\" aria-label=\"Todo\" data-drop=\"0\" style=\"padding:20px\">"), .. Items(static model => model.Todo, "0"), new LiteralNode("</ul>"),
            new LiteralNode("<ul id=\"done\" aria-label=\"Done\" data-drop=\"1\" style=\"padding:20px\">"), .. Items(static model => model.Done, "1"), new LiteralNode("</ul>"),
        ], []);
        return site;
    }

    private static async Task DragAsync(IPage page, string from, string to, bool above)
    {
        var source = (await page.Locator(from).BoundingBoxAsync())!;
        var target = (await page.Locator(to).BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(source.X + source.Width / 2, source.Y + source.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(source.X + source.Width / 2, source.Y + source.Height / 2 + 8, new() { Steps = 2 });
        await page.Mouse.MoveAsync(target.X + target.Width / 2, target.Y + (above ? 2 : target.Height - 2), new() { Steps = 8 });
        await page.Mouse.UpAsync();
    }

    private static Task<IBrowser> LaunchAsync(IPlaywright playwright) =>
        playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> StartAsync(Site site, Lists lists)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/lists", "Pages/lists/Page.axaml", _ => lists, [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Lists : IPageModel
    {
        public string Title => "Lists";

        public ImmutableList<string> Todo { get; private set; } = ["a", "b", "c"];

        public ImmutableList<string> Done { get; private set; } = ["d", "e"];

        public void Apply(string posted, int? serverIndex = null)
        {
            var parts = posted.Split('|');
            var item = parts[2];
            var index = serverIndex ?? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            (Todo, Done) = (Todo.Remove(item), Done.Remove(item));
            (Todo, Done) = parts[0] == "0" ? (Todo.Insert(index, item), Done) : (Todo, Done.Insert(index, item));
        }
    }
}
