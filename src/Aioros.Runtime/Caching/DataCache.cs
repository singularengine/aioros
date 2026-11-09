using System.Collections.Immutable;
using System.Security.Claims;
using Aioros.Runtime.Functions;

namespace Aioros.Runtime.Caching;

/// <summary>
/// The results of <c>[Cached]</c> server functions, kept across requests for the loaders of later loads, as Next.js's
/// <c>"use cache"</c> keeps a read until <c>revalidateTag</c> or its <c>cacheLife</c> ends it: keyed by the function, its
/// arguments and, unless the function is shared, the signed-in user; an entry is served while its lifetime lasts on the
/// app's <see cref="TimeProvider"/> and none of the keys it read was invalidated since it started (<see cref="DataVersions"/>).
/// Equal calls share one run, a failure is never kept, and a call outside a load (a command's) or, for data per user,
/// from a visitor without an identity, goes straight to the implementation. The entries are one immutable dictionary
/// swapped with <see cref="ImmutableInterlocked"/>; past its limit the entries already expired or invalidated go first,
/// then those that expire soonest, down to three quarters of the limit, as <c>MemoryCache.Compact</c> removes expired
/// entries before the oldest by expiration, so a full cache compacts once in a while instead of on every insert.
/// </summary>
public sealed class DataCache(DataVersions versions, TimeProvider time, int maxEntries = DataCache.DefaultMaxEntries)
{
    public const int DefaultMaxEntries = 4096;

    private readonly int _maxEntries = maxEntries;

    private ImmutableDictionary<Key, Entry> _entries = ImmutableDictionary<Key, Entry>.Empty;

    public DataVersions Versions { get; } = versions;

    /// <summary>How many entries are kept, served or not.</summary>
    public int Count => Volatile.Read(ref _entries).Count;

    /// <summary>
    /// Calls a cached function: from its entry while it is current, otherwise through <paramref name="call"/>, whose result
    /// is kept when it succeeds. <paramref name="caller"/> is the load's services (none outside a load), which know the user.
    /// </summary>
    public Task<TResult> CallAsync<TResult>(string function, object arguments, object? caller, TimeSpan lifetime, bool shared, ImmutableArray<string> keys, Func<Task<TResult>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        string? user = null;
        if (caller is null || (!shared && (user = Identity(caller)) is null))
        {
            return call();
        }

        var key = new Key(function, arguments, user);
        var now = time.GetUtcNow();
        if (Volatile.Read(ref _entries).TryGetValue(key, out var known) && Live(known, now))
        {
            return (Task<TResult>)known.Result;
        }

        var source = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entry = new Entry(source.Task, Versions.Clock, keys, now + lifetime);
        var winner = ImmutableInterlocked.AddOrUpdate(ref _entries, key, entry, (_, existing) => Live(existing, now) ? existing : entry);
        if (!ReferenceEquals(winner, entry))
        {
            return (Task<TResult>)winner.Result;
        }

        if (Volatile.Read(ref _entries).Count > _maxEntries)
        {
            Compact(key, now);
        }

        _ = CompleteAsync(source, call, key, entry);
        return source.Task;
    }

    /// <summary>Marks every entry that read <paramref name="key"/> stale, after a function invalidated it.</summary>
    public void Invalidate(string key) => Versions.Invalidate(key);

    /// <summary>Marks every entry stale, after a function without key information changed something.</summary>
    public void InvalidateAll() => Versions.InvalidateAll();

    /// <summary>The signed-in user a load runs for, as a key part: the authentication type and the user's identifier or name.</summary>
    private static string? Identity(object caller) =>
        caller is ICallerIdentity { User: { Identity: { IsAuthenticated: true } identity } user }
        && (user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? identity.Name) is { Length: > 0 } id
            ? identity.AuthenticationType + "\n" + id
            : null;

    private bool Live(Entry entry, DateTimeOffset now) =>
        now < entry.Expires && !entry.Result.IsFaulted && !entry.Result.IsCanceled && Versions.Current(entry.Stamp, entry.Keys);

    private void Compact(Key added, DateTimeOffset now) => ImmutableInterlocked.Update(ref _entries, static (entries, state) =>
    {
        if (entries.Count <= state.Cache._maxEntries)
        {
            return entries;
        }

        var kept = entries.ToBuilder();
        foreach (var (key, entry) in entries)
        {
            if (!state.Cache.Live(entry, state.Now))
            {
                kept.Remove(key);
            }
        }

        var target = state.Cache._maxEntries * 3 / 4;
        if (kept.Count > state.Cache._maxEntries && kept.Count - 1 > target)
        {
            foreach (var soonest in kept.Where(pair => !pair.Key.Equals(state.Added)).OrderBy(static pair => pair.Value.Expires).Take(kept.Count - 1 - target).ToList())
            {
                kept.Remove(soonest.Key);
            }
        }

        return kept.ToImmutable();
    }, (Cache: this, Added: added, Now: now));

    private async Task CompleteAsync<TResult>(TaskCompletionSource<TResult> source, Func<Task<TResult>> call, Key key, Entry entry)
    {
        try
        {
            source.SetResult(await call().ConfigureAwait(false));
        }
        catch (OperationCanceledException canceled)
        {
            Forget(key, entry);
            source.SetCanceled(canceled.CancellationToken);
        }
#pragma warning disable CA1031
        catch (Exception failure)
#pragma warning restore CA1031
        {
            Forget(key, entry);
            source.SetException(failure);
        }
    }

    private void Forget(Key key, Entry entry) => ImmutableInterlocked.Update(ref _entries, static (entries, forgotten) =>
        entries.TryGetValue(forgotten.Key, out var current) && ReferenceEquals(current, forgotten.Entry) ? entries.Remove(forgotten.Key) : entries, (Key: key, Entry: entry));

    private readonly record struct Key(string Function, object Arguments, string? User);

    private sealed record Entry(Task Result, long Stamp, ImmutableArray<string> Keys, DateTimeOffset Expires);
}
