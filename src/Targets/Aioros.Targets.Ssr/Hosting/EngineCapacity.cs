using Aioros.Runtime.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Hosting;

/// <summary>
/// Answers a request the engine refused for a full queue (<see cref="EngineBusyException"/>) with <c>503</c> and
/// <c>Retry-After: 1</c> rather than a <c>500</c>, as ASP.NET Core's concurrency limiter answers a full queue.
/// </summary>
public static class EngineCapacity
{
    public static IServiceCollection AddEngineCapacity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<IStartupFilter, BusyFirst>();
        return services;
    }

    private sealed class BusyFirst : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(static async (http, inner) =>
            {
                try
                {
                    await inner(http);
                }
                catch (EngineBusyException) when (!http.Response.HasStarted)
                {
                    http.Response.Clear();
                    http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    http.Response.Headers.RetryAfter = "1";
                }
            });
            next(app);
        };
    }
}
