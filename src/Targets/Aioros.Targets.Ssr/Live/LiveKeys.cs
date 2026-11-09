using System.Collections.Immutable;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aioros.Targets.Ssr.Live;

/// <summary>
/// <c>GET /_live?keys=…</c>: the installed app's one stream of the data keys its live page's views read, as a long-lived
/// <c>X-Aioros-Revalidate</c>. It sends only the names of the keys it was asked for when they are invalidated, never data,
/// so the app refetches them through <c>/_data</c>, an ordinary authorized request where sign-out, revocation and access
/// rules apply (React Router's <c>useRevalidator</c> on a socket message, Turbo 8's refresh signal, htmx
/// <c>hx-trigger="sse:…"</c>).
/// </summary>
public static class LiveKeys
{
    public const string Path = RemoteLiveKeys.Path;

    public static IEndpointRouteBuilder MapLiveKeys(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Path, Stream);
        return endpoints;
    }

    private static IResult Stream(HttpContext http)
    {
        var keys = Keys(http.Request.QueryString.Value);
        if (keys.IsEmpty || keys.Count > RemoteLiveKeys.MaxKeys || http.RequestServices.GetService<DataVersions>() is not { } versions)
        {
            return TypedResults.BadRequest();
        }

        var stopping = http.RequestServices.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None;
        return TypedResults.ServerSentEvents(EventsAsync(versions, keys, http.RequestAborted, stopping));
    }

    /// <summary>The keys of the raw query, unescaped one by one, so a key may hold an escaped comma.</summary>
    private static ImmutableHashSet<string> Keys(string? query) =>
        query?.TrimStart('?').Split('&').FirstOrDefault(static part => part.StartsWith("keys=", StringComparison.Ordinal)) is { Length: > 5 } named
            ? RemoteLiveKeys.Parse(named[5..])
            : [];

    private static async IAsyncEnumerable<SseItem<string>> EventsAsync(DataVersions versions, ImmutableHashSet<string> keys, CancellationToken aborted, CancellationToken stopping, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(aborted, stopping, cancellationToken);
        var invalidations = versions.WatchAsync(stop.Token);
        yield return new SseItem<string>(string.Empty, RemoteLiveKeys.OpenEvent);
        await foreach (var invalidation in invalidations.ConfigureAwait(false))
        {
            var named = invalidation.All ? keys : keys.Intersect(invalidation.Keys);
            if (!named.IsEmpty)
            {
                yield return new SseItem<string>(string.Join(',', named.Order(StringComparer.Ordinal).Select(Uri.EscapeDataString)), RemoteLiveKeys.KeysEvent);
            }
        }
    }
}
