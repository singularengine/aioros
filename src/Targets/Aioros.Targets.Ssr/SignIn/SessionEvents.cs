using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aioros.Runtime.SignIn;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Validation;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Aioros.Targets.Ssr.SignIn;

/// <summary>
/// The life of an installed-app session: each sign-in of the app is one OpenIddict authorization named after the device,
/// and it ends when its refresh token is revoked (RFC 7009), when the user revokes it from the device list, or, when the
/// app chooses, when the user signs out of the website.
/// </summary>
internal static class SessionEvents
{
    public const string DeviceParameter = "device_name";

    private const int MaxDevice = 64;

    /// <summary>Creates the authorization of this sign-in, one session of the app named after the device the request names.</summary>
    public static async Task<string?> StartAsync(HttpContext http, OpenIddictRequest request, ClaimsPrincipal principal, string subject, InstalledAppSignInOptions options)
    {
        var applications = http.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
        var authorizations = http.RequestServices.GetRequiredService<IOpenIddictAuthorizationManager>();
        var application = await applications.FindByClientIdAsync(request.ClientId ?? options.ClientId, http.RequestAborted)
            ?? throw new InvalidOperationException("The installed app's client is not registered.");
        var device = new string([.. ((string?)request[DeviceParameter] ?? string.Empty).Where(static character => !char.IsControl(character))]).Trim();
        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = await applications.GetIdAsync(application, http.RequestAborted),
            CreationDate = (http.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow(),
            Principal = principal,
            Status = Statuses.Valid,
            Subject = subject,
            Type = AuthorizationTypes.AdHoc,
            Scopes = { Scopes.OfflineAccess },
            Properties = { [InstalledAppSessions.DeviceProperty] = JsonSerializer.SerializeToElement(device.Length > MaxDevice ? device[..MaxDevice] : device, SessionJson.Default.String) },
        };
        var authorization = await authorizations.CreateAsync(descriptor, http.RequestAborted);
        return await authorizations.GetIdAsync(authorization, http.RequestAborted);
    }

    /// <summary>The cookie sign-out, followed by the revocation of every installed-app session of the user who signs out.</summary>
    public static Func<CookieSigningOutContext, Task> RevokingOnSignOut(Func<CookieSigningOutContext, Task> signingOut) => async context =>
    {
        await signingOut(context);
        await context.HttpContext.RequestServices.GetRequiredService<InstalledAppSessions>().RevokeAllAsync(context.HttpContext.User, context.HttpContext.RequestAborted);
    };

    /// <summary>
    /// After OpenIddict revokes a refresh token at the revocation endpoint, ends the session it belongs to: its
    /// authorization and every other token issued under it, so the access token issued with it is refused too.
    /// </summary>
    public sealed class EndingOnRevocation(IServiceProvider services) : IOpenIddictServerHandler<OpenIddictServerEvents.HandleRevocationRequestContext>
    {
        public static OpenIddictServerHandlerDescriptor Descriptor { get; } = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleRevocationRequestContext>()
            .UseScopedHandler<EndingOnRevocation>()
            .SetOrder(OpenIddictServerHandlers.Revocation.RevokeToken.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

        public async ValueTask HandleAsync(OpenIddictServerEvents.HandleRevocationRequestContext context)
        {
            if (context.GenericTokenPrincipal?.GetAuthorizationId() is not { } id)
            {
                return;
            }

            var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
            if (await authorizations.FindByIdAsync(id, context.CancellationToken) is { } authorization)
            {
                await services.GetRequiredService<InstalledAppSessions>().RevokeAsync(authorization, id, context.CancellationToken);
            }
        }
    }

    /// <summary>
    /// Gives the bearer validation the session of the access token, which OpenIddict reads only from a token entry, so
    /// its authorization entry check refuses the token of an ended session with one lookup and no token entry read.
    /// </summary>
    public sealed class CheckingSession : IOpenIddictValidationHandler<OpenIddictValidationEvents.ValidateTokenContext>
    {
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; } = OpenIddictValidationHandlerDescriptor.CreateBuilder<OpenIddictValidationEvents.ValidateTokenContext>()
            .UseSingletonHandler<CheckingSession>()
            .SetOrder(OpenIddictValidationHandlers.Protection.ValidateAuthorizationEntry.Descriptor.Order - 1)
            .SetType(OpenIddictValidationHandlerType.Custom)
            .Build();

        public ValueTask HandleAsync(OpenIddictValidationEvents.ValidateTokenContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.AuthorizationId ??= context.Principal?.GetAuthorizationId();
            return ValueTask.CompletedTask;
        }
    }
}

[JsonSerializable(typeof(string))]
internal sealed partial class SessionJson : JsonSerializerContext;
