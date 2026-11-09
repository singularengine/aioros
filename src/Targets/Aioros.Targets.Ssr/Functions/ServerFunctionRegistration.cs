using Aioros.Runtime.Caching;
using Aioros.Runtime.Functions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aioros.Targets.Ssr.Functions;

public static class ServerFunctionRegistration
{
    /// <summary>
    /// Registers a server function interface: its implementation in process, through the generated in-process caller that
    /// fails as the client would, or its generated client when the app talks to a remote server; and
    /// <see cref="Aioros.Functions.IDataKeys"/>, which records into the command running in the caller's flow; and the data
    /// cache, which the in-process caller keeps its <c>[Cached]</c> results in and evicts its <c>[Invalidates]</c> keys from.
    /// </summary>
    public static IServiceCollection AddServerFunction<TInterface, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection services, IReadOnlyList<ServerFunctionGroup> groups, Uri? server, string? group = null)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(groups);
        services.AddDataKeys();
        services.TryAddSingleton(static provider => new DataCache(provider.GetRequiredService<DataVersions>(), provider.GetService<TimeProvider>() ?? TimeProvider.System));
        var functions = groups.FirstOrDefault(candidate => candidate.Interface == typeof(TInterface));
        if (functions is not null && group is not null && functions.Group != group)
        {
            throw new InvalidOperationException($"{typeof(TInterface).Name} was generated with the group '{functions.Group}', not '{group}'.");
        }

        if (server is null && functions?.InProcess is { } inProcess)
        {
            services.TryAddScoped<TImplementation>();
            return services.AddScoped(provider => (TInterface)Cached(inProcess(provider.GetRequiredService<TImplementation>()), provider));
        }

        if (server is null)
        {
            return services.AddScoped<TInterface, TImplementation>();
        }

        _ = functions ?? throw new InvalidOperationException($"{typeof(TInterface).Name} has no generated server functions; register it with AddServerFunction in the app's project.");
        services.AddHttpClient(functions.Group, http => http.BaseAddress = server);
        return services.AddTransient(provider => (TInterface)functions.Client(provider.GetRequiredService<IHttpClientFactory>().CreateClient(functions.Group)));
    }

    private static object Cached(object caller, IServiceProvider provider)
    {
        if (caller is ICachedCalls cached)
        {
            cached.Cache = provider.GetService<DataCache>();
        }

        return caller;
    }
}
