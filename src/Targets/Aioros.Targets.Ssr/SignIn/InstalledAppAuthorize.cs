using System.Security.Claims;
using Aioros.Runtime.SignIn;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Aioros.Targets.Ssr.SignIn;

/// <summary>
/// The browser's step of the installed app's sign-in: the visitor signs in with the app's cookie (the challenge sends an
/// anonymous one to the app's sign-in route and back), then the code is issued for a principal that carries the
/// cookie's claims, so the app's code reads the same user either way.
/// </summary>
internal static class InstalledAppAuthorize
{
    public static async Task<IResult> AuthorizeAsync(HttpContext http, InstalledAppSignInOptions options, Func<string, bool>? pages)
    {
        var request = http.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("The authorization request could not be read.");
        var cookie = await http.AuthenticateAsync(options.CookieScheme);
        if (cookie is not { Succeeded: true, Principal: { } user } || user.FindFirst(ClaimTypes.NameIdentifier) is not { } id)
        {
            return Results.Challenge(new AuthenticationProperties { RedirectUri = http.Request.PathBase + http.Request.Path + http.Request.QueryString }, [options.CookieScheme]);
        }

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.AddClaims(user.Claims.Where(static claim => claim.Type != "AspNet.Identity.SecurityStamp").Select(static claim => new Claim(claim.Type, claim.Value, claim.ValueType)));
        identity.AddClaim(new Claim(Claims.Subject, id.Value));
        if (user.Identity?.Name is { } name)
        {
            identity.AddClaim(new Claim(Claims.Name, name));
        }

        identity.AddClaims(user.FindAll(ClaimTypes.Role).Select(static role => new Claim(Claims.Role, role.Value)));
        identity.SetScopes(Scopes.OfflineAccess);
        identity.SetDestinations(static _ => [Destinations.AccessToken]);
        var principal = new ClaimsPrincipal(identity);
        identity.SetAuthorizationId(await SessionEvents.StartAsync(http, request, principal, id.Value, options));
        var properties = new AuthenticationProperties();
        if ((string?)request[SignInClient.ReturnPathParameter] == "request" && InstalledAppReturnPath.Take(http, options, pages) is { } back)
        {
            properties.Parameters[SignInClient.ReturnPathParameter] = back;
        }

        return Results.SignIn(principal, properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
