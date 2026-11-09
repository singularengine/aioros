using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Showcase : ObservableObject
{
    public string Title { get; init; } = "Title";

    public bool Visible { get; init; } = true;

    [ObservableProperty]
    public partial bool Agree { get; set; } = true;

    [ObservableProperty]
    public partial string Name { get; set; } = "Ada";

    [ObservableProperty]
    public partial string Plan { get; set; } = "Pro";

    [ObservableProperty]
    public partial double Volume { get; set; } = 40;

    [ObservableProperty]
    public partial double Progress { get; set; } = 30;

    [ObservableProperty]
    public partial DateTime? Day { get; set; } = new DateTime(2024, 3, 14, 0, 0, 0, DateTimeKind.Unspecified);

    [ObservableProperty]
    public partial TimeSpan? At { get; set; } = new TimeSpan(13, 45, 0);

    public ImmutableArray<Item> Items { get; init; } = [new Item("one"), new Item("two")];

    [ObservableProperty]
    public partial Item? Selected { get; set; }

    [ObservableProperty]
    public partial int Tab { get; set; }

    [ObservableProperty]
    public partial int Page { get; set; } = 1;

    public int Count { get; init; } = 4;

    public string Icon { get; init; } = "check";
}

public sealed partial class Checkout : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = "Ada";

    [ObservableProperty]
    public partial bool Agree { get; set; } = true;

    [ObservableProperty]
    public partial bool Express { get; set; }

    [ObservableProperty]
    public partial int Quantity { get; set; } = 2;

    [ObservableProperty]
    public partial double Volume { get; set; } = 40;

    [ObservableProperty]
    public partial DateTime? Day { get; set; } = new DateTime(2024, 3, 14, 0, 0, 0, DateTimeKind.Unspecified);

    [ObservableProperty]
    public partial TimeSpan? At { get; set; } = new TimeSpan(13, 45, 0);

    [ObservableProperty]
    public partial string Plan { get; set; } = "Pro";

    public ImmutableArray<Item> Items { get; init; } = [new Item("one"), new Item("two"), new Item("three")];

    [ObservableProperty]
    public partial Item? Selected { get; set; }

    [ObservableProperty]
    public partial int Page { get; set; }

    public int Count { get; init; } = 4;

    [ObservableProperty]
    public partial string Saved { get; set; } = string.Empty;

    public string Snapshot => $"{Name}|{Agree}|{Express}|{Quantity}|{Volume}|{Day:O}|{At}|{Plan}|{Selected?.Name}|{Page}|{Saved}";

    [RelayCommand]
    private async Task SaveAsync(string? note)
    {
        await Task.Yield();
        Saved = note + ":" + Name;
    }
}

public sealed partial class Tiers : ObservableObject
{
    public IReadOnlyList<string> Choices { get; init; } = ["Free", "Pro"];

    [ObservableProperty]
    public partial string Choice { get; set; } = "Pro";
}

public sealed class Remittance
{
    public string Memo { get; set; } = "memo";

    public bool Paid { get; set; } = true;

    public bool Locked { get; set; }
}
