using System.Net;
using System.Text.Json;

namespace Aioros.Runtime.SignIn;

/// <summary>
/// The installed app's side of the sign-in: OAuth 2.0 authorization code with PKCE in the system browser and a loopback
/// redirect (RFC 8252), the tokens kept in the operating system's store, refreshed before they expire and after a
/// <c>401</c>, one refresh at a time since each refresh token is valid once (RFC 9700). The current tokens are one
/// snapshot published with <see cref="Volatile"/>; a running refresh is one task that every caller awaits.
/// </summary>
/// <param name="options">The client id and the endpoints.</param>
/// <param name="tokens">A client of the server, without the bearer handler, for the token endpoint.</param>
/// <param name="store">Where the tokens are kept.</param>
/// <param name="time">The clock the expiry is read with.</param>
public sealed class SignInClient(InstalledAppSignInOptions options, HttpClient tokens, ITokenStore store, TimeProvider time)
{
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(1);
    private Snapshot? _current;
    private Task<TokenSet?>? _refreshing;

    public InstalledAppSignInOptions Options { get; } = options;

    /// <summary>
    /// What the app calls its device in the server's list of installed-app sessions: the machine's name and its operating
    /// system, never a user name or an identifier that follows the user.
    /// </summary>
    public static string DeviceName { get; } = $"{Environment.MachineName} ({(OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux")})";

    /// <summary>
    /// Signs in through the system browser: opens the authorization endpoint with an S256 challenge, a random state, a
    /// loopback redirect and the device's name, waits for the code, exchanges it with the verifier and stores the tokens.
    /// </summary>
    /// <param name="openBrowser">Opens a URL in the system browser.</param>
    /// <param name="cancellationToken">Cancels the sign-in, for instance when the user gives up.</param>
    public Task SignInAsync(Func<Uri, Task> openBrowser, CancellationToken cancellationToken) => SignInAsync(openBrowser, false, cancellationToken);

    /// <summary>
    /// Signs in as <see cref="SignInAsync(Func{Uri, Task}, CancellationToken)"/> does and, when <paramref name="askReturnPath"/>
    /// is set (the first run after an install), asks the server for the page the browser came from; answers that path as
    /// the server sent it, unchecked, or <see langword="null"/>.
    /// </summary>
    public async Task<string?> SignInAsync(Func<Uri, Task> openBrowser, bool askReturnPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(openBrowser);
        using var redirect = LoopbackRedirect.Listen(Options.CallbackPath);
        var verifier = Pkce.Verifier();
        var state = Pkce.State();
        var query = string.Join('&', new[]
        {
            ("response_type", "code"), ("client_id", Options.ClientId), ("redirect_uri", redirect.RedirectUri.AbsoluteUri),
            ("code_challenge", Pkce.Challenge(verifier)), ("code_challenge_method", "S256"), ("state", state), ("device_name", DeviceName),
        }.Concat(askReturnPath ? [(ReturnPathParameter, ReturnPathRequest)] : []).Select(static pair => pair.Item1 + "=" + Uri.EscapeDataString(pair.Item2)));
        var received = redirect.ReceiveAsync(state, cancellationToken);
        await openBrowser(new Uri(tokens.BaseAddress!, Options.AuthorizePath + "?" + query));
        var answer = await received;
        var code = answer["code"];
        var issued = await RedeemAsync(
            [new("grant_type", "authorization_code"), new("code", code), new("redirect_uri", redirect.RedirectUri.AbsoluteUri), new("client_id", Options.ClientId), new("code_verifier", verifier)],
            cancellationToken) ?? throw new SignInRequiredException("The server did not accept the sign-in's code.");
        await store.WriteAsync(issued, cancellationToken);
        Volatile.Write(ref _current, new Snapshot(issued));
        return askReturnPath ? answer.GetValueOrDefault(ReturnPathParameter) : null;
    }

    /// <summary>
    /// Signs out: forgets the tokens, here and in the store, then revokes the refresh token at the server (RFC 7009),
    /// which ends the session it belongs to. The tokens are forgotten even when the server cannot be reached; answers
    /// whether the server confirmed the revocation.
    /// </summary>
    public async Task<bool> SignOutAsync(CancellationToken cancellationToken)
    {
        var held = (Volatile.Read(ref _current) ?? await LoadAsync(cancellationToken)).Tokens;
        await ForgetAsync(cancellationToken);
        if (held is not { RefreshToken.Length: > 0 })
        {
            return false;
        }

        try
        {
            using var content = new FormUrlEncodedContent([new("token", held.RefreshToken), new("token_type_hint", "refresh_token"), new("client_id", Options.ClientId)]);
            using var response = await tokens.PostAsync(new Uri(Options.RevokePath, UriKind.Relative), content, cancellationToken);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>The authorization request's parameter that asks for, and the answer's that carries, the page the browser came from.</summary>
    public const string ReturnPathParameter = "return_path";

    private const string ReturnPathRequest = "request";

    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _current, new Snapshot(null));
        await store.ClearAsync(cancellationToken);
    }

    /// <summary>The access token to send, refreshed first when it expires within a minute; <see langword="null"/> when signed out.</summary>
    public async ValueTask<string?> AccessTokenAsync(CancellationToken cancellationToken)
    {
        var current = Volatile.Read(ref _current) ?? await LoadAsync(cancellationToken);
        if (current.Tokens is not { } held)
        {
            return null;
        }

        return held.ExpiresAt - Skew > time.GetUtcNow() ? held.AccessToken : await RefreshAsync(held.AccessToken, cancellationToken);
    }

    /// <summary>
    /// Replaces a refused access token: when another caller already replaced it, its successor; otherwise the refresh
    /// token is redeemed, once for every caller that asks while it runs. <see langword="null"/> when the server refuses
    /// it, after which the user must sign in.
    /// </summary>
    public async ValueTask<string?> RefreshAsync(string refused, CancellationToken cancellationToken)
    {
        var current = Volatile.Read(ref _current) ?? await LoadAsync(cancellationToken);
        if (current.Tokens is not { } held)
        {
            return null;
        }

        if (held.AccessToken != refused)
        {
            return held.AccessToken;
        }

        var pending = new TaskCompletionSource<TokenSet?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = Interlocked.CompareExchange(ref _refreshing, pending.Task, null);
        if (running is not null)
        {
            return (await running.WaitAsync(cancellationToken))?.AccessToken;
        }

        try
        {
            var fresh = Volatile.Read(ref _current)?.Tokens is { } now && now.AccessToken != refused ? now : await RenewAsync(held, cancellationToken);
            pending.SetResult(fresh);
            return fresh?.AccessToken;
        }
        catch (Exception exception)
        {
            pending.SetException(exception);
            _ = pending.Task.Exception;
            throw;
        }
        finally
        {
            _ = Interlocked.CompareExchange(ref _refreshing, null, pending.Task);
        }
    }

    private async Task<TokenSet?> RenewAsync(TokenSet held, CancellationToken cancellationToken)
    {
        var fresh = await RedeemAsync([new("grant_type", "refresh_token"), new("refresh_token", held.RefreshToken), new("client_id", Options.ClientId)], cancellationToken);
        if (fresh is null)
        {
            await ForgetAsync(cancellationToken);
            return null;
        }

        await store.WriteAsync(fresh, cancellationToken);
        Volatile.Write(ref _current, new Snapshot(fresh));
        return fresh;
    }

    private async ValueTask<Snapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var loaded = new Snapshot(await store.ReadAsync(cancellationToken));
        return Interlocked.CompareExchange(ref _current, loaded, null) ?? loaded;
    }

    private async Task<TokenSet?> RedeemAsync(KeyValuePair<string, string>[] form, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await tokens.PostAsync(new Uri(Options.TokenPath, UriKind.Relative), content, cancellationToken);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var lifetime = root.TryGetProperty("expires_in", out var expires) ? expires.GetInt64() : (long)Options.AccessTokenLifetime.TotalSeconds;
        return new TokenSet(
            root.GetProperty("access_token").GetString()!,
            root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString()! : string.Empty,
            time.GetUtcNow().AddSeconds(lifetime));
    }

    private sealed record Snapshot(TokenSet? Tokens);
}
