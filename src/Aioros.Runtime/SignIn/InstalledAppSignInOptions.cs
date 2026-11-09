namespace Aioros.Runtime.SignIn;

/// <summary>
/// How the installed app signs in against its server: the native client the server registers, the endpoints of the
/// authorization server, the app's sign-in route and the lifetimes of the tokens. Shared by both sides.
/// </summary>
public sealed record InstalledAppSignInOptions
{
    /// <summary>The client id of the installed app, a public native client.</summary>
    public string ClientId { get; init; } = "aioros-installed-app";

    /// <summary>The authorization endpoint the system browser opens.</summary>
    public string AuthorizePath { get; init; } = "/connect/authorize";

    /// <summary>The token endpoint the app exchanges codes and refresh tokens at.</summary>
    public string TokenPath { get; init; } = "/connect/token";

    /// <summary>The revocation endpoint (RFC 7009) the app posts its refresh token to when it signs out.</summary>
    public string RevokePath { get; init; } = "/connect/revoke";

    /// <summary>The path of the loopback redirect, on <c>127.0.0.1</c> with any port.</summary>
    public string CallbackPath { get; init; } = "/callback";

    /// <summary>
    /// The app's sign-in route: the cookie scheme's login path on the web, where the browser signs in; in the installed app
    /// a navigation there, or a <c>401</c> that remains, signs in through the system browser.
    /// </summary>
    public string SignInPath { get; init; } = "/login";

    /// <summary>
    /// The installed app's sign-out route: a navigation there revokes the refresh token at the server, clears the token
    /// store and shows a "You are signed out" view that offers to sign in again.
    /// </summary>
    public string SignOutPath { get; init; } = "/logout";

    /// <summary>
    /// Whether signing out of the website (the cookie scheme's sign-out) also revokes every installed-app session of the
    /// user. Off by default: signing out of a browser signs out only that browser, as on GitHub or Google, and the
    /// device list revokes an installed app on its own.
    /// </summary>
    public bool RevokeOnWebSignOut { get; init; }

    /// <summary>How long the website keeps the page a visitor came from while the app is downloaded and installed.</summary>
    public TimeSpan ReturnPathLifetime { get; init; } = TimeSpan.FromHours(1);

    /// <summary>The authentication scheme of the web sign-in the authorization endpoint relies on.</summary>
    public string CookieScheme { get; init; } = "Identity.Application";

    /// <summary>How long an access token is valid.</summary>
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>How long a refresh token is valid; each use replaces it.</summary>
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);
}
