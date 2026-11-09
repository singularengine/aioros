using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gallery.Models;

public sealed partial class TextModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Subscribe { get; set; }

    [ObservableProperty]
    public partial string Plan { get; set; } = "Free";

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [RelayCommand]
    private void Save() => Summary = $"Saved {Name} on the {Plan} plan{(Subscribe ? ", subscribed" : string.Empty)}.";
}
