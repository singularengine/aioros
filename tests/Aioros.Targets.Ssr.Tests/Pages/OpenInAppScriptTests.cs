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

public sealed class OpenInAppScriptTests
{
    private const string Link = "shop://open?path=%2Forders%2F42";

    private const string StandInLink = "shop://open?path=%2Ffiles";

    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact(Timeout = 60_000)]
    [Trait("AC", "INSTALL-09")]
    public async Task OpenInAppTriesTheSchemeAndFallsBackToTheLinkAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        await using var app = await StartAsync("shop");
        await using var plain = await StartAsync(null);
        using var http = new HttpClient();
        var withScheme = await http.GetStringAsync(new Uri(Address(app) + "/orders/42"), bound);
        var withoutScheme = await http.GetStringAsync(new Uri(Address(plain) + "/orders/42"), bound);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });

        var page = await browser.NewPageAsync();
        var (launched, _) = await NavigationsAsync(page, handler: null);
        await PauseClockAsync(page);
        await page.GotoAsync(Address(app) + "/orders/42");
        await page.ClickAsync("a[data-open-app]");
        await page.Clock.RunForAsync(1_400);
        var waiting = new Uri(page.Url).AbsolutePath;
        var fallback = page.WaitForURLAsync("**/download?return=*");
        await page.Clock.RunForAsync(200);
        await fallback;
        var fellBack = new Uri(page.Url).PathAndQuery;

        var handled = await browser.NewPageAsync();
        var taken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (handledLaunches, session) = await NavigationsAsync(handled, handler: async () =>
        {
            await handled.EvaluateAsync("() => dispatchEvent(new Event('blur'))");
            taken.TrySetResult();
        });
        await PauseClockAsync(handled);
        await handled.GotoAsync(Address(app) + "/orders/42");
        await handled.ClickAsync("a[data-open-app]");
        await taken.Task.WaitAsync(bound);
        await handled.Clock.RunForAsync(3_000);
        await session.SendAsync("Runtime.evaluate", new Dictionary<string, object> { ["expression"] = "0" });
        var stayed = new Uri(handled.Url).AbsolutePath;

        var standIn = await browser.NewPageAsync();
        var (standInLaunches, _) = await NavigationsAsync(standIn, handler: null);
        await PauseClockAsync(standIn);
        await standIn.GotoAsync(Address(app) + "/files");
        await standIn.ClickAsync("a[data-open-app]");
        var standInFallback = standIn.WaitForURLAsync("**/download?return=*");
        await standIn.Clock.RunForAsync(1_600);
        await standInFallback;

        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var scriptless = await context.NewPageAsync();
        await scriptless.GotoAsync(Address(app) + "/orders/42");
        await scriptless.ClickAsync("a[data-open-app]");
        await scriptless.WaitForURLAsync("**/download");

        Assert.Contains("<meta name=\"aioros-app\" content=\"shop\">", withScheme, StringComparison.Ordinal);
        Assert.DoesNotContain("<meta name=\"aioros-app\"", withoutScheme, StringComparison.Ordinal);
        Assert.Equal([Link, Address(app) + "/download?return=%2Forders%2F42"], launched);
        Assert.Equal("/orders/42", waiting);
        Assert.Equal("/download?return=%2Forders%2F42", fellBack);
        Assert.Equal([Link], handledLaunches);
        Assert.Equal("/orders/42", stayed);
        Assert.Equal("/download", new Uri(scriptless.Url).AbsolutePath);
        Assert.Equal([StandInLink, Address(app) + "/download?return=%2Ffiles"], standInLaunches);
    }

    /// <summary>
    /// The URLs the page's script asks the browser to open, read from Chromium's <c>Page.frameRequestedNavigation</c>
    /// (events on a session arrive before the answer to a later command on it, so a command is a barrier); a fake
    /// handler answers each scheme URL as the operating system does when an app takes the link.
    /// </summary>
    private static async Task<(List<string> Urls, ICDPSession Session)> NavigationsAsync(IPage page, Func<Task>? handler)
    {
        var launches = new List<string>();
        var session = await page.Context.NewCDPSessionAsync(page);
        await session.SendAsync("Page.enable");
        session.Event("Page.frameRequestedNavigation").OnEvent += (sender, navigation) =>
        {
            var url = navigation?.GetProperty("url").GetString() ?? string.Empty;
            launches.Add(url);
            if (handler is not null && url.StartsWith("shop:", StringComparison.Ordinal))
            {
                _ = handler();
            }
        };
        return (launches, session);
    }

    /// <summary>
    /// Installs Playwright's fake clock and pauses it: an installed clock still flows with real time, so on a loaded
    /// machine the script's fallback timer could fire before the test listens for it; a paused clock fires timers only
    /// when the test runs it forward.
    /// </summary>
    private static async Task PauseClockAsync(IPage page)
    {
        await page.Clock.InstallAsync(new() { TimeDate = Epoch });
        await page.Clock.PauseAtAsync(Epoch.AddSeconds(1));
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> StartAsync(string? scheme)
    {
        var site = new Site(new SsrOptions { AppScheme = scheme });
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<a href=\"/download\" data-open-app>Open in the desktop app</a>")], []);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([
            new PageDefinition("/orders/{id}", "Pages/orders/[id]/Page.axaml", static _ => null, []),
            new PageDefinition("/download", "Pages/download/Page.axaml", static _ => null, []),
            new PageDefinition("/files", "Pages/files/Page.axaml", static _ => null, []) { Platforms = PagePlatforms.Desktop },
        ]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
