using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Hosting;

/// <summary>
/// A Content Security Policy on every HTML document outside development: scripts from the app's origin only (every script
/// of the framework is a file), styles from it and inline (the writers' style attributes and page styles), images, media
/// and fonts from it or <c>data:</c>, images and media from any HTTPS origin as well, requests to it, no plugins, and
/// framing by it only. <see cref="SsrOptions.ContentSecurityPolicy"/> replaces it and an empty one turns it off; a policy
/// the app set on the answer is kept. Decided with the user on 2026-10-28 (the security audit's F5).
/// </summary>
public static class ContentSecurity
{
    public const string DefaultPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; "
        + "media-src 'self' data: https:; font-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";

    public static IServiceCollection AddContentSecurity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<IStartupFilter, PolicyFirst>();
        return services;
    }

    private sealed class PolicyFirst : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            var options = app.ApplicationServices.GetService<SsrOptions>() ?? new SsrOptions();
            if (!options.Development && options.ContentSecurityPolicy is { Length: > 0 } policy)
            {
                app.Use((http, inner) =>
                {
                    http.Response.OnStarting(() =>
                    {
                        if (http.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true && http.Response.Headers.ContentSecurityPolicy.Count == 0)
                        {
                            http.Response.Headers.ContentSecurityPolicy = policy;
                        }

                        return Task.CompletedTask;
                    });
                    return inner(http);
                });
            }

            next(app);
        };
    }
}
