using Aioros.Runtime.SignIn;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.SignIn;

/// <summary>
/// The page a visitor came from, kept through a first install. "Open in the app" falls back to the download page with
/// <c>?return={path}</c>; the download page's answer keeps that path, checked against the app's pages, in a short-lived
/// cookie scoped to the authorization endpoint; the installed app's first sign-in, in the same browser, asks for it and
/// gets it in the authorization response, which deletes the cookie. Only a path travels, never a token, so the installer
/// holds no secret.
/// </summary>
public static class InstalledAppReturnPath
{
    /// <summary>The cookie that holds the path.</summary>
    public const string Cookie = ".Aioros.Return";

    /// <summary>The download link's query value that carries the path.</summary>
    public const string Query = "return";

    /// <summary>
    /// Keeps the path the download page's link carries; <paramref name="isPage"/> tells whether a path, without its query,
    /// is one of the app's pages.
    /// </summary>
    public static IApplicationBuilder UseInstalledAppReturnPath(this IApplicationBuilder app, string download, Func<string, bool> isPage)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(isPage);
        var options = app.ApplicationServices.GetRequiredService<InstalledAppSignInOptions>();
        return app.Use(async (http, next) =>
        {
            if (HttpMethods.IsGet(http.Request.Method)
                && string.Equals(http.Request.Path.Value, download, StringComparison.OrdinalIgnoreCase)
                && Accepted(http.Request.Query[Query].ToString(), isPage) is { } path)
            {
                http.Response.Cookies.Append(Cookie, path, CookieOptions(http, options, options.ReturnPathLifetime));
            }

            await next(http);
        });
    }

    /// <summary>The path the browser holds, deleted once read; <see langword="null"/> when it holds none or one that is not a page.</summary>
    internal static string? Take(HttpContext http, InstalledAppSignInOptions options, Func<string, bool>? isPage)
    {
        if (http.Request.Cookies[Cookie] is not { } held)
        {
            return null;
        }

        http.Response.Cookies.Delete(Cookie, CookieOptions(http, options, null));
        return isPage is null ? null : Accepted(held, isPage);
    }

    private static string? Accepted(string path, Func<string, bool> isPage) =>
        Pages.PageRedirects.IsLocal(path) && isPage(path.Split('?', 2)[0]) ? path : null;

#pragma warning disable S2092
    private static CookieOptions CookieOptions(HttpContext http, InstalledAppSignInOptions options, TimeSpan? lifetime) => new()
    {
        Path = http.Request.PathBase + options.AuthorizePath,
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = Hosting.ProxiedRequests.SecureCookies(http.Request),
        MaxAge = lifetime,
        IsEssential = true,
    };
#pragma warning restore S2092
}
