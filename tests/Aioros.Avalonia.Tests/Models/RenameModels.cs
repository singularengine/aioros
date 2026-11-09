using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>A model whose command leaves a name its page's plan never learned the icon of.</summary>
public sealed partial class Renamer : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = "Ada";

    [RelayCommand]
    private void Rename() => Name = "Renamed";
}
