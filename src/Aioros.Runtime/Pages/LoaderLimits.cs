using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Runtime.Pages;

/// <summary>
/// How many loaders may run at once: <see cref="PerLoad"/> within one page load (a request, a navigation or a
/// revalidation), and <see cref="Global"/> across every load of the app when it is set, sized to the data source's
/// connection pool as .NET's <c>ConcurrencyLimiter</c> is. The tokens are a channel's items, so waiting loaders queue on
/// the channel and nothing locks.
/// </summary>
public sealed class LoaderLimits
{
    /// <summary>The loaders one load runs at once unless the app sets another limit.</summary>
    public const int DefaultPerLoad = 8;

    private readonly int _global;

    public static LoaderLimits Default { get; } = new();

    /// <summary>
    /// The loaders one load runs at once, or <c>0</c> for no limit; a load with no more loaders than this runs them all
    /// without a gate.
    /// </summary>
    public int PerLoad { get; init; } = DefaultPerLoad;

    /// <summary>The loaders every load of the app runs at once together, or <c>0</c> for no such limit.</summary>
    public int Global
    {
        get => _global;
        init
        {
            _global = value;
            GlobalTokens = value > 0 ? Tokens(value) : null;
        }
    }

    internal Channel<bool>? GlobalTokens { get; private init; }

    internal static Channel<bool> Tokens(int count)
    {
        var tokens = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        for (var index = 0; index < count; index++)
        {
            tokens.Writer.TryWrite(true);
        }

        return tokens;
    }
}

public static class LoaderLimitsRegistration
{
    /// <summary>
    /// Sets how many loaders run at once, per load (<c>0</c> for no limit) and, when <paramref name="global"/> is above zero,
    /// across the app.
    /// </summary>
    public static IServiceCollection AddLoaderLimits(this IServiceCollection services, int perLoad = LoaderLimits.DefaultPerLoad, int global = 0)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfNegative(perLoad);
        ArgumentOutOfRangeException.ThrowIfNegative(global);
        return services.AddSingleton(new LoaderLimits { PerLoad = perLoad, Global = global });
    }
}
