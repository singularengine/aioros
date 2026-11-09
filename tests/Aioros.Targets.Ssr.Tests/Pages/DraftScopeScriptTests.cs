using System.Security.Claims;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class DraftScopeScriptTests
{
    private sealed class Model : IPageModel
    {
        public string Title => string.Empty;
    }

    private const string Saved = "() => document.documentElement.getAttribute('data-aioros-draft') === 'saved'";

    private const string Scope = "aioros-draft-scope";

    private static async Task SignInAsync(IPage page, string address, string user) =>
        await page.Context.AddCookiesAsync([new Cookie { Name = "who", Value = user, Url = address }]);

    private static async Task OpenAsync(IPage page, string address)
    {
        await page.GotoAsync(address + "/offline");
        await page.WaitForFunctionAsync("() => !!window.aioros", null, new() { PollingInterval = 20 });
    }

    private static async Task<int> OfferedAsync(IPage page, string value)
    {
        await page.FillAsync("#t", value);
        await page.WaitForFunctionAsync(Saved, null, new() { PollingInterval = 20 });
        return await page.Locator(".aioros-draft").CountAsync();
    }

    private static async Task<string?> CookieAsync(IPage page) => (await page.Context.CookiesAsync()).FirstOrDefault(static cookie => cookie.Name == Scope)?.Value;

    [Fact]
    [Trait("AC", "SSR-435")]
    public async Task ADraftIsOfferedOnlyToTheUserWhoWroteItAsync()
    {
        await using var stage = await StartAsync();
        var (page, address) = (stage.Page, stage.Address);

        await SignInAsync(page, address, "ada@example.com");
        await OpenAsync(page, address);
        await page.FillAsync("#t", "ada's draft");
        await page.WaitForFunctionAsync(Saved, null, new() { PollingInterval = 20 });
        var scope = await CookieAsync(page);
        await OpenAsync(page, address);
        await page.WaitForSelectorAsync(".aioros-draft[role=status]");

        await SignInAsync(page, address, "bob@example.com");
        await OpenAsync(page, address);
        var toBob = await OfferedAsync(page, "bob's draft");
        var changed = await CookieAsync(page);

        await SignInAsync(page, address, string.Empty);
        await OpenAsync(page, address);
        var toNobody = await OfferedAsync(page, "anonymous draft");
        var signedOut = await CookieAsync(page);

        Assert.NotNull(scope);
        Assert.DoesNotContain("ada", scope, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(scope, changed);
        Assert.Equal(0, toBob);
        Assert.Equal(0, toNobody);
        Assert.Null(signedOut);
    }

    [Fact]
    [Trait("AC", "SSR-436")]
    public async Task ADraftWithoutAScopeStaysWithTheVisitorsNotSignedInAsync()
    {
        await using var stage = await StartAsync();
        var (page, address) = (stage.Page, stage.Address);

        await OpenAsync(page, address);
        await page.FillAsync("#t", "visitor's draft");
        await page.WaitForFunctionAsync(Saved, null, new() { PollingInterval = 20 });
        await OpenAsync(page, address);
        await page.WaitForSelectorAsync(".aioros-draft[role=status]");

        await SignInAsync(page, address, "ada@example.com");
        await OpenAsync(page, address);
        var toAda = await OfferedAsync(page, "ada's draft");

        await SignInAsync(page, address, string.Empty);
        await OpenAsync(page, address);
        await page.WaitForSelectorAsync(".aioros-draft[role=status]");

        Assert.Equal(0, toAda);
    }

    private static async Task<Stage> StartAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new LiteralNode("""<input type="text" name="f0" id="t" value="server">""")], []);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        DataProtectionStateSeal.AddStateSeal(builder.Services);
        var app = builder.Build();
        app.Use(async (http, next) =>
        {
            if (http.Request.Cookies["who"] is { Length: > 0 } user)
            {
                http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], "test"));
            }

            await next(http);
        });
        app.MapAiorosPages([new PageDefinition("/offline", "Pages/offline/Page.axaml", _ => new Model(), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new Stage(app, playwright, browser, page, address);
    }

    private sealed class Stage(WebApplication app, IPlaywright playwright, IBrowser browser, IPage page, string address) : IAsyncDisposable
    {
        public IPage Page => page;

        public string Address => address;

        public async ValueTask DisposeAsync()
        {
            await browser.DisposeAsync();
            playwright.Dispose();
            await app.DisposeAsync();
        }
    }
}
