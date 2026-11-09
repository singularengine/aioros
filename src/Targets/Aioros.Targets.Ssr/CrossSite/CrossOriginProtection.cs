using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.CrossSite;

/// <summary>
/// Refuses requests a browser sent from another origin, by Fetch Metadata and <c>Origin</c>, as Go 1.25's
/// <c>net/http.CrossOriginProtection</c>: safe methods (<c>GET</c>, <c>HEAD</c>, <c>OPTIONS</c>) always pass; otherwise
/// <c>Sec-Fetch-Site</c> must be <c>same-origin</c> or <c>none</c>; without it, an <c>Origin</c> must name the request's
/// host and port; a request with neither header was not sent cross-site by a browser and passes. Page posts and server
/// function calls go through it, so no antiforgery token is needed. Register one with
/// <c>services.AddSingleton(new CrossOriginProtection { ... })</c> to trust origins or exempt routes.
/// </summary>
public sealed class CrossOriginProtection
{
    /// <summary>The Fetch Metadata request header the browser sets on every request.</summary>
    public const string FetchSiteHeader = "Sec-Fetch-Site";

    private readonly ImmutableHashSet<string> _trusted = ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The protection without trusted origins or exempt routes.</summary>
    public static CrossOriginProtection Default { get; } = new();

    /// <summary>
    /// Origins (<c>scheme://host[:port]</c>, without path, query or fragment) whose cross-origin requests are accepted,
    /// as Go's <c>AddTrustedOrigin</c>.
    /// </summary>
    public ImmutableHashSet<string> TrustedOrigins
    {
        get => _trusted;
        init => _trusted = value.Select(Validated).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Routes, as declared (a page's route such as <c>/hooks/{id}</c>, or a server function's <c>/_sf/{group}/{name}</c>),
    /// exempt from the check, as Go's <c>AddInsecureBypassPattern</c>.
    /// </summary>
    public ImmutableHashSet<string> InsecureBypassPatterns { get; init; } = ImmutableHashSet.Create<string>(StringComparer.Ordinal);

    /// <summary>Whether a request to <paramref name="route"/> may run.</summary>
    public bool Allows(HttpRequest request, string route)
    {
        ArgumentNullException.ThrowIfNull(request);
        var method = request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return true;
        }

        var origin = request.Headers.Origin.ToString();
        var site = request.Headers[FetchSiteHeader].ToString();
        if (site.Length > 0)
        {
            return site is "same-origin" or "none" || Exempt(origin, route);
        }

        return origin.Length == 0 || SameHost(origin, request.Host) || Exempt(origin, route);
    }

    private bool Exempt(string origin, string route) =>
        InsecureBypassPatterns.Contains(route) || (origin.Length > 0 && _trusted.Contains(origin));

    private static bool SameHost(string origin, HostString host)
    {
        var separator = origin.IndexOf("://", StringComparison.Ordinal);
        return separator > 0 && host.Value is { Length: > 0 } value && origin.AsSpan(separator + 3).Equals(value, StringComparison.OrdinalIgnoreCase);
    }

    private static string Validated(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Host.Length > 0
        && string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase)
            ? origin
            : throw new ArgumentException($"The trusted origin '{origin}' must be scheme://host[:port], without a path, a query or a fragment.", nameof(origin));
}
