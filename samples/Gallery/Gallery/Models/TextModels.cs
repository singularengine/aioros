using System.Collections.Immutable;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gallery.Models;

public sealed partial class TextModel
{
    [ObservableProperty]
    public partial string Email { get; set; } = "ada@example.com";

    [ObservableProperty]
    public partial string Password { get; set; } = "secret";

    [ObservableProperty]
    public partial string Notes { get; set; } = "Multi-line text\nwith a second line.";

    [ObservableProperty]
    public partial string? Phone { get; set; }

    [ObservableProperty]
    public partial decimal? Quantity { get; set; } = 2.5m;

    [ObservableProperty]
    public partial string? Fruit { get; set; }

    [ObservableProperty]
    public partial string Order { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Avalonia.Media.Color Accent { get; set; } = Avalonia.Media.Color.FromRgb(0x00, 0x78, 0xd7);

    [ObservableProperty]
    public partial string Picked { get; set; } = string.Empty;

    public ImmutableArray<string> Fruits { get; } = ["Apple", "Apricot", "Banana", "Blueberry", "Cherry", "Grape", "Mango"];

    [RelayCommand]
    private void PlaceOrder() =>
        Order = string.Create(CultureInfo.InvariantCulture, $"{Quantity ?? 0} × {(string.IsNullOrEmpty(Fruit) ? "nothing" : Fruit)} for {Email}, phone {Phone ?? "none"}.");

    [RelayCommand]
    private void PickColor() => Picked = string.Create(CultureInfo.InvariantCulture, $"Picked #{Accent.R:x2}{Accent.G:x2}{Accent.B:x2}.");
}
