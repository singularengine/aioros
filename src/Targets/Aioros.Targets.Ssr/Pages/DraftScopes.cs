using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// The scope the draft script keeps a signed-in user's drafts under: an opaque cookie the page's script reads, sealed with
/// ASP.NET Core Data Protection, so it names nobody and the server can tell whose it is. A request of another user replaces
/// it, and one with no user removes it, so a browser never offers one person's edits to the next (decision "Drafts per user").
/// </summary>
public sealed class DraftScopes(IDataProtectionProvider provider)
{
    public const string CookieName = "aioros-draft-scope";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    private readonly IDataProtector _protector = provider.CreateProtector("Aioros.DraftScope");

    /// <summary>Makes the request's scope cookie the one of the request's user, or removes it when no one is signed in.</summary>
    public void Apply(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var present = http.Request.Cookies[CookieName];
        if (http.User.Identity?.IsAuthenticated != true)
        {
            if (present is not null)
            {
#pragma warning disable S2092, S3330
                http.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
#pragma warning restore S2092, S3330
            }

            return;
        }

        var user = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.Identity.Name ?? string.Empty;
        if (present is null || !Holds(present, user))
        {
#pragma warning disable S2092, S3330
            http.Response.Cookies.Append(CookieName, _protector.Protect(user), new CookieOptions { Path = "/", SameSite = SameSiteMode.Lax, Secure = Hosting.ProxiedRequests.SecureCookies(http.Request), MaxAge = Lifetime });
#pragma warning restore S2092, S3330
        }
    }

    private bool Holds(string cookie, string user)
    {
        try
        {
            return string.Equals(_protector.Unprotect(cookie), user, StringComparison.Ordinal);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
