using Aioros.Targets.Ssr.Documents;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// Redirects a page's answer; the navigation script gets a redirect after a post to another site as a header, since a
/// fetch cannot follow it there.
/// </summary>
internal static class PageRedirects
{
    public static void Redirect(HttpContext http, int status, string location)
    {
        if (status == StatusCodes.Status303SeeOther && http.Request.Headers[DocumentParts.NavigationHeader] == "1" && Elsewhere(http.Request, location))
        {
            http.Response.StatusCode = StatusCodes.Status200OK;
            http.Response.Headers[PageHandler.RedirectHeader] = location;
            return;
        }

        http.Response.StatusCode = status;
        http.Response.Headers.Location = location;
    }

    /// <summary>
    /// Whether a location is a path on this site: it starts with one slash, and holds no backslash or control character,
    /// so a browser cannot read it as protocol-relative (<c>//host</c>, <c>/\host</c>), as ASP.NET Core's <c>IsLocalUrl</c>.
    /// </summary>
    public static bool IsLocal(string path) =>
        path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal) && !path.Contains('\\', StringComparison.Ordinal) && !path.Any(char.IsControl);

    private static bool Elsewhere(HttpRequest request, string location) =>
        Uri.TryCreate(location, UriKind.Absolute, out var target)
        && (target.Scheme == Uri.UriSchemeHttp || target.Scheme == Uri.UriSchemeHttps)
        && !(string.Equals(target.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(target.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase));
}
