using Aioros.Events;
using Aioros.Pages;
using Aioros.Runtime.Events;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class StreamCacheScriptTests
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-188")]
    public async Task StreamsCloseWhenThePageGoesIntoTheBackForwardCacheAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var site = new Site(new SsrOptions { Development = true });
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("<p>Live</p>")], []);
        var live = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var development = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddSingleton<IEventBus, EventBus>().AddHttpContextAccessor();
        await using var app = builder.Build();
        app.Use(async (http, next) =>
        {
            var ended = http.Request.Path.Value switch
            {
                "/inbox/live" => live,
                "/_aioros/dev" => development,
                _ => null,
            };
            if (ended == live)
            {
                opened.TrySetResult();
            }

            await next(http);
            ended?.TrySetResult();
        });
        app.MapAiorosPages([
            new PageDefinition("/inbox", "Pages/inbox/Page.axaml", static _ => new Model("Inbox"), [], Live: true),
            new PageDefinition("/other", "Pages/other/Page.axaml", static _ => new Model("Other"), []),
        ]);
        await app.StartAsync(cancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium",
            Args = ["--no-sandbox", "--enable-features=BackForwardCache"],
            IgnoreDefaultArgs = ["--disable-back-forward-cache"],
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(address + "/inbox");
        await opened.Task.WaitAsync(cancellationToken);
        await page.GotoAsync(address + "/other");
        await Task.WhenAll(live.Task, development.Task).WaitAsync(cancellationToken);
        var reopened = page.WaitForRequestAsync(request => request.Url.Contains("/inbox/live", StringComparison.Ordinal));
        await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit });
        await reopened.WaitAsync(cancellationToken);

        Assert.Equal("/inbox", new Uri(page.Url).AbsolutePath);
    }
}
