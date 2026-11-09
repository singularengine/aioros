using System.Collections.Immutable;
using System.Globalization;

namespace Aioros.Pages;

public sealed record PageRequest(string Path, ImmutableDictionary<string, string> RouteValues, ImmutableDictionary<string, string> Query)
{
    /// <summary>The signed-in user, or an unauthenticated principal for an anonymous visitor.</summary>
    public System.Security.Claims.ClaimsPrincipal User { get; init; } = new();

    /// <summary>
    /// The request's cookies by their exact name, as Next.js's <c>cookies()</c> gives them to a layout (shadcn/ui's sidebar
    /// reads <c>sidebar_state</c> there); empty on the native targets. A loader that reads them is never cached.
    /// </summary>
    public ImmutableDictionary<string, string> Cookies { get; init; } = ImmutableDictionary<string, string>.Empty;

    public static PageRequest For(string path) =>
        new(path, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty);

    public string? Value(string name) => RouteValues.GetValueOrDefault(name) ?? Query.GetValueOrDefault(name);

    public int Number(string name) =>
        int.TryParse(Value(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
