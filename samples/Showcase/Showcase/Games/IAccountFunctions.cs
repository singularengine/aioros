namespace Showcase.Games;

/// <summary>Signs a player in and out of the store, and signs out one of the player's installed apps.</summary>
public interface IAccountFunctions
{
    Task<bool> SignInAsync(string userName, string password);

    Task SignOutAsync();

    Task<bool> RevokeDeviceAsync(string id);
}
