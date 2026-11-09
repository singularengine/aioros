using System.Collections.Immutable;
using System.Security.Cryptography;
using Aioros.Pages;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Live;

/// <summary>
/// The live connections whose models their page's posts act on, by the name each stream opens with, as a LiveView's
/// events reach its socket's own process: a post naming an open connection of its page and its user is handed to that
/// connection, whose stream runs it on its models between its pushes, so its changes reach that connection alone. Each
/// connection adds and removes itself; posts read an immutable snapshot.
/// </summary>
public sealed class LiveStates
{
    /// <summary>The header on the script's posts naming the page's live connection.</summary>
    public const string Header = "X-Aioros-Live";

    /// <summary>The header on an answer whose post ran on the connection it named, so the script keeps its stream.</summary>
    public const string KeptHeader = "X-Aioros-Live-Kept";

    private ImmutableDictionary<string, Held> _open = ImmutableDictionary.Create<string, Held>(StringComparer.Ordinal);

    /// <summary>How many connections are open.</summary>
    public int Count => Volatile.Read(ref _open).Count;

    /// <summary>Names a connection of <paramref name="page"/> for the stream's user, open until <see cref="Close"/>.</summary>
    internal string Open(HttpContext http, PageDefinition page, LiveConnection connection)
    {
        var name = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        ImmutableInterlocked.TryAdd(ref _open, name, new Held(page, http.User.Identity?.Name, connection));
        return name;
    }

    /// <summary>Lets the connection go once its stream ended.</summary>
    public void Close(string name) => ImmutableInterlocked.TryRemove(ref _open, name, out _);

    /// <summary>The open connection a post names, when it is of the same page and the same user; nothing for any other request.</summary>
    internal LiveConnection? Of(HttpContext http, PageDefinition page) =>
        HttpMethods.IsPost(http.Request.Method) && http.Request.Headers[Header].ToString() is { Length: > 0 } name
        && Volatile.Read(ref _open).TryGetValue(name, out var held) && ReferenceEquals(held.Page, page) && held.User == http.User.Identity?.Name
            ? held.Connection
            : null;

    private sealed record Held(PageDefinition Page, string? User, LiveConnection Connection);
}
