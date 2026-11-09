using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Aioros.Runtime.Caching;

/// <summary>
/// The versions of the app's data keys, which decide whether something kept is still current: a clock that every
/// invalidation advances, and for each invalidated key the clock's value when it was last invalidated. Whatever is kept
/// notes the clock before it starts reading (its stamp) and stays current while none of the keys it read was invalidated
/// after that, as Next.js's <c>revalidateTag</c> marks every entry carrying the tag stale. One immutable state swapped
/// with <see cref="ImmutableInterlocked"/>, so nothing locks; past <see cref="MaxKeys"/> keys the state starts again with
/// everything older than now invalidated, which bounds it. Every invalidation also reaches the watchers
/// (<see cref="WatchAsync"/>), each through a channel of its own, as Phoenix.PubSub delivers a broadcast to each subscribed
/// process: what live pages rerun.
/// </summary>
public sealed class DataVersions
{
    /// <summary>How many invalidated keys are remembered before everything kept until then is invalidated at once.</summary>
    public const int MaxKeys = 4096;

    private static readonly ImmutableDictionary<string, long> None = ImmutableDictionary.Create<string, long>(StringComparer.Ordinal);

    private State _state = new(0, 0, None);

    private ImmutableArray<ChannelWriter<string?>> _watchers = [];

    /// <summary>How many watchers receive the invalidations now.</summary>
    public int Watchers => _watchers.Length;

    /// <summary>The current clock: the stamp of a read that starts now.</summary>
    public long Clock => Volatile.Read(ref _state).Clock;

    /// <summary>Marks every read of <paramref name="key"/> stamped before now stale.</summary>
    public void Invalidate(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ImmutableInterlocked.Update(ref _state, static (state, key) =>
        {
            var clock = state.Clock + 1;
            return state.Keys.Count >= MaxKeys ? new State(clock, clock, None) : state with { Clock = clock, Keys = state.Keys.SetItem(key, clock) };
        }, key);
        Publish(key);
    }

    /// <summary>Marks everything stamped before now stale, as after a change whose keys are unknown.</summary>
    public void InvalidateAll()
    {
        ImmutableInterlocked.Update(ref _state, static state => new State(state.Clock + 1, state.Clock + 1, None));
        Publish(null);
    }

    /// <summary>
    /// Watches the invalidations from now until <paramref name="cancellationToken"/> is cancelled, when the watcher is
    /// removed at once and the sequence ends. The watcher is added before this returns, so nothing invalidated after the
    /// call is missed; the keys invalidated while the reader was busy arrive together, in one <see cref="Invalidation"/>.
    /// </summary>
    public IAsyncEnumerable<Invalidation> WatchAsync(CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<string?>(new UnboundedChannelOptions { SingleReader = true });
        ImmutableInterlocked.Update(ref _watchers, static (watchers, writer) => watchers.Add(writer), channel.Writer);
        var registration = cancellationToken.Register(static state =>
        {
            var (versions, writer) = ((DataVersions, ChannelWriter<string?>))state!;
            versions.Unwatch(writer);
        }, (this, channel.Writer));
        return ReadAsync(channel, registration, CancellationToken.None);
    }

    private async IAsyncEnumerable<Invalidation> ReadAsync(Channel<string?> channel, CancellationTokenRegistration registration, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return Drain(channel.Reader);
            }
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);
            Unwatch(channel.Writer);
        }
    }

    /// <summary>Everything the reader holds, as one invalidation.</summary>
    private static Invalidation Drain(ChannelReader<string?> reader)
    {
        var keys = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        var all = false;
        while (reader.TryRead(out var key))
        {
            all |= key is null;
            if (key is not null)
            {
                keys.Add(key);
            }
        }

        return new Invalidation(keys.ToImmutable(), all);
    }

    private void Unwatch(ChannelWriter<string?> writer)
    {
        ImmutableInterlocked.Update(ref _watchers, static (watchers, writer) => watchers.Remove(writer), writer);
        writer.TryComplete();
    }

    private void Publish(string? key)
    {
        foreach (var watcher in _watchers)
        {
            watcher.TryWrite(key);
        }
    }

    /// <summary>Whether a read stamped <paramref name="stamp"/> that read <paramref name="keys"/> is still current.</summary>
    public bool Current(long stamp, ImmutableArray<string> keys)
    {
        var state = Volatile.Read(ref _state);
        if (stamp < state.Floor)
        {
            return false;
        }

        if (keys.IsDefaultOrEmpty || state.Keys.IsEmpty)
        {
            return true;
        }

        foreach (var key in keys)
        {
            if (state.Keys.TryGetValue(key, out var version) && version > stamp)
            {
                return false;
            }
        }

        return true;
    }

    private sealed record State(long Clock, long Floor, ImmutableDictionary<string, long> Keys);
}

/// <summary>
/// The keys invalidated since a watcher last read, or everything (<see cref="All"/>) after a change whose keys are unknown.
/// </summary>
public sealed record Invalidation(ImmutableHashSet<string> Keys, bool All)
{
    /// <summary>Whether a load that read <paramref name="read"/> is stale after this invalidation.</summary>
    public bool Meets(ImmutableArray<string> read) => All || (!read.IsDefaultOrEmpty && read.Any(Keys.Contains));
}
