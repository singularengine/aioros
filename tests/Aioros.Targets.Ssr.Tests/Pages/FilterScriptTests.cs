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

public sealed class FilterScriptTests
{
    private const string Menu = """
        <div id="menu"><input type="text" name="f0" data-aioros-filter role="combobox" aria-autocomplete="list" aria-expanded="true" value="">
        <div data-aioros-empty hidden>No results found.</div>
        <div role="listbox">
          <div role="group"><div>Suggestions</div><div>
            <button type="submit" name="_btn" value="0|" tabindex="-1" role="option">Calendar</button>
            <button type="submit" name="_btn" value="1|" tabindex="-1" role="option"><span>Search</span> <span>Emoji</span></button>
            <button type="submit" name="_btn" value="2|" tabindex="-1" role="option" disabled>Calculator</button>
          </div></div>
          <hr>
          <div role="group"><div>Settings</div><div>
            <button type="submit" name="_btn" value="3|" tabindex="-1" role="option" data-value="Profile">Profile <span>P</span></button>
            <button type="submit" name="_btn" value="4|" tabindex="-1" role="option"><kbd>B</kbd>Billing</button>
            <button type="submit" name="_btn" value="5|" tabindex="-1" role="option">Settings</button>
          </div></div>
        </div></div>
        """;

    private const string Shown = """
        () => [...document.querySelectorAll('[role=option]')].filter(o => !o.hidden).map(o => (o.getAttribute('aria-selected') === 'true' ? '*' : '') + o.textContent.trim().split(/\s+/)[0]).join()
          + '|' + [...document.querySelectorAll('[role=group]')].filter(g => !g.hidden).map(g => g.firstElementChild.textContent).join()
          + '|' + (document.querySelector('hr').hidden ? 'h' : 's') + (document.querySelector('[data-aioros-empty]').hidden ? '' : 'E')
        """;

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ITEMS-111")]
    public async Task TypingFiltersRanksAndSelectsAsCmdkDoesAsync()
    {
        var site = new Site();
        var posted = new List<string>();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Menu)], []);
        site.Engine.Command = _ =>
        {
            posted.Add(site.Engine.Posted!["_btn"]);
            site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Plan = new RenderPlan([], [new LiteralNode(Menu + "<p id=\"done\"></p>")], []) });
            return ValueTask.CompletedTask;
        };
        await using var app = await StartAsync(site, "/menu");
        await using var plain = await StartAsync(new Site(), "/plain");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var bound = TestContext.Current.CancellationToken;
        var requested = new List<string>();
        page.Request += (_, request) => requested.Add(request.Url);
        async Task<string> TypeAsync(string text)
        {
            await page.FillAsync("input", text);
            return await page.EvaluateAsync<string>(Shown);
        }

        async Task<string> PressAsync(string key)
        {
            await page.Keyboard.PressAsync(key);
            return await page.EvaluateAsync<string>("() => document.querySelector('[aria-selected=true]')?.textContent.trim().split(/\\s+/)[0]");
        }

        await page.GotoAsync(Address(plain) + "/plain");
        var withoutList = requested.Any(url => url.Contains(FilterScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/menu");
        await page.WaitForFunctionAsync("() => !!document.querySelector('[aria-selected=true]')").WaitAsync(bound);
        var named = await page.EvaluateAsync<bool>("() => { const i = document.querySelector('input'); return document.getElementById(i.getAttribute('aria-activedescendant')).textContent === 'Calendar' && document.getElementById(i.getAttribute('aria-controls')).getAttribute('role') === 'listbox' }");
        var start = await page.EvaluateAsync<string>(Shown);
        var ranked = await TypeAsync("e");
        var grouped = await TypeAsync("set");
        var shortcut = await TypeAsync("gb");
        var empty = await TypeAsync("zz");
        var cleared = await TypeAsync(string.Empty);
        await page.FocusAsync("input");
        string[] keys = ["ArrowDown", "ArrowDown", "End", "ArrowDown", "Home", "ArrowUp", "Control+KeyN", "Control+KeyJ", "Control+KeyP", "Control+KeyK", "Alt+ArrowDown", "Alt+ArrowUp", "Meta+ArrowDown", "Meta+ArrowUp"];
        var moves = new List<string>();
        foreach (var key in keys)
        {
            moves.Add(await PressAsync(key));
        }

        await page.HoverAsync("text=Billing");
        var hovered = await page.EvaluateAsync<string>("() => document.querySelector('[aria-selected=true]').textContent");
        await TypeAsync("e");
        var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.FocusAsync("input");
        await page.Keyboard.PressAsync("Enter");
        await answered;
        await page.WaitForFunctionAsync("() => !!document.getElementById('done') && !!document.querySelector('[aria-selected=true]')").WaitAsync(bound);
        var kept = await page.EvaluateAsync<string>(Shown);
        var restored = await TypeAsync(string.Empty);

        Assert.False(withoutList);
        Assert.True(named);
        Assert.Equal("*Calendar,Search,Calculator,Profile,BBilling,Settings|Suggestions,Settings|s", start);
        Assert.Equal("*Search,Calendar,Profile,Settings|Suggestions,Settings|h", ranked);
        Assert.Equal("*Settings|Settings|h", grouped);
        Assert.Equal("*BBilling|Settings|h", shortcut);
        Assert.Equal("||hE", empty);
        Assert.Equal(start, cleared);
        Assert.Equal(["Search", "Profile", "Settings", "Settings", "Calendar", "Calendar", "Search", "Profile", "Search", "Calendar", "Profile", "Calendar", "Settings", "Calendar"], moves);
        Assert.Equal("BBilling", hovered);
        Assert.Equal(["1|"], posted);
        Assert.Equal(ranked, kept);
        Assert.Equal(start, restored);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ITEMS-125")]
    public async Task SelectingAnOptionScrollsOnlyItsListAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<div style=\"height:1200px\">Top</div>" + Menu.Replace("<div role=\"listbox\">", "<div role=\"listbox\" style=\"max-height:72px;overflow:auto\">", StringComparison.Ordinal) + "<div style=\"height:1200px\"></div>")], []);
        await using var app = await StartAsync(site, "/menu");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        await page.GotoAsync(Address(app) + "/menu");
        await page.WaitForFunctionAsync("() => !!document.querySelector('[aria-selected=true]')").WaitAsync(TestContext.Current.CancellationToken);
        var loaded = await page.EvaluateAsync<double>("() => scrollY");
        await page.FocusAsync("input");
        var focused = await page.EvaluateAsync<double>("() => scrollY");
        foreach (var key in new[] { "ArrowDown", "ArrowDown", "ArrowDown", "ArrowDown" })
        {
            await page.Keyboard.PressAsync(key);
        }

        const string Kept = "() => { const l = document.querySelector('[role=listbox]'), o = document.querySelector('[aria-selected=true]').getBoundingClientRect(), b = l.getBoundingClientRect(); return [l.scrollTop > 0, o.top >= b.top - 1 && o.bottom <= b.bottom + 1, scrollY].join() }";
        var moved = await page.EvaluateAsync<string>(Kept);

        Assert.Equal(0, loaded);
        Assert.Equal($"true,true,{focused}", moved);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> StartAsync(Site site, string route)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition(route, "Pages" + route + "/Page.axaml", _ => new Menued(), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Menued : IPageModel
    {
        public string Title => "Menu";
    }
}
