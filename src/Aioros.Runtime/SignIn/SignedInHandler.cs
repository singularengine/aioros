using System.Net;
using System.Net.Http.Headers;

namespace Aioros.Runtime.SignIn;

/// <summary>
/// Attaches the installed app's access token to its requests to <c>/_data</c> and <c>/_sf</c>, refreshes it before it
/// expires, and after a <c>401</c> refreshes it once and sends the request again (Uno's <c>HeaderHandler</c>,
/// <c>MauiBlazorWebIdentity</c>). A <c>401</c> that remains is returned, and the callers fail with
/// <see cref="SignInRequiredException"/>.
/// </summary>
public sealed class SignedInHandler(SignInClient client) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = await client.AccessTokenAsync(cancellationToken);
        Attach(request, token);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || token is null || await client.RefreshAsync(token, cancellationToken) is not { } fresh)
        {
            return response;
        }

        response.Dispose();
        Attach(request, fresh);
        return await base.SendAsync(request, cancellationToken);
    }

    private static void Attach(HttpRequestMessage request, string? token) =>
        request.Headers.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
}
