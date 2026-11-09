using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Basket : ObservableObject
{
    [ObservableProperty]
    public partial string Fruit { get; set; } = "Pear";
}

public sealed partial class Shelf : ObservableObject
{
    public Shelf(int selected, params Item[] items)
    {
        Items = [.. items];
        Selected = Items[selected];
    }

    public ImmutableArray<Item> Items { get; }

    [ObservableProperty]
    public partial Item? Selected { get; set; }
}

public sealed partial class Tabs : ObservableObject
{
    [ObservableProperty]
    public partial int Tab { get; set; }
}

public sealed partial class Ticker : ObservableObject
{
    private int _reads;

    public Ticker(Action<Ticker> realized, params string[] names)
    {
        Ticks = [.. names.Select(name => new Tick(name, () =>
        {
            if (Interlocked.Increment(ref _reads) == 1)
            {
                realized(this);
            }
        }))];
    }

    public ImmutableArray<Tick> Ticks { get; }

    [ObservableProperty]
    public partial string Price { get; set; } = "10";
}

public sealed class Tick(string name, Action read)
{
    public string Name
    {
        get
        {
            read();
            return name;
        }
    }
}

public sealed record Product(string Name, int Stock);

public sealed class Inventory(params Product[] products)
{
    public ImmutableArray<Product> Products { get; } = [.. products];
}

public sealed partial class Paging : ObservableObject
{
    [ObservableProperty]
    public partial int Page { get; set; }

    public int Count { get; init; } = 4;
}

public sealed class Category(string name)
{
    public string Name { get; } = name;
}

public sealed partial class Assignment : ObservableObject
{
    public Assignment(int selected, params Category[] categories)
    {
        Categories = [.. categories];
        Category = Categories[selected];
    }

    public ImmutableArray<Category> Categories { get; }

    [ObservableProperty]
    public partial Category? Category { get; set; }
}

public sealed class SortOrder(string name)
{
    public static SortOrder[] All { get; } = [new("Due date"), new("Title"), new("Priority")];

    public string Name { get; } = name;

    public override string ToString() => Name;
}

public sealed partial class OrderPick(int selected) : ObservableObject
{
    [ObservableProperty]
    public partial SortOrder Order { get; set; } = SortOrder.All[selected];
}

public sealed class Picks(string[] all, params string[] chosen)
{
    public ImmutableArray<string> All { get; } = [.. all];

    public System.Collections.ObjectModel.ObservableCollection<string> Chosen { get; } = [.. chosen];
}

public sealed partial class Leaves(ImmutableArray<Item> items, int index) : ObservableObject
{
    public ImmutableArray<Item> Items { get; } = items;

    [ObservableProperty]
    public partial int Index { get; set; } = index;
}
