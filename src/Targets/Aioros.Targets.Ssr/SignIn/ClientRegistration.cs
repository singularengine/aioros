using System.Net;
using Aioros.Runtime.SignIn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Aioros.Targets.Ssr.SignIn;

/// <summary>
/// Registers the installed app as a public native client with a loopback redirect, once, and gives a client registered
/// before revocation existed the permissions it now needs.
/// </summary>
internal sealed class ClientRegistration(IServiceProvider services, InstalledAppSignInOptions options) : IHostedService
{
    private static readonly string[] Needed =
    [
        Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.Revocation,
        Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code,
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        if (await manager.FindByClientIdAsync(options.ClientId, cancellationToken) is { } registered)
        {
            var current = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(current, registered, cancellationToken);
            if (!Needed.All(current.Permissions.Contains))
            {
                current.Permissions.UnionWith(Needed);
                await manager.UpdateAsync(registered, current, cancellationToken);
            }

            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = options.ClientId,
            ClientType = ClientTypes.Public,
            ApplicationType = ApplicationTypes.Native,
            DisplayName = "Installed app",
            RedirectUris = { new UriBuilder(Uri.UriSchemeHttp, IPAddress.Loopback.ToString()) { Path = options.CallbackPath }.Uri },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        descriptor.Permissions.UnionWith(Needed);
        await manager.CreateAsync(descriptor, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
