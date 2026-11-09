using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Hosting;

/// <summary>
/// Requests that reach the app through a reverse proxy which terminates TLS: the client's address and scheme are taken
/// from <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> first in the pipeline, from the proxies
/// <see cref="ForwardedHeadersOptions"/> trusts, the loopback address unless the app configures others (an ingress on
/// another machine is added to <see cref="ForwardedHeadersOptions.KnownProxies"/> or
/// <see cref="ForwardedHeadersOptions.KnownIPNetworks"/>, or trusted with ASP.NET Core's
/// <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c>), as ASP.NET Core's forwarded headers middleware does.
/// </summary>
public static class ProxiedRequests
{
    private const string ForwardedProto = "X-Forwarded-Proto";

    /// <summary>Applies the forwarded headers of trusted proxies before anything else reads the request's scheme or address.</summary>
    public static IServiceCollection AddProxiedRequests(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure<ForwardedHeadersOptions>(static options => options.ForwardedHeaders |= ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        services.AddTransient<IStartupFilter, ForwardedFirst>();
        return services;
    }

    /// <summary>
    /// Whether a cookie set on this request is marked <c>Secure</c>: over HTTPS, or when a proxy says the visitor's scheme
    /// is HTTPS, trusted or not, since that can only make the cookie stricter; plain HTTP otherwise (a LAN address, where a
    /// browser drops a <c>Secure</c> cookie), as <c>CookieSecurePolicy.SameAsRequest</c>.
    /// </summary>
    internal static bool SecureCookies(HttpRequest request) =>
        request.IsHttps || request.Headers[ForwardedProto].Any(static value => value is not null && value.Contains("https", StringComparison.OrdinalIgnoreCase));

    private sealed class ForwardedFirst : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseForwardedHeaders();
            next(app);
        };
    }
}
