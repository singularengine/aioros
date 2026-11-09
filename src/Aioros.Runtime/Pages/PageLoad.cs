using System.Collections.Immutable;
using System.Threading.Channels;
using Aioros.Runtime.Functions;

namespace Aioros.Runtime.Pages;

/// <summary>
/// What the loaders of one page load share, outside their own scopes: the memo of their <c>[Memo]</c> calls, which holds
/// only results (React <c>cache</c>, one memo per server request), and the limits on how many of them run at once. One
/// load is a request's load, a navigation's or one revalidation's, so nothing it memoized outlives it.
/// </summary>
internal sealed class PageLoad(LoaderLimits limits) : ICallMemo
{
    private ImmutableDictionary<(string Function, object Arguments), Task> _calls = ImmutableDictionary<(string Function, object Arguments), Task>.Empty;

    public static PageLoad For(IServiceProvider services) => new(services.GetService(typeof(LoaderLimits)) as LoaderLimits ?? LoaderLimits.Default);

    /// <summary>The gate of a set of loaders that start together: none when they fit the limits.</summary>
    public LoaderGate? Gate(int runs)
    {
        var own = limits.PerLoad > 0 && runs > limits.PerLoad;
        return own || limits.GlobalTokens is not null ? new LoaderGate(own ? LoaderLimits.Tokens(limits.PerLoad) : null, limits.GlobalTokens) : null;
    }

    public Task<TResult> CallAsync<TResult>(string function, object arguments, Func<Task<TResult>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var key = (function, arguments);
        if (Volatile.Read(ref _calls).TryGetValue(key, out var known))
        {
            return (Task<TResult>)known;
        }

        var source = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var winner = ImmutableInterlocked.GetOrAdd(ref _calls, key, source.Task);
        if (!ReferenceEquals(winner, source.Task))
        {
            return (Task<TResult>)winner;
        }

        _ = CompleteAsync(source, call);
        return source.Task;
    }

    private static async Task CompleteAsync<TResult>(TaskCompletionSource<TResult> source, Func<Task<TResult>> call)
    {
        try
        {
            source.SetResult(await call().ConfigureAwait(false));
        }
        catch (OperationCanceledException canceled)
        {
            source.SetCanceled(canceled.CancellationToken);
        }
#pragma warning disable CA1031
        catch (Exception failure)
#pragma warning restore CA1031
        {
            source.SetException(failure);
        }
    }
}

/// <summary>
/// The tokens the loaders of a load take before they run and give back when they end: the load's own, when it has more
/// loaders than its limit, and the app's, when it sets a global limit. A loader waiting for a token waits on the
/// channel's reader, as a request waits for a pooled connection.
/// </summary>
internal sealed class LoaderGate(Channel<bool>? own, Channel<bool>? global)
{
    public async ValueTask EnterAsync(CancellationToken cancellationToken)
    {
        if (own is not null)
        {
            await own.Reader.ReadAsync(cancellationToken);
        }

        if (global is null)
        {
            return;
        }

        try
        {
            await global.Reader.ReadAsync(cancellationToken);
        }
        catch
        {
            own?.Writer.TryWrite(true);
            throw;
        }
    }

    public void Leave()
    {
        global?.Writer.TryWrite(true);
        own?.Writer.TryWrite(true);
    }
}
