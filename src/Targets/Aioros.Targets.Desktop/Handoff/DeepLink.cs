using System.Collections.Immutable;
using System.Threading.Channels;
using Aioros.Pages;
using Aioros.Routing.Tables;

namespace Aioros.Targets.Desktop.Handoff;

/// <summary>
/// The installed app's deep links, <c>{scheme}://open?path=/orders/42</c>. A deep link is not authenticated (any website
/// or app can invoke a scheme, and another app can claim it, RFC 8252 section 8.1), so it carries only a navigation path,
/// checked against the app's routes, never a token, a code or a cookie.
/// </summary>
public static class DeepLink
{
    public const string Host = "open";

    private const string PathKey = "path=";

    /// <summary>The routes of the app's pages, which a deep link's path must match.</summary>
    public static RouteTable Routes(IEnumerable<PageDefinition> pages) => pages.Aggregate(RouteTable.Empty, static (table, page) => table.Add(page.Route));

    /// <summary>The path a deep link opens, or <see langword="null"/> when it is not one of the app's deep links to a known page.</summary>
    public static string? Path(string link, string scheme, RouteTable routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase)
            || uri.UserInfo.Length > 0 || !uri.IsDefaultPort && uri.Port >= 0 || uri.Fragment.Length > 0
            || uri.AbsolutePath is not ("/" or "")
            || !uri.Query.StartsWith("?" + PathKey, StringComparison.Ordinal) || uri.Query.Contains('&', StringComparison.Ordinal))
        {
            return null;
        }

        var path = Uri.UnescapeDataString(uri.Query[(1 + PathKey.Length)..]);
        return IsLocal(path) && routes.Match(path.Split('?', 2)[0]).IsMatch ? path : null;
    }

    /// <summary>
    /// The first path of the app started with <paramref name="args"/>: the first accepted deep link's, otherwise
    /// <paramref name="start"/>; <c>Rejected</c> names the deep links that were refused.
    /// </summary>
    public static (string Path, string? Rejected) Start(IEnumerable<string> args, string scheme, RouteTable routes, string start)
    {
        var links = args.Where(arg => IsLink(arg, scheme)).ToList();
        var accepted = links.Select(link => Path(link, scheme, routes)).FirstOrDefault(static path => path is not null);
        var rejected = links.Where(link => Path(link, scheme, routes) is null).ToList();
        return (accepted ?? start, rejected.Count == 0 ? null : "Ignored deep links that open no page of the app: " + string.Join(", ", rejected));
    }

    /// <summary>
    /// Follows the arguments other instances forward (<see cref="SingleInstance"/>) until <paramref name="cancellationToken"/>
    /// is cancelled: <paramref name="open"/> gets each forward's first accepted path, or <see langword="null"/> when it
    /// carries none, so the window can still come to the front.
    /// </summary>
    public static async Task FollowAsync(ChannelReader<ImmutableArray<string>> forwarded, string scheme, RouteTable routes, Func<string?, Task> open, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(forwarded);
        ArgumentNullException.ThrowIfNull(open);
        try
        {
            await foreach (var args in forwarded.ReadAllAsync(cancellationToken))
            {
                await open(args.Where(arg => IsLink(arg, scheme)).Select(link => Path(link, scheme, routes)).FirstOrDefault(static path => path is not null));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static bool IsLink(string arg, string scheme) => arg.StartsWith(scheme + ":", StringComparison.OrdinalIgnoreCase);

    private static bool IsLocal(string path) =>
        path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal) && !path.Contains('\\', StringComparison.Ordinal) && !path.Any(char.IsControl);
}
