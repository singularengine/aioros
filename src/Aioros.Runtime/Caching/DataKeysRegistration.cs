using Aioros.Functions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aioros.Runtime.Caching;

public static class DataKeysRegistration
{
    /// <summary>
    /// Registers the app's data versions and its <see cref="IDataKeys"/>, once: what a command or a background service
    /// invalidates evicts what the caches kept and refreshes the live pages whose loaders read it.
    /// </summary>
    public static IServiceCollection AddDataKeys(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DataVersions>();
        services.TryAddSingleton<IDataKeys>(static provider => new EvictingKeys(provider.GetRequiredService<DataVersions>()));
        return services;
    }
}
