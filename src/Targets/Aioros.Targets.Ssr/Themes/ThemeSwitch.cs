using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Themes;

/// <summary>
/// The visitor's theme: <c>light</c> or <c>dark</c> as chosen, or <c>system</c>, which follows the device's color scheme
/// as the browser reports it in the <c>Sec-CH-Prefers-Color-Scheme</c> client hint (asked for with <c>Accept-CH</c>, and
/// with <c>Critical-CH</c> so the first request is retried with it), light when the browser sends none. A page is always
/// rendered with the light or the dark plan: <c>system</c> resolves to one of them per request.
/// </summary>
public static class ThemeSwitch
{
    public const string Cookie = "theme";
    public const string Light = "light";
    public const string Dark = "dark";

    /// <summary>The choice that follows the device's color scheme.</summary>
    public const string SystemPreference = "system";

    /// <summary>The client hint with the device's color scheme (<c>"light"</c> or <c>"dark"</c>).</summary>
    public const string Hint = "Sec-CH-Prefers-Color-Scheme";

    /// <summary>
    /// The cookie the framework's script keeps with the device's color scheme, read for <see cref="SystemPreference"/>
    /// when a browser sends no <see cref="Hint"/> (Firefox and Safari).
    /// </summary>
    public const string SystemCookie = "theme-system";

    /// <summary>
    /// The theme a request is rendered with: the chosen one, or the device's for <see cref="SystemPreference"/>, read from
    /// the hint, else from <see cref="SystemCookie"/>.
    /// </summary>
    public static string Of(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var request = http.Request;
        return Preference(http) switch
        {
            Dark => Dark,
            SystemPreference => (request.Headers.TryGetValue(Hint, out var hint) ? hint.ToString().Trim().Trim('"') : request.Cookies[SystemCookie]) == Dark ? Dark : Light,
            _ => Light,
        };
    }

    /// <summary>
    /// Asks the browser for the color-scheme hint on an answer to a visitor who follows the system: <c>Accept-CH</c> for
    /// the next requests, <c>Critical-CH</c> so a request that came without it is retried with it, and <c>Vary</c>.
    /// </summary>
    public static void AskForHint(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (Preference(http) != SystemPreference)
        {
            return;
        }

        if (http.Request.Cookies[Cookie] is null)
        {
            Remember(http, SystemPreference);
        }

        var headers = http.Response.Headers;
        headers["Accept-CH"] = Hint;
        headers["Critical-CH"] = Hint;
        headers.Vary = headers.Vary.Count == 0 ? Hint : headers.Vary + ", " + Hint;
    }

    /// <summary>
    /// Switches the theme and goes back to the page that asked; another site's request (<c>Sec-Fetch-Site</c>
    /// <c>cross-site</c> or <c>same-site</c>) changes nothing and goes home, since a safe method should not change state.
    /// </summary>
    public static IResult Switch(HttpContext http, string name)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (http.Request.Headers[CrossSite.CrossOriginProtection.FetchSiteHeader].ToString() is { Length: > 0 } site && site is not ("same-origin" or "none"))
        {
            return Results.Redirect("/");
        }

        var next = name switch
        {
            "toggle" => Of(http) == Dark ? Light : Dark,
            Dark => Dark,
            SystemPreference => SystemPreference,
            _ => Light,
        };
        Remember(http, next);
        if (next == SystemPreference)
        {
            http.Response.Headers["Accept-CH"] = Hint;
        }

        return Results.Redirect(BackTo(http));
    }

    /// <summary>The visitor's choice, or the app's default theme for a visitor who has made none (<see cref="Hosting.SsrOptions.Theme"/>).</summary>
    private static string Preference(HttpContext http) =>
        http.Request.Cookies[Cookie] ?? (http.RequestServices?.GetService(typeof(Hosting.SsrOptions)) as Hosting.SsrOptions)?.Theme ?? Light;

    /// <summary>Keeps the theme in the visitor's cookie, which the framework's script reads to follow the device while it is <see cref="SystemPreference"/>.</summary>
    private static void Remember(HttpContext http, string theme)
    {
#pragma warning disable S2092, S3330
        http.Response.Cookies.Append(Cookie, theme, new CookieOptions { Path = "/", MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax, HttpOnly = false, Secure = Hosting.ProxiedRequests.SecureCookies(http.Request) });
#pragma warning restore S2092, S3330
    }

    private static string BackTo(HttpContext http)
    {
        var referer = http.Request.Headers.Referer.ToString();
        return Uri.TryCreate(referer, UriKind.Absolute, out var uri) && string.Equals(uri.Authority, http.Request.Host.Value, StringComparison.OrdinalIgnoreCase)
            && Pages.PageRedirects.IsLocal(uri.PathAndQuery)
            ? uri.PathAndQuery
            : "/";
    }
}
