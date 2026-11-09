using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Json;
using Aioros.Runtime.SignIn;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Aioros.Targets.Ssr.SignIn;

/// <summary>One installed app signed in as the user: the session's id, the device it named and when it was created and last used.</summary>
public sealed record InstalledAppSession(string Id, string Device, DateTimeOffset Created, DateTimeOffset LastUsed);

/// <summary>
/// The user's installed-app sessions, one OpenIddict authorization per sign-in of the app's client, which a page can list
/// and revoke one by one (GitHub's and Google's device lists). Revoking a session revokes its authorization and every
/// token issued under it, so its refresh token no longer refreshes and its access token is refused at once. Where the
/// app does not run the authorization server (the desktop in process), the list is empty.
/// </summary>
public sealed class InstalledAppSessions(IServiceProvider services)
{
    /// <summary>The name of the authorization's property that holds the device the app named.</summary>
    public const string DeviceProperty = "aioros_device";

    /// <summary>The user's valid sessions, newest first.</summary>
    public async Task<ImmutableArray<InstalledAppSession>> ListAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (Subject(user) is not { } subject || await ClientAsync(cancellationToken) is not { } client)
        {
            return [];
        }

        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
        var sessions = ImmutableArray.CreateBuilder<InstalledAppSession>();
        await foreach (var authorization in authorizations.FindAsync(subject, client, Statuses.Valid, null, null, cancellationToken))
        {
            var id = await authorizations.GetIdAsync(authorization, cancellationToken) ?? string.Empty;
            var created = await authorizations.GetCreationDateAsync(authorization, cancellationToken) ?? DateTimeOffset.MinValue;
            var properties = await authorizations.GetPropertiesAsync(authorization, cancellationToken);
            var device = properties.TryGetValue(DeviceProperty, out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() ?? string.Empty : string.Empty;
            var used = created;
            await foreach (var token in tokens.FindByAuthorizationIdAsync(id, cancellationToken))
            {
                if (await tokens.GetCreationDateAsync(token, cancellationToken) is { } issued && issued > used)
                {
                    used = issued;
                }
            }

            sessions.Add(new InstalledAppSession(id, device, created, used));
        }

        return [.. sessions.OrderByDescending(static session => session.Created)];
    }

    /// <summary>Revokes one of the user's sessions; <see langword="false"/> when it is not one of theirs or is already revoked.</summary>
    public async Task<bool> RevokeAsync(ClaimsPrincipal user, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(id);
        if (Subject(user) is not { } subject || await ClientAsync(cancellationToken) is not { } client)
        {
            return false;
        }

        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.FindByIdAsync(id, cancellationToken);
        if (authorization is null
            || await authorizations.GetSubjectAsync(authorization, cancellationToken) != subject
            || await authorizations.GetApplicationIdAsync(authorization, cancellationToken) != client
            || !await authorizations.HasStatusAsync(authorization, Statuses.Valid, cancellationToken))
        {
            return false;
        }

        return await RevokeAsync(authorization, id, cancellationToken);
    }

    /// <summary>Revokes every session of the user, as a sign-out of every installed app does; answers how many were revoked.</summary>
    public async Task<int> RevokeAllAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var revoked = 0;
        foreach (var session in await ListAsync(user, cancellationToken))
        {
            revoked += await RevokeAsync(user, session.Id, cancellationToken) ? 1 : 0;
        }

        return revoked;
    }

    internal async Task<bool> RevokeAsync(object authorization, string id, CancellationToken cancellationToken)
    {
        var revoked = await services.GetRequiredService<IOpenIddictAuthorizationManager>().TryRevokeAsync(authorization, cancellationToken);
        await services.GetRequiredService<IOpenIddictTokenManager>().RevokeByAuthorizationIdAsync(id, cancellationToken);
        return revoked;
    }

    private static string? Subject(ClaimsPrincipal user) => user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(Claims.Subject)?.Value;

    private async Task<string?> ClientAsync(CancellationToken cancellationToken)
    {
        if (services.GetService<InstalledAppSignInOptions>() is not { } options || services.GetService<IOpenIddictApplicationManager>() is not { } applications)
        {
            return null;
        }

        return await applications.FindByClientIdAsync(options.ClientId, cancellationToken) is { } application ? await applications.GetIdAsync(application, cancellationToken) : null;
    }
}
