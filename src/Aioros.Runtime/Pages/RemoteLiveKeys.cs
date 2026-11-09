using System.Collections.Immutable;
using System.Net;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Aioros.Runtime.Caching;
using Aioros.Runtime.SignIn;

namespace Aioros.Runtime.Pages;

/// <summary>
/// The server's <c>GET /_live?keys=…</c> stream for the installed app: <c>keys</c> events whose data is the invalidated
/// keys, each escaped as a URI component and separated by commas, as <c>X-Aioros-Revalidate</c>. When the stream ends,
/// fails or the server answers <c>408</c>, <c>429</c> or <c>5xx</c>, it opens again after the wait
/// <see cref="LiveReconnection"/> says, at least the server's <c>Retry-After</c>, and once it
/// is open again reports everything invalidated, since what changed meanwhile is unknown (Turbo 8's refresh and
/// LiveView's remount on reconnect). A <c>401</c> that remains after the sign-in handler's refresh ends the watch with
/// <see cref="SignInRequiredException"/>; <c>204</c> and any other refusal end it quietly, as the HTML EventSource does.
/// </summary>
public sealed class RemoteLiveKeys(HttpClient http, LiveReconnection? reconnection = null) : ILiveKeys
{
    /// <summary>The path of the stream.</summary>
    public const string Path = "/_live";

    /// <summary>The most keys one stream watches.</summary>
    public const int MaxKeys = 256;

    /// <summary>The event that carries invalidated keys.</summary>
    public const string KeysEvent = "keys";

    /// <summary>The event the stream opens with, once the server watches.</summary>
    public const string OpenEvent = "open";

    private static readonly Invalidation Everything = new(ImmutableHashSet<string>.Empty, true);

    private readonly LiveReconnection _reconnection = reconnection ?? new LiveReconnection(TimeProvider.System);

    public IAsyncEnumerable<Invalidation> WatchAsync(ImmutableArray<string> keys, CancellationToken cancellationToken) => ReadAsync(keys, cancellationToken);

    /// <summary>The query of a stream for <paramref name="keys"/>: each key escaped as a URI component, separated by commas.</summary>
    public static string Address(IEnumerable<string> keys) => Path + "?keys=" + string.Join(',', keys.Select(Uri.EscapeDataString));

    /// <summary>The keys an event's data names.</summary>
    public static ImmutableHashSet<string> Parse(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Length == 0 ? [] : [.. data.Split(',').Select(Uri.UnescapeDataString)];
    }

    private async IAsyncEnumerable<Invalidation> ReadAsync(ImmutableArray<string> keys, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var address = new Uri(Address(keys), UriKind.Relative);
        var (attempt, retry, reopening) = (0, (TimeSpan?)null, false);
        while (true)
        {
            var (response, refused, floor) = await OpenAsync(address, cancellationToken).ConfigureAwait(false);
            if (refused)
            {
                yield break;
            }

            if (response is not null)
            {
                attempt = 0;
                var interval = new StrongBox<TimeSpan?>(retry);
                await foreach (var invalidation in EventsAsync(response, reopening, interval, cancellationToken).ConfigureAwait(false))
                {
                    yield return invalidation;
                }

                retry = interval.Value;
            }

            reopening = true;
            await _reconnection.WaitAsync(_reconnection.Delay(attempt++, retry), floor, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<Invalidation> EventsAsync(HttpResponseMessage response, bool reopening, StrongBox<TimeSpan?> retry, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var _ = response;
        await using var stream = await StreamAsync(response, cancellationToken).ConfigureAwait(false);
        var parser = SseParser.Create(stream);
        await using var events = parser.EnumerateAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (await NextAsync(events).ConfigureAwait(false))
        {
            if (events.Current.EventType == OpenEvent && reopening)
            {
                yield return Everything;
            }
            else if (events.Current.EventType == KeysEvent)
            {
                yield return new Invalidation(Parse(events.Current.Data), false);
            }
        }

        if (parser.ReconnectionInterval > TimeSpan.Zero)
        {
            retry.Value = parser.ReconnectionInterval;
        }
    }

    private async Task<(HttpResponseMessage? Response, bool Refused, TimeSpan Floor)> OpenAsync(Uri address, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Accept.ParseAdd("text/event-stream");
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException || (failure is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return (null, false, TimeSpan.Zero);
        }

        if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
        {
            return (response, false, TimeSpan.Zero);
        }

        var status = (int)response.StatusCode;
        var floor = _reconnection.RetryAfter(response.Headers.RetryAfter);
        response.Dispose();
        return status switch
        {
            401 => throw new SignInRequiredException("The server asks for a sign-in before it sends the page's live updates."),
            408 or 429 or >= 500 => (null, false, floor),
            _ => (null, true, TimeSpan.Zero),
        };
    }

    private static async Task<Stream> StreamAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException)
        {
            return Stream.Null;
        }
    }

    private static async Task<bool> NextAsync(IAsyncEnumerator<SseItem<string>> events)
    {
        try
        {
            return await events.MoveNextAsync().ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException)
        {
            return false;
        }
    }
}
