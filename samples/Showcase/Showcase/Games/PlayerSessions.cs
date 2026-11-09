using System.Collections.Immutable;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Showcase.Games;

/// <summary>Signs the virtual players in: one ASP.NET Core Identity session cookie per seeded player account.</summary>
internal static class PlayerSessions
{
    public static Uri Address(IServiceProvider services)
    {
        var address = services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("The server has no address to send the virtual players to.");
        var uri = new UriBuilder(address.Replace("://+", "://localhost", StringComparison.Ordinal).Replace("://*", "://localhost", StringComparison.Ordinal).Replace("://[::]", "://localhost", StringComparison.Ordinal).Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal));
        return uri.Uri;
    }

    public static async Task<ImmutableArray<string>> CreateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
        var cookie = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var sessions = ImmutableArray.CreateBuilder<string>(GameSeeder.Accounts);
        for (var index = 1; index <= GameSeeder.Accounts; index++)
        {
            if (await users.FindByNameAsync(GameSeeder.Account(index)) is { } user)
            {
                var ticket = new AuthenticationTicket(await signIn.CreateUserPrincipalAsync(user), IdentityConstants.ApplicationScheme);
                sessions.Add((cookie.Cookie.Name ?? ".AspNetCore.Identity.Application") + "=" + cookie.TicketDataFormat.Protect(ticket));
            }
        }

        return sessions.ToImmutable();
    }

}
