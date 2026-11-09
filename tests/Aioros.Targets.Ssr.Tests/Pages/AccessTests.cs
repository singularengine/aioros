using System.Net;
using System.Security.Claims;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class AccessTests
{
    [Fact]
    [Trait("AC", "ACCESS-02")]
    public async Task PagesWithAccessRulesChallengeAndForbidAsync()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        var anonymous = await client.GetAsync(new Uri("/vault?tab=1", UriKind.Relative), TestContext.Current.CancellationToken);
        var post = await client.PostAsync(new Uri("/vault", UriKind.Relative), new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        var live = await client.GetAsync(new Uri("/vault/live", UriKind.Relative), TestContext.Current.CancellationToken);
        var member = await client.GetAsync(new Uri("/vault?as=member", UriKind.Relative), TestContext.Current.CancellationToken);
        var owner = await client.GetAsync(new Uri("/vault?as=owner", UriKind.Relative), TestContext.Current.CancellationToken);
        var open = await client.GetAsync(new Uri("/open", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal("/login?ReturnUrl=%2Fvault%3Ftab%3D1", anonymous.Headers.Location?.PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, live.StatusCode);
        Assert.Equal("/denied", member.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
    }

    [Fact]
    [Trait("AC", "ACCESS-03")]
    public async Task TheSignedInUserReachesTheLoaderAsync()
    {
        var model = new Model();
        var http = Site.Http();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ada")], "test"));
        var anonymous = new Model();

        await new Site().Handler.GetAsync(http, Site.Page(() => model));
        await new Site().Handler.GetAsync(Site.Http(), Site.Page(() => anonymous));

        Assert.Equal("ada", model.Request?.User.Identity?.Name);
        Assert.False(anonymous.Request?.User.Identity?.IsAuthenticated);
    }

    [Fact]
    [Trait("AC", "ACCESS-05")]
    public async Task FolderConventionsProtectPagesWithoutLoadersAsync()
    {
        var conventions = new PageConventions()
            .AuthorizeFolder("/Account", "owners")
            .AuthorizePage("/reports")
            .AllowAnonymousToPage("/account/login");
        PageDefinition Page(string route, string folder) => new(route, "Pages" + folder + "/Page.axaml", _ => new Model(), []);
        await using var app = await StartAsync(
            conventions,
            Page("/account/settings", "/account/settings"),
            Page("/account/login", "/account/login"),
            Page("/account/public", "/account/public") with { AllowAnonymous = true },
            Page("/reports", "/reports"),
            Page("/reports/daily", "/reports/daily"));
        var client = app.GetTestClient();

        var anonymous = await client.GetAsync(new Uri("/account/settings", UriKind.Relative), TestContext.Current.CancellationToken);
        var member = await client.GetAsync(new Uri("/account/settings?as=member", UriKind.Relative), TestContext.Current.CancellationToken);
        var owner = await client.GetAsync(new Uri("/account/settings?as=owner", UriKind.Relative), TestContext.Current.CancellationToken);
        var reports = await client.GetAsync(new Uri("/reports", UriKind.Relative), TestContext.Current.CancellationToken);
        var login = await client.GetAsync(new Uri("/account/login", UriKind.Relative), TestContext.Current.CancellationToken);
        var exception = await client.GetAsync(new Uri("/account/public", UriKind.Relative), TestContext.Current.CancellationToken);
        var outside = await client.GetAsync(new Uri("/reports/daily", UriKind.Relative), TestContext.Current.CancellationToken);
        var own = conventions.Apply(Page("/account/admin", "/account/admin") with { Access = [new PageAccess("admins", null)] });

        Assert.Equal("/login", anonymous.Headers.Location?.AbsolutePath);
        Assert.Equal("/denied", member.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        Assert.Equal("/login", reports.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
        Assert.Equal(HttpStatusCode.OK, outside.StatusCode);
        Assert.Equal([new PageAccess("owners", null), new PageAccess("admins", null)], own.Access);
    }

    [Fact]
    [Trait("AC", "ACCESS-06")]
    public async Task AProtectedPagesTemplateKeepsItsAccessRulesAsync()
    {
        var site = new Site();
        PageDefinition Page(string route, bool owners) =>
            new(route, "Pages" + route + "/Page.axaml", _ => new Model(), []) { Access = owners ? [new PageAccess(null, null), new PageAccess("owners", null)] : [] };
        await using var app = await StartAsync(site, null, Page("/vault", owners: true), Page("/open", owners: false), Page("/hidden", owners: true), Page("/shown", owners: false));
        var client = app.GetTestClient();
        async Task<string> PlanAsync(string text, params string[] routes)
        {
            site.Engine.Plan = new RenderPlan([], [new LiteralNode(text)], []);
            foreach (var route in routes)
            {
                await client.GetAsync(new Uri(route + "?as=owner", UriKind.Relative), TestContext.Current.CancellationToken);
            }

            return site.Engine.Plan.Template.Fingerprint;
        }

        var vault = await PlanAsync("<p>Vault</p>", "/vault");
        var open = await PlanAsync("<p>Open</p>", "/open");
        var shared = await PlanAsync("<p>Shared</p>", "/hidden", "/shown");
        Task<HttpResponseMessage> TemplateAsync(string fingerprint, string query = "") =>
            client.GetAsync(new Uri(PageHandler.TemplatePath + fingerprint + query, UriKind.Relative), TestContext.Current.CancellationToken);

        var anonymous = await TemplateAsync(vault);
        var member = await TemplateAsync(vault, "?as=member");
        var owner = await TemplateAsync(vault, "?as=owner");
        var everyone = await TemplateAsync(open);
        var both = await TemplateAsync(shared);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);
        Assert.Equal((HttpStatusCode.OK, "private, max-age=31536000, immutable"), (owner.StatusCode, owner.Headers.NonValidated["Cache-Control"].ToString()));
        Assert.Contains("Vault", await owner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.OK, "public, max-age=31536000, immutable"), (everyone.StatusCode, everyone.Headers.NonValidated["Cache-Control"].ToString()));
        Assert.Equal((HttpStatusCode.OK, "public, max-age=31536000, immutable"), (both.StatusCode, both.Headers.NonValidated["Cache-Control"].ToString()));
    }

    private static Task<WebApplication> StartAsync() =>
        StartAsync(
            null,
            new PageDefinition("/vault", "Pages/vault/Page.axaml", _ => new Model(), [], Live: true) { Access = [new PageAccess(null, null), new PageAccess("owners", null)] },
            new PageDefinition("/open", "Pages/open/Page.axaml", _ => new Model(), []));

    private static Task<WebApplication> StartAsync(PageConventions? conventions, params PageDefinition[] pages) => StartAsync(new Site(), conventions, pages);

    private static async Task<WebApplication> StartAsync(Site site, PageConventions? conventions, params PageDefinition[] pages)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options);
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/denied";
            options.ForwardAuthenticate = "query";
        }).AddScheme<AuthenticationSchemeOptions, QueryUser>("query", null);
        builder.Services.AddAuthorizationBuilder().AddPolicy("owners", policy => policy.RequireRole("owner"));
        var app = builder.Build();
        app.MapAiorosPages(pages, conventions);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class QueryUser(Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options, Microsoft.Extensions.Logging.ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Request.Query["as"].ToString() is { Length: > 0 } role
                ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, role), new Claim(ClaimTypes.Role, role)], "query")), "query"))
                : AuthenticateResult.NoResult());
    }
}
