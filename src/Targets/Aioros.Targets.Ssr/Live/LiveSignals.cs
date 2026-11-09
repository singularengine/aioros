using System.Collections.Immutable;
using System.ComponentModel;
using System.Threading.Channels;
using Aioros.Runtime.Caching;

namespace Aioros.Targets.Ssr.Live;

/// <summary>
/// What wakes one live connection: its page model's own changes and the data keys invalidated since it last rendered,
/// merged into one pending invalidation. A bounded channel of one coalesces the wake-ups, so a burst renders once, as
/// Turbo 8 debounces the refreshes of a burst of broadcasts. One connection owns it; the feed writes into it from the
/// thread that invalidates.
/// </summary>
internal sealed class LiveSignals : IDisposable
{
    private readonly Channel<bool> _signals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private Invalidation? _pending;

    private int _changed;

    private INotifyPropertyChanged? _observed;

    public ChannelReader<bool> Reader => _signals.Reader;

    /// <summary>Follows the changes of the page's model, after it was created again by a refresh too.</summary>
    public void Observe(object? model)
    {
        if (ReferenceEquals(model, _observed))
        {
            return;
        }

        if (_observed is not null)
        {
            _observed.PropertyChanged -= Changed;
        }

        _observed = model as INotifyPropertyChanged;
        if (_observed is not null)
        {
            _observed.PropertyChanged += Changed;
        }
    }

    /// <summary>Takes the invalidations into the pending one until they end.</summary>
    public async Task FollowAsync(IAsyncEnumerable<Invalidation> invalidations)
    {
        await foreach (var invalidation in invalidations.ConfigureAwait(false))
        {
            ImmutableInterlocked.Update(ref _pending, static (pending, next) => pending is null ? next : new Invalidation(pending.Keys.Union(next.Keys), pending.All || next.All), invalidation);
            _signals.Writer.TryWrite(true);
        }
    }

    /// <summary>The keys invalidated since the last call, if any.</summary>
    public Invalidation? TakeInvalidation() => Interlocked.Exchange(ref _pending, null);

    /// <summary>Whether the page's model changed since the last call.</summary>
    public bool TakeChange() => Interlocked.Exchange(ref _changed, 0) == 1;

    /// <summary>Wakes the connection for work handed to it.</summary>
    public void Wake() => _signals.Writer.TryWrite(true);

    public void Dispose() => Observe(null);

    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        Volatile.Write(ref _changed, 1);
        _signals.Writer.TryWrite(true);
    }
}
