using Aioros.Hosting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Showcase.Games;

/// <summary>The signed-in user, from the store layout's loader.</summary>
public sealed record StoreUser(string Name);

/// <summary>
/// The store's layout; its loader decides who may open the store. Signing out ends the web session; the installed app
/// then goes to its sign-out route, which revokes its own session at the server and offers to sign in again.
/// </summary>
public sealed partial class StoreShellModel(StoreUser user, IAccountFunctions account, IAppNavigator navigator, AiorosMode mode)
{
    public string UserName { get; } = user.Name;

    public string Initial { get; } = user.Name[..1].ToUpperInvariant();

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await account.SignOutAsync();
        await navigator.GoToAsync(mode.Role == AiorosRole.Client ? "/logout" : "/login");
    }
}

/// <summary>An installed app signed in as the player.</summary>
public sealed record DeviceRow(string Id, string Device, string Signed);

/// <summary>The account page's data: the player and the installed apps signed in as the player.</summary>
public sealed record AccountPage(string UserName, IReadOnlyList<DeviceRow> Devices);

/// <summary>The account page: the installed apps signed in as the player, each signed out with its own button.</summary>
public sealed partial class AccountModel : ObservableObject
{
    public AccountModel(AccountPage data, IAccountFunctions account)
    {
        RevokeCommand = new AsyncRelayCommand<string>(id => account.RevokeDeviceAsync(id!));
        Receive(data);
    }

    public IAsyncRelayCommand<string> RevokeCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Empty))]
    public partial IReadOnlyList<DeviceRow> Devices { get; set; } = [];

    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    public bool Empty => Devices.Count == 0;

    public void Receive(AccountPage data)
    {
        ArgumentNullException.ThrowIfNull(data);
        UserName = data.UserName;
        Devices = data.Devices;
    }
}

/// <summary>Where the sign-in page goes once the player is signed in.</summary>
public sealed record SignInData(string ReturnUrl);

public sealed partial class LoginModel(SignInData data, IAccountFunctions account, IAppNavigator navigator) : ObservableObject
{
    private string _returnUrl = data.ReturnUrl;

    [ObservableProperty]
    public partial string UserName { get; set; } = "admin";

    [ObservableProperty]
    public partial string Password { get; set; } = "admin";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Failed))]
    public partial string Error { get; set; } = string.Empty;

    public bool Failed => Error.Length > 0;

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (await account.SignInAsync(UserName, Password))
        {
            await navigator.GoToAsync(_returnUrl);
            return;
        }

        Error = "That user name and password do not match.";
    }

    public void Receive(SignInData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _returnUrl = data.ReturnUrl;
    }
}
