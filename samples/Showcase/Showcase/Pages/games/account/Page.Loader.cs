using System.Globalization;
using System.Security.Claims;
using Aioros.Pages;
using Aioros.Targets.Ssr.SignIn;
using Showcase.Games;

namespace Showcase.Pages.Games;

/// <summary>The player's account: the installed apps signed in as the player, from the server's sessions.</summary>
public sealed class AccountLoader(InstalledAppSessions sessions)
{
    public async Task<LoaderResult<AccountPage>> LoadAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var devices = await sessions.ListAsync(user, cancellationToken);
        return LoaderResult.With(
            new AccountPage(
                user.Identity?.Name ?? "guest",
                [.. devices.Select(static device => new DeviceRow(
                    device.Id,
                    device.Device.Length > 0 ? device.Device : "Unnamed device",
                    string.Create(CultureInfo.InvariantCulture, $"Signed in {device.Created:d MMM yyyy} · last used {device.LastUsed:d MMM yyyy HH:mm} UTC")))]),
            "Account · Aioros Games");
    }
}
