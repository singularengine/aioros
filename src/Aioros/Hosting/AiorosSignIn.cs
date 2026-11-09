using Aioros.Hosting;
using Aioros.Runtime.Pages;
using Aioros.Runtime.SignIn;
using Aioros.Targets.Ssr.SignIn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Builder;

public static class AiorosSignIn
{
    /// <summary>
    /// Lets the installed app sign in against this server. On the website: an OAuth 2.0 authorization server (OpenIddict)
    /// for the app's own native client, over the app's cookie sign-in, and a bearer token accepted on <c>/_data</c> and
    /// <c>/_sf</c> alongside the cookie. In the installed app (<c>Aioros:Server</c>): the sign-in through the system browser
    /// with PKCE and a loopback redirect, the tokens in the operating system's store, attached to the page data and the
    /// server function clients and refreshed. The desktop running in process needs neither. The server needs no keys or
    /// certificates: its tokens are protected by the app's data protection key ring, so they outlive a restart wherever
    /// the sign-in cookie does (ASP.NET Core persists the ring by default; a container or a farm persists and shares it).
    /// </summary>
    /// <param name="builder">The app's builder.</param>
    /// <param name="stores">Where the server keeps clients, authorizations and tokens: <c>core => core.UseEntityFrameworkCore().UseDbContext&lt;T&gt;()</c>.</param>
    /// <param name="options">The sign-in route, the cookie scheme, the endpoints and the lifetimes; the defaults fit ASP.NET Core Identity with <c>/login</c>.</param>
    public static WebApplicationBuilder AddInstalledAppSignIn(this WebApplicationBuilder builder, Action<OpenIddictCoreBuilder> stores, InstalledAppSignInOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(stores);
        var settings = options ?? new InstalledAppSignInOptions();
        var mode = AiorosHost.Mode(builder);
        switch (mode.Role)
        {
            case AiorosRole.Server:
                builder.Services.AddInstalledAppAuthorization(stores, settings, requireHttps: !builder.Environment.IsDevelopment());
                break;
            case AiorosRole.Client:
                AddClient(builder.Services, settings, mode.Server!, AiorosHost.App(builder).Assembly.GetName().Name ?? "app");
                break;
            default:
                builder.Services.AddScoped<InstalledAppSessions>();
                break;
        }

        return builder;
    }

    /// <summary>
    /// On the website with the installed-app sign-in: keeps the page a download link carries for the first install and
    /// maps the authorization endpoint, both checked against the app's pages.
    /// </summary>
    internal static void MapInstalledAppSignIn(WebApplication app, AiorosApp aioros)
    {
        if (app.Services.GetService<InstalledAppSignInOptions>() is null)
        {
            return;
        }

        var routes = aioros.Pages.Aggregate(Aioros.Routing.Tables.RouteTable.Empty, static (table, page) => table.Add(page.Route));
        bool IsPage(string path) => routes.Match(path).IsMatch;
        app.UseInstalledAppReturnPath(app.Services.GetRequiredService<Aioros.Targets.Ssr.Hosting.SsrOptions>().Download, IsPage);
        app.MapInstalledAppAuthorization(IsPage);
    }

    private static void AddClient(IServiceCollection services, InstalledAppSignInOptions options, Uri server, string app)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(options);
        services.AddSingleton<ITokenStore>(provider => TokenStores.ForCurrentOs(app, server, provider.GetRequiredService<ILoggerFactory>().CreateLogger("Aioros.SignIn")));
        services.AddSingleton(provider => new SignInClient(options, new HttpClient { BaseAddress = server }, provider.GetRequiredService<ITokenStore>(), provider.GetRequiredService<TimeProvider>()));
        services.ConfigureHttpClientDefaults(http => http.AddHttpMessageHandler(provider => new SignedInHandler(provider.GetRequiredService<SignInClient>())));
        services.AddSingleton<ILoaderResults>(provider => new RemoteLoaderResults(new HttpClient(new SignedInHandler(provider.GetRequiredService<SignInClient>()) { InnerHandler = new SocketsHttpHandler() }) { BaseAddress = server }));
    }
}
