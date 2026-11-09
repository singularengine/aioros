using System.Security.Cryptography;
using Aioros.Runtime.SignIn;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

namespace Aioros.Targets.Ssr.SignIn;

/// <summary>
/// The authorization server of the installed app, on OpenIddict: one public native client, the authorization code flow
/// with PKCE S256 only, a loopback redirect with any port (RFC 8252), rotating refresh tokens whose reuse is refused
/// (RFC 9700), revocation (RFC 7009), and the app's own cookie sign-in in the browser. A bearer token is accepted wherever
/// the cookie is. Each sign-in of the app is one authorization, a session of <see cref="InstalledAppSessions"/> that names
/// its device; revoking it, or its refresh token, ends it, and an access token of an ended session is refused at once.
/// </summary>
public static class InstalledAppAuthorization
{
    /// <summary>The scheme that authenticates a request by its bearer token when it carries one, by the cookie otherwise.</summary>
    public const string Scheme = "Aioros.BearerOrCookie";

    /// <summary>The authorization request's parameter that names the device the app runs on, shown in the user's list of sessions.</summary>
    public const string DeviceParameter = SessionEvents.DeviceParameter;

    /// <summary>
    /// Registers the authorization server, its client and the bearer validation.
    /// </summary>
    /// <param name="services">The app's services.</param>
    /// <param name="stores">Where OpenIddict keeps clients, authorizations and tokens: <c>core.UseEntityFrameworkCore().UseDbContext&lt;T&gt;()</c>.</param>
    /// <param name="options">The client, the endpoints and the lifetimes.</param>
    /// <param name="requireHttps">Whether the endpoints refuse plain HTTP; off in development.</param>
    public static IServiceCollection AddInstalledAppAuthorization(this IServiceCollection services, Action<OpenIddictCoreBuilder> stores, InstalledAppSignInOptions options, bool requireHttps)
    {
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddOpenIddict()
            .AddCore(stores)
            .AddServer(server => Server(server, options, requireHttps))
            .AddValidation(validation =>
            {
                validation.UseLocalServer();
                validation.EnableAuthorizationEntryValidation();
                validation.AddEventHandler(SessionEvents.CheckingSession.Descriptor);
                validation.UseDataProtection();
                validation.UseAspNetCore();
            });
        services.AddAuthentication().AddPolicyScheme(Scheme, null, policy => policy.ForwardDefaultSelector = http =>
            http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                : options.CookieScheme);
        services.PostConfigure<AuthenticationOptions>(authentication => authentication.DefaultAuthenticateScheme = Scheme);
        services.AddHostedService<ClientRegistration>();
        services.AddScoped<InstalledAppSessions>();
        if (options.RevokeOnWebSignOut)
        {
            services.PostConfigure<CookieAuthenticationOptions>(options.CookieScheme, cookie => cookie.Events.OnSigningOut = SessionEvents.RevokingOnSignOut(cookie.Events.OnSigningOut));
        }

        return services;
    }

    private static void Server(OpenIddictServerBuilder server, InstalledAppSignInOptions options, bool requireHttps)
    {
        server.SetAuthorizationEndpointUris(options.AuthorizePath).SetTokenEndpointUris(options.TokenPath).SetRevocationEndpointUris(options.RevokePath);
        server.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange().AllowRefreshTokenFlow();
        server.SetAccessTokenLifetime(options.AccessTokenLifetime).SetRefreshTokenLifetime(options.RefreshTokenLifetime).SetRefreshTokenReuseLeeway(TimeSpan.Zero);
        server.AddEncryptionKey(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32))).AddSigningKey(new ECDsaSecurityKey(ECDsa.Create(ECCurve.NamedCurves.nistP256)));
        server.UseDataProtection();
        server.AddEventHandler(SessionEvents.EndingOnRevocation.Descriptor);
        var host = server.UseAspNetCore().EnableAuthorizationEndpointPassthrough();
        if (!requireHttps)
        {
            host.DisableTransportSecurityRequirement();
        }
    }

    /// <summary>Maps the authorization endpoint; OpenIddict answers the token endpoint itself.</summary>
    /// <param name="endpoints">The app's endpoints.</param>
    /// <param name="pages">Whether a path, without its query, is one of the app's pages: the page a first install returns to must be one.</param>
    public static IEndpointRouteBuilder MapInstalledAppAuthorization(this IEndpointRouteBuilder endpoints, Func<string, bool>? pages = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var options = endpoints.ServiceProvider.GetRequiredService<InstalledAppSignInOptions>();
        endpoints.MapMethods(options.AuthorizePath, [HttpMethods.Get, HttpMethods.Post], (Delegate)((HttpContext http) => InstalledAppAuthorize.AuthorizeAsync(http, options, pages)));
        return endpoints;
    }
}
