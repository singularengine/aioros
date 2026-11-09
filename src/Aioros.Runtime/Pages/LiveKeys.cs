using System.Collections.Immutable;
using Aioros.Runtime.Caching;

namespace Aioros.Runtime.Pages;

/// <summary>
/// Where a live page shown in the installed app learns which data keys changed: in process from the app's own
/// <see cref="DataVersions"/>, or from the server's <c>/_live</c> stream, which carries only key names, so every read stays
/// an ordinary request (React Router's <c>useRevalidator</c> on a socket message, Turbo 8's refresh signal).
/// </summary>
public interface ILiveKeys
{
    /// <summary>
    /// The invalidations of <paramref name="keys"/> from now until <paramref name="cancellationToken"/> is cancelled; the
    /// watch starts before this returns.
    /// </summary>
    IAsyncEnumerable<Invalidation> WatchAsync(ImmutableArray<string> keys, CancellationToken cancellationToken);

    /// <summary>
    /// The source of a host's services: the server's stream when the loaders are remote, reconnecting as
    /// <paramref name="reconnection"/> says, its data versions otherwise.
    /// </summary>
    static ILiveKeys? For(IServiceProvider services, LiveReconnection? reconnection = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetService(typeof(ILoaderResults)) switch
        {
            RemoteLoaderResults remote => new RemoteLiveKeys(remote.Http, reconnection),
            null when services.GetService(typeof(DataVersions)) is DataVersions versions => new LocalLiveKeys(versions),
            _ => null,
        };
    }
}

/// <summary>The invalidations of the app's own data versions, for the desktop running the server's services itself.</summary>
public sealed class LocalLiveKeys(DataVersions versions) : ILiveKeys
{
    public IAsyncEnumerable<Invalidation> WatchAsync(ImmutableArray<string> keys, CancellationToken cancellationToken) => versions.WatchAsync(cancellationToken);
}
