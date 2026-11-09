using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Pages;
using Aioros.Runtime.SignIn;
using Avalonia.Controls;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>
/// The watch of the live page the navigator shows: one at a time, on the keys its loaders read, started when the page is
/// shown, started again when those keys change, and stopped when the navigator leaves it, as Phoenix LiveView subscribes
/// in <c>mount</c> and unsubscribes with its process. The navigator owns it on the UI thread, where the invalidations
/// arrive. The installed app's stream reconnects as the app's <see cref="LiveReconnection"/> says (one from its services,
/// or one on its clock), woken when the window is reactivated or the network becomes available; a refusal that asks for a
/// sign-in goes to the navigator's failures, as a refused load does.
/// </summary>
internal sealed class LiveViews
{
    private readonly ILiveKeys? _source;

    private readonly Func<Exception, Task> _failed;

    private Action? _stop;

    private ImmutableArray<string> _keys = [];

    public LiveViews(IServiceProvider services, Window window, Func<Exception, Task> failed)
    {
        var reconnection = services.GetService(typeof(LiveReconnection)) as LiveReconnection
            ?? new LiveReconnection(services.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System);
        _source = ILiveKeys.For(services, reconnection);
        _failed = failed;
        LiveWakeups.Attach(window, reconnection);
    }

    /// <summary>Watches the keys a live page's loaders read, unless it already does; any other page watches nothing.</summary>
    public void Follow(PageDefinition? page, ImmutableArray<ImmutableArray<string>> keys, Func<Invalidation, Task> refresh)
    {
        var wanted = _source is not null && page is { Live: true } ? PageKeys.All(keys) : [];
        if (_stop is not null && wanted.SequenceEqual(_keys, StringComparer.Ordinal))
        {
            return;
        }

        Stop();
        if (_source is null || wanted.IsEmpty)
        {
            return;
        }

        var watching = new CancellationTokenSource();
        (_keys, _stop) = (wanted, () =>
        {
            watching.Cancel();
            watching.Dispose();
        });
        _ = RunAsync(_source.WatchAsync(wanted, watching.Token), refresh, _failed, watching.Token);
    }

    public void Stop()
    {
        _stop?.Invoke();
        (_stop, _keys) = (null, []);
    }

    private static async Task RunAsync(IAsyncEnumerable<Invalidation> invalidations, Func<Invalidation, Task> refresh, Func<Exception, Task> failed, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var invalidation in invalidations)
            {
                await refresh(invalidation);
            }
        }
        catch (Exception ended) when (ended is SignInRequiredException || (ended is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            if (ended is SignInRequiredException)
            {
                await failed(ended);
            }
        }
    }
}
