using System.Collections.Concurrent;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Themes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class ThemeScriptTests
{
    private const string Scheme = "() => getComputedStyle(document.documentElement).colorScheme";

    private const string CountFetches = """
        window.fetches = 0;
        const original = window.fetch;
        window.fetch = (url, init) => { if (init && init.headers && init.headers['X-Aioros-Navigation']) window.fetches++; return original(url, init); };
        """;

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-174")]
    public async Task AThemeLinkSwitchesInPlaceAsync()
    {
        var site = Site();
        var seen = new ConcurrentQueue<(string Path, bool Navigation, bool Plan, bool Layout)>();
        await using var app = await StartAsync(site, seen, stripHint: false);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var template = page.WaitForResponseAsync(response => response.Url.Contains("/_aioros/plan/", StringComparison.Ordinal));

        await page.GotoAsync(Address(app) + "/themes");
        await template;
        await page.EvaluateAsync("() => { window.marker = 1; document.getElementById('kept').marked = 1; }");
        var before = await page.EvaluateAsync<string>(Scheme);
        await page.HoverAsync("#dark");
        await page.HoverAsync("#kept");
        var hovered = seen.Count(request => request.Path.StartsWith("/theme/", StringComparison.Ordinal));
        await page.ClickAsync("#dark");
        await page.WaitForFunctionAsync("() => getComputedStyle(document.documentElement).colorScheme === 'dark'").WaitAsync(TestContext.Current.CancellationToken);
        var kept = (await page.EvaluateAsync<int?>("() => window.marker"), await page.EvaluateAsync<int?>("() => document.getElementById('kept').marked"), await page.EvaluateAsync<string>("() => location.pathname"));
        var cookie = (await context.CookiesAsync()).Single(cookie => cookie.Name == ThemeSwitch.Cookie).Value;

        Assert.Equal("light", before);
        Assert.Equal(0, hovered);
        Assert.Equal((1, 1, "/themes"), kept);
        Assert.Equal("dark", cookie);
        Assert.Contains(("/theme/dark", true, false, false), seen);
    }

    [Fact]
    [Trait("AC", "SSR-175")]
    public async Task WithoutJavaScriptAThemeLinkReloadsThePageAsync()
    {
        var site = Site();
        await using var app = await StartAsync(site, new ConcurrentQueue<(string, bool, bool, bool)>(), stripHint: false);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var page = await context.NewPageAsync();

        await page.GotoAsync(Address(app) + "/themes");
        var reloaded = page.WaitForResponseAsync(response => response.Request.IsNavigationRequest && response.Url.EndsWith("/themes", StringComparison.Ordinal));
        await page.ClickAsync("#dark");
        var status = (await reloaded).Status;

        Assert.Equal(200, status);
        Assert.Contains(":root{color-scheme:dark}", await page.ContentAsync(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-177")]
    public async Task TheScriptFixesTheSystemThemeWithoutAHintAsync()
    {
        var site = Site();
        await using var app = await StartAsync(site, new ConcurrentQueue<(string, bool, bool, bool)>(), stripHint: true);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);
        var context = await browser.NewContextAsync(new() { ColorScheme = ColorScheme.Dark });
        await context.AddCookiesAsync([new Cookie { Name = ThemeSwitch.Cookie, Value = ThemeSwitch.SystemPreference, Url = Address(app) }]);
        await context.AddInitScriptAsync(CountFetches);
        var page = await context.NewPageAsync();

        await page.GotoAsync(Address(app) + "/themes");
        await page.EvaluateAsync("() => { window.marker = 1; }");
        await page.WaitForFunctionAsync("() => getComputedStyle(document.documentElement).colorScheme === 'dark'").WaitAsync(TestContext.Current.CancellationToken);
        var dark = (await page.EvaluateAsync<int>("() => window.fetches"), await page.EvaluateAsync<int?>("() => window.marker"));
        var stored = (await context.CookiesAsync()).Single(cookie => cookie.Name == "theme-system");
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light });
        await page.WaitForFunctionAsync("() => getComputedStyle(document.documentElement).colorScheme === 'light'").WaitAsync(TestContext.Current.CancellationToken);
        var light = (await page.EvaluateAsync<int>("() => window.fetches"), await page.EvaluateAsync<int?>("() => window.marker"));

        Assert.Equal((1, 1), dark);
        Assert.Equal(("dark", "/", SameSiteAttribute.Lax), (stored.Value, stored.Path, stored.SameSite));
        Assert.InRange(stored.Expires - DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 364 * 86400, 366 * 86400);
        Assert.Equal((2, 1), light);
        Assert.Equal("light", (await context.CookiesAsync()).Single(cookie => cookie.Name == "theme-system").Value);
    }

    [Fact]
    [Trait("AC", "SSR-178")]
    public async Task TheScriptLeavesAMatchingThemeAloneAsync()
    {
        var site = Site();
        await using var app = await StartAsync(site, new ConcurrentQueue<(string, bool, bool, bool)>(), stripHint: true);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright);

        async Task<(int Fetches, string Scheme, string Stored)> VisitAsync(string theme, string? stored)
        {
            var context = await browser.NewContextAsync(new() { ColorScheme = ColorScheme.Dark });
            await context.AddCookiesAsync([
                new Cookie { Name = ThemeSwitch.Cookie, Value = theme, Url = Address(app) },
                .. stored is null ? Array.Empty<Cookie>() : [new Cookie { Name = "theme-system", Value = stored, Url = Address(app) }],
            ]);
            await context.AddInitScriptAsync(CountFetches);
            var page = await context.NewPageAsync();
            await page.GotoAsync(Address(app) + "/themes");
            return (await page.EvaluateAsync<int>("() => window.fetches"), await page.EvaluateAsync<string>(Scheme), (await context.CookiesAsync()).Single(cookie => cookie.Name == "theme-system").Value);
        }

        var following = await VisitAsync(ThemeSwitch.SystemPreference, "dark");
        var chosen = await VisitAsync(ThemeSwitch.Light, null);

        Assert.Equal((0, "dark", "dark"), following);
        Assert.Equal((0, "light", "dark"), chosen);
    }

    private static Site Site()
    {
        var site = new Site();
        site.Engine.Themed = true;
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<a id=\"dark\" href=\"/theme/dark\">Dark</a><div id=\"kept\">kept</div>")], []);
        return site;
    }

    private static Task<IBrowser> LaunchAsync(IPlaywright playwright) =>
        playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> StartAsync(Site site, ConcurrentQueue<(string Path, bool Navigation, bool Plan, bool Layout)> seen, bool stripHint)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.Use((http, next) =>
        {
            var headers = http.Request.Headers;
            seen.Enqueue((http.Request.Path.Value ?? "/", headers.ContainsKey("X-Aioros-Navigation"), headers.ContainsKey("X-Aioros-Plan"), headers.ContainsKey("X-Aioros-Layout")));
            if (stripHint)
            {
                headers.Remove(ThemeSwitch.Hint);
            }

            return next(http);
        });
        app.MapAiorosPages([new PageDefinition("/themes", "Pages/themes/Page.axaml", _ => new Model("Themes"), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
