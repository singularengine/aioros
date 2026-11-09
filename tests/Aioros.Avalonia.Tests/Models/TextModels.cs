using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Order : ObservableObject
{
    [ObservableProperty]
    public partial string Customer { get; set; } = "Ada";

    [ObservableProperty]
    public partial string? Phone { get; set; }

    [ObservableProperty]
    public partial decimal? Quantity { get; set; } = 2.5m;

    [ObservableProperty]
    public partial string? Fruit { get; set; } = "Apple";

    public ImmutableArray<string> Fruits { get; } = ["Apple", "Banana"];
}

public sealed partial class Palette : ObservableObject
{
    [ObservableProperty]
    public partial global::Avalonia.Media.Color Accent { get; set; } = global::Avalonia.Media.Color.FromRgb(0x33, 0x66, 0x99);
}
