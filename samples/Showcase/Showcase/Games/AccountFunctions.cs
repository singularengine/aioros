using System.Security.Claims;
using Aioros.Targets.Ssr.SignIn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Showcase.Games;

/// <summary>
/// ASP.NET Core Identity behind the account functions; on the web the sign-in cookie goes with the page's answer. The
/// installed apps signed in as the player are their sessions on the server, revoked one by one.
/// </summary>
[AllowAnonymous]
public sealed class AccountFunctions(SignInManager<IdentityUser> signIn, InstalledAppSessions sessions, IHttpContextAccessor http) : IAccountFunctions
{
    public async Task<bool> SignInAsync(string userName, string password) =>
        (await signIn.PasswordSignInAsync(userName, password, isPersistent: true, lockoutOnFailure: false)).Succeeded;

    public Task SignOutAsync() => signIn.SignOutAsync();

    [Authorize]
    public Task<bool> RevokeDeviceAsync(string id) =>
        sessions.RevokeAsync(http.HttpContext?.User ?? new ClaimsPrincipal(), id, http.HttpContext?.RequestAborted ?? CancellationToken.None);
}
