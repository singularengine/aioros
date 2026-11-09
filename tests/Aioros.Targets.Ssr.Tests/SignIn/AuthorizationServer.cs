using System.Net;
using System.Security.Claims;
using Aioros.Runtime.SignIn;
using Aioros.Targets.Ssr.SignIn;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Tests.SignIn;

/// <summary>
/// The installed app's authorization server on a test host: a cookie sign-in at <c>/test/signin</c>, OpenIddict over an
/// in-memory store (a shared one when the test names it), and whatever else the test maps. A "browser" here is an HTTP client holding the sign-in cookie.
/// </summary>
public sealed class AuthorizationServer : IAsyncDisposable
{
    public const string UserId = "user-1";
    public const string UserName = "ana";

    private readonly SqliteConnection _database;

    private AuthorizationServer(WebApplication app, InstalledAppSignInOptions options, SqliteConnection database)
    {
        App = app;
        Options = options;
        _database = database;
    }

    public WebApplication App { get; }

    public InstalledAppSignInOptions Options { get; }

    public Uri BaseAddress => App.GetTestServer().BaseAddress;

    public const string OtherUserId = "user-2";
    public const string OtherUserName = "bo";

    public static async Task<AuthorizationServer> StartAsync(Action<IServiceCollection>? services = null, Action<WebApplication>? map = null, TimeSpan? accessTokenLifetime = null, Func<InstalledAppSignInOptions, InstalledAppSignInOptions>? configure = null, Func<string, bool>? pages = null, string database = "Data Source=:memory:")
    {
        var options = new InstalledAppSignInOptions { CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme, AccessTokenLifetime = accessTokenLifetime ?? TimeSpan.FromMinutes(15) };
        options = configure?.Invoke(options) ?? options;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        var connection = new SqliteConnection(database);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddDbContext<Store>(db => db.UseSqlite(connection).UseOpenIddict());
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(cookie => cookie.LoginPath = options.SignInPath);
        builder.Services.AddAuthorization();
        builder.Services.AddInstalledAppAuthorization(core => core.UseEntityFrameworkCore().UseDbContext<Store>(), options, requireHttps: false);
        services?.Invoke(builder.Services);
        var app = builder.Build();
        app.MapGet("/test/signin", static (HttpContext http, string? user) => http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(user == OtherUserName ? [new Claim(ClaimTypes.NameIdentifier, OtherUserId), new Claim(ClaimTypes.Name, OtherUserName)] : [new Claim(ClaimTypes.NameIdentifier, UserId), new Claim(ClaimTypes.Name, UserName)], CookieAuthenticationDefaults.AuthenticationScheme))));
        app.MapGet("/test/signout", static (HttpContext http) => http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme));
        app.MapInstalledAppAuthorization(pages);
        map?.Invoke(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Store>().Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        await app.StartAsync(TestContext.Current.CancellationToken);
        return new AuthorizationServer(app, options, connection);
    }

    /// <summary>
    /// Signs in and out once on a server of its own, so that a fixture pays the process's first run of ASP.NET Core, EF Core
    /// and OpenIddict before any test starts its timeout: on a loaded runner that first run takes most of a minute.
    /// </summary>
    public static async Task WarmUpAsync()
    {
        await using var server = await StartAsync();
        var cookie = await server.CookieAsync();
        using var tokens = server.Client();
        var signIn = new SignInClient(server.Options, tokens, new SessionTokenStore(), TimeProvider.System);
        await signIn.SignInAsync(server.Browser(cookie), askReturnPath: true, TestContext.Current.CancellationToken);
        await signIn.SignOutAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A client of the server for the app's token requests, through the test host.</summary>
    public HttpClient Client(DelegatingHandler? outer = null)
    {
        var inner = App.GetTestServer().CreateHandler();
        if (outer is not null)
        {
            outer.InnerHandler = inner;
        }

        return new HttpClient(outer ?? inner) { BaseAddress = BaseAddress };
    }

    /// <summary>Signs the browser in with the app's cookie and returns the cookie to send.</summary>
    public async Task<string> CookieAsync(string? user = null)
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/test/signin" + (user is null ? string.Empty : "?user=" + user), UriKind.Relative), TestContext.Current.CancellationToken);
        return string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(static cookie => cookie.Split(';')[0]));
    }

    /// <summary>What the browser gets from the authorization endpoint, without following the redirect.</summary>
    public async Task<HttpResponseMessage> BrowseAsync(Uri address, string? cookie)
    {
        using var client = Client();
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>The system browser of a test: opens the authorization request with the cookie and follows the redirect to the loopback.</summary>
    public Func<Uri, Task> Browser(string cookie) => async address =>
    {
        using var answer = await BrowseAsync(address, cookie);
        if (answer.StatusCode != HttpStatusCode.Redirect || answer.Headers.Location is not { } location)
        {
            throw new InvalidOperationException($"The authorization endpoint answered {(int)answer.StatusCode}.");
        }

        using var loopback = new HttpClient();
        using var _ = await loopback.GetAsync(location, TestContext.Current.CancellationToken);
    };

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        await _database.DisposeAsync();
    }

    private sealed class Store(DbContextOptions<Store> options) : DbContext(options);
}
