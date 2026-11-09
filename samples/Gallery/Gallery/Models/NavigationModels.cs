using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gallery.Models;

public sealed partial class NavigationModel : ObservableValidator
{
    [ObservableProperty]
    public partial string Message { get; set; } = "Pick a command.";

    [ObservableProperty]
    public partial bool Bold { get; set; }

    [ObservableProperty]
    public partial int Tab { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "A name is required.")]
    [MinLength(3, ErrorMessage = "Use at least three letters.")]
    public partial string Name { get; set; } = string.Empty;

    [RelayCommand]
    private void Run(string command) => Message = $"Ran \"{command}\" on the server.";

    [RelayCommand]
    private void Register()
    {
        ValidateAllProperties();
        Message = HasErrors ? "Fix the errors and try again." : $"Registered {Name}.";
    }
}
