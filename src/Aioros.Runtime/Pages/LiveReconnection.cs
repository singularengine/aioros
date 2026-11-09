using System.Net.Http.Headers;

namespace Aioros.Runtime.Pages;

/// <summary>
/// When the installed app opens its live stream again: after a wait drawn at random below a bound that doubles from
/// 1 s, or from the server's <c>retry:</c> interval, up to 30 s (AWS's "full jitter", the HTML EventSource reconnection
/// time, SignalR's and Phoenix's backoff), and at once when <see cref="Wake"/> says the network came back or the window
/// was reactivated (React Query's <c>refetchOnReconnect</c> and <c>refetchOnWindowFocus</c>). A server's
/// <c>Retry-After</c> (RFC 9110) is the least wait, up to 5 minutes, and no wake shortens it. It waits on the
/// <see cref="TimeProvider"/> it is given, so a test drives it with a fake clock.
/// </summary>
public sealed class LiveReconnection(TimeProvider time, Func<double>? random = null)
{
    /// <summary>The bound of the first wait when the server sent no <c>retry:</c> interval.</summary>
    public static readonly TimeSpan First = TimeSpan.FromSeconds(1);

    /// <summary>The largest bound, unless the server's <c>retry:</c> interval is larger.</summary>
    public static readonly TimeSpan Cap = TimeSpan.FromSeconds(30);

    /// <summary>The longest wait a server's <c>Retry-After</c> imposes.</summary>
    public static readonly TimeSpan RetryAfterCap = TimeSpan.FromMinutes(5);

    private readonly Func<double> _random = random ?? Random.Shared.NextDouble;

    private TaskCompletionSource? _waking;

    /// <summary>The wait before attempt <paramref name="attempt"/>, counted from 0: a random part of <c>min(cap, base × 2ⁿ)</c>.</summary>
    public TimeSpan Delay(int attempt, TimeSpan? retry = null)
    {
        var first = retry is { Ticks: > 0 } interval ? interval : First;
        var cap = first > Cap ? first : Cap;
        var bound = Math.Min(cap.Ticks, first.Ticks * Math.Pow(2, Math.Max(attempt, 0)));
        return TimeSpan.FromTicks((long)(Math.Clamp(_random(), 0, 1) * bound));
    }

    /// <summary>
    /// Ends the current wait at once: the network became available or the window was reactivated. Returns whether it
    /// ended one; outside a wait, or while the server's <c>Retry-After</c> lasts, it does nothing.
    /// </summary>
    public bool Wake() => Interlocked.Exchange(ref _waking, null)?.TrySetResult() ?? false;

    /// <summary>
    /// The wait a <c>Retry-After</c> header asks for, as delta-seconds or an HTTP-date on the clock, at most
    /// <see cref="RetryAfterCap"/>; none for no header or a date already past.
    /// </summary>
    public TimeSpan RetryAfter(RetryConditionHeaderValue? header)
    {
        var wait = header switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - time.GetUtcNow(),
            _ => TimeSpan.Zero,
        };
        return wait <= TimeSpan.Zero ? TimeSpan.Zero : wait > RetryAfterCap ? RetryAfterCap : wait;
    }

    /// <summary>Waits <paramref name="delay"/>, or until <see cref="Wake"/>; a wake from before the wait does not count.</summary>
    public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => WaitAsync(delay, TimeSpan.Zero, cancellationToken);

    /// <summary>
    /// Waits <paramref name="floor"/>, which no wake shortens (the server's <c>Retry-After</c>), then what remains of
    /// <paramref name="delay"/>, or until <see cref="Wake"/>.
    /// </summary>
    public async Task WaitAsync(TimeSpan delay, TimeSpan floor, CancellationToken cancellationToken)
    {
        if (floor > TimeSpan.Zero)
        {
            await Task.Delay(floor, time, cancellationToken).ConfigureAwait(false);
            delay -= floor;
            if (delay <= TimeSpan.Zero)
            {
                return;
            }
        }

        var waking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _waking, waking);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var elapsed = Task.Delay(delay, time, waiting.Token);
        await Task.WhenAny(elapsed, waking.Task).ConfigureAwait(false);
        Interlocked.CompareExchange(ref _waking, null, waking);
        await waiting.CancelAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
