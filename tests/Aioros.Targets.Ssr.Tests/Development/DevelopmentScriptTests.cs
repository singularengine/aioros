using Aioros.Events;
using Aioros.Pages;
using Aioros.Runtime.Events;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Development;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Development;
using Aioros.Targets.Ssr.Tests.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Development;

public sealed class DevelopmentScriptTests
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-285")]
    public async Task APageRefreshesInPlaceWhenItsDevelopmentChannelComesBackAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var site = new Site(new SsrOptions { Development = true });
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<p>Inbox</p>")], []);
        var connections = 0;
        var restarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddSingleton<IEventBus, EventBus>().AddHttpContextAccessor();
        await using var app = builder.Build();
        app.Use(async (http, next) =>
        {
            if (http.Request.Path == DevelopmentChannel.Path && Interlocked.Increment(ref connections) == 1)
            {
                http.Response.ContentType = "text/event-stream";
                await http.Response.WriteAsync("retry: 250\n\n", http.RequestAborted);
                return;
            }

            if (http.Request.Path == DevelopmentChannel.Path)
            {
                restarted.TrySetResult();
            }

            await next(http);
        });
        app.MapAiorosPages([new PageDefinition("/inbox", "Pages/inbox/Page.axaml", static _ => new Model("Inbox"), [])]);
        await app.StartAsync(cancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium",
            Args = ["--no-sandbox"],
        });
        var page = await browser.NewPageAsync();

        var refreshed = page.WaitForRequestAsync(request => request.ResourceType == "fetch" && new Uri(request.Url).AbsolutePath == "/inbox");
        await page.GotoAsync(address + "/inbox");
        await page.EvaluateAsync("window.kept = 'yes'");
        await restarted.Task.WaitAsync(cancellationToken);
        await refreshed.WaitAsync(cancellationToken);
        using var client = new HttpClient();
        await using var channel = await client.GetStreamAsync(new Uri(address + DevelopmentChannel.Path), cancellationToken);
        var opening = new byte["retry: 250\n\n".Length];
        await channel.ReadExactlyAsync(opening, cancellationToken);

        Assert.Equal("retry: 250\n\n", System.Text.Encoding.UTF8.GetString(opening));
        Assert.Equal("yes", await page.EvaluateAsync<string>("window.kept"));
        Assert.Equal("/inbox", new Uri(page.Url).AbsolutePath);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-286")]
    public async Task OpenStreamsEndWhenTheAppStopsAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var site = new Site(new SsrOptions { Development = true });
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<p>Live</p>")], []);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromHours(1));
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddSingleton<IEventBus, EventBus>().AddHttpContextAccessor();
        await using var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/inbox", "Pages/inbox/Page.axaml", static _ => new Model("Inbox"), [], Live: true)]);
        await app.StartAsync(cancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var client = new HttpClient();
        await using var development = await client.GetStreamAsync(new Uri(address + DevelopmentChannel.Path), cancellationToken);
        await using var live = await client.GetStreamAsync(new Uri(address + "/inbox/live"), cancellationToken);
        var buffer = new byte[256];
        await development.ReadAtLeastAsync(buffer, 1, throwOnEndOfStream: true, cancellationToken);
        await live.ReadAtLeastAsync(buffer, 1, throwOnEndOfStream: true, cancellationToken);

        await app.StopAsync(cancellationToken);

        Assert.True(await EndsAsync(development, cancellationToken));
        Assert.True(await EndsAsync(live, cancellationToken));
    }

    private static async Task<bool> EndsAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (await stream.ReadAsync(buffer, cancellationToken) > 0)
        {
        }

        return true;
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-288")]
    public async Task FaultsShowOverThePageAndClearOnceFixedAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var site = new Site(new SsrOptions { Development = true });
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<p>Inbox</p>")], []);
        var broken = false;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = Path.GetDirectoryName(Here()) });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddSingleton<IEventBus, EventBus>().AddHttpContextAccessor();
        builder.Services.AddSingleton<Microsoft.AspNetCore.Diagnostics.IDeveloperPageExceptionFilter, DevelopmentFaults>();
        await using var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.MapAiorosPages([new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => Volatile.Read(ref broken) ? throw new InvalidOperationException("Inbox broke") : new Model("Inbox"), [])]);
        await app.StartAsync(cancellationToken);
        var bus = app.Services.GetRequiredService<IEventBus>();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium",
            Args = ["--no-sandbox"],
        });
        var page = await browser.NewPageAsync();
        var fault = page.Locator("[data-aioros-fault]");

        Task<IResponse> SubscribedAsync() => page.WaitForResponseAsync(response => response.Url.EndsWith(DevelopmentChannel.Path, StringComparison.Ordinal));
        var connected = SubscribedAsync();
        await page.GotoAsync(address + "/inbox");
        await connected;
        await page.EvaluateAsync("window.kept = 'yes'");
        Volatile.Write(ref broken, true);
        await bus.PublishAsync(new XamlReloaded("Pages/inbox/Page.axaml"), cancellationToken);
        await fault.WaitForAsync();
        var shown = await fault.InnerTextAsync();
        var kept = await page.EvaluateAsync<string>("window.kept");
        var content = await page.Locator("p").First.InnerTextAsync();
        Volatile.Write(ref broken, false);
        await bus.PublishAsync(new XamlReloaded("Pages/inbox/Page.axaml"), cancellationToken);
        await fault.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        Volatile.Write(ref broken, true);
        var reconnected = SubscribedAsync();
        await page.GotoAsync(address + "/inbox");
        await reconnected;
        await fault.WaitForAsync();
        var document = await fault.InnerTextAsync();
        await page.WaitForFunctionAsync("document.readyState === 'complete'");
        Volatile.Write(ref broken, false);
        var reloaded = SubscribedAsync();
        await bus.PublishAsync(new XamlReloaded("Pages/inbox/Page.axaml"), cancellationToken);
        await page.Locator("p", new() { HasText = "Inbox" }).WaitForAsync();
        await reloaded;
        await bus.PublishAsync(new BuildReported([new ReportedError("Models/Inbox.cs", 11, 19, "CS1002", "; expected", [new FaultLine(11, "    var x = 1")])]), cancellationToken);
        await fault.WaitForAsync();
        var build = await fault.InnerTextAsync();
        await bus.PublishAsync(new BuildReported([]), cancellationToken);
        await fault.WaitForAsync(new() { State = WaitForSelectorState.Detached });

        Assert.Contains("System.InvalidOperationException: Inbox broke", shown, StringComparison.Ordinal);
        Assert.Contains("DevelopmentScriptTests.cs:", shown, StringComparison.Ordinal);
        Assert.Contains("Inbox broke\")", shown, StringComparison.Ordinal);
        Assert.Equal(("yes", "Inbox"), (kept, content));
        Assert.Contains("Inbox broke", document, StringComparison.Ordinal);
        Assert.Equal(0, await fault.CountAsync());
        Assert.Contains("Build failed", build, StringComparison.Ordinal);
        Assert.Contains("CS1002: ; expected", build, StringComparison.Ordinal);
        Assert.Contains("Models/Inbox.cs:11:19", build, StringComparison.Ordinal);
        Assert.Contains(">   11 |     var x = 1", build, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "CLI-46")]
    public async Task AHeadlessBrowserNoToolDrivesOpensNoDevelopmentChannelAsync()
    {
        const string Spy = "window.opened = 0; window.EventSource = class { constructor() { window.opened++; } addEventListener() {} close() {} }; Object.defineProperty(Navigator.prototype, 'webdriver', { get() { window.asked = true; return false; } });";
        var cancellationToken = TestContext.Current.CancellationToken;
        var site = new Site(new SsrOptions { Development = true });
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<p>Inbox</p>")], []);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddSingleton<IEventBus, EventBus>().AddHttpContextAccessor();
        await using var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/inbox", "Pages/inbox/Page.axaml", static _ => new Model("Inbox"), [])]);
        await app.StartAsync(cancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });

        async Task<int> OpenedAsync(string agent)
        {
            var page = await browser.NewPageAsync(new() { UserAgent = $"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) {agent}/154.0.0.0 Safari/537.36" });
            await page.AddInitScriptAsync(Spy);
            await page.GotoAsync(address + "/inbox");
            await page.WaitForFunctionAsync("() => window.asked === true");
            return await page.EvaluateAsync<int>("() => window.opened");
        }

        Assert.Equal(0, await OpenedAsync("HeadlessChrome"));
        Assert.Equal(1, await OpenedAsync("Chrome"));
    }

    private static string Here([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
}
