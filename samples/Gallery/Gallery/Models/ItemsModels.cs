using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gallery.Models;

public sealed record Fruit(string Name, string Origin);

public sealed record Notice(string Kind, string Text);

public sealed record Country(string Name, string Region, int Population, int Area);

public sealed partial class Pick(Country country) : ObservableObject
{
    public Country Country { get; } = country;

    [ObservableProperty]
    public partial bool IsPicked { get; set; }
}

public sealed partial class ItemsModel : ObservableObject
{
    public ItemsModel()
    {
        Fruit = Fruits[1];
        Picks = [.. Countries.Select(country => new Pick(country))];
        foreach (var pick in Picks)
        {
            pick.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(Picked));
                OnPropertyChanged(nameof(AllPicked));
            };
        }
    }

    public ImmutableArray<Fruit> Fruits { get; } =
    [
        new("Apple", "Kazakhstan"),
        new("Banana", "Papua New Guinea"),
        new("Cherry", "Anatolia"),
        new("Mango", "South Asia"),
    ];

    public ImmutableArray<Country> Countries { get; } =
    [
        new("Brazil", "Americas", 212_559_417, 8_515_767),
        new("Japan", "Asia", 125_836_021, 377_975),
        new("Kenya", "Africa", 53_771_296, 580_367),
        new("Norway", "Europe", 5_421_241, 385_207),
    ];

    public ImmutableArray<Pick> Picks { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Shown))]
    public partial string? Filter { get; set; }

    public ImmutableArray<Pick> Shown => [.. Picks.Where(pick => string.IsNullOrEmpty(Filter) || pick.Country.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase))];

    public string Picked => $"{Picks.Count(pick => pick.IsPicked)} of {Picks.Length} picked.";

    public bool? AllPicked
    {
        get => Shown.All(pick => pick.IsPicked) ? true : Shown.Any(pick => pick.IsPicked) ? null : false;
        set
        {
            var picked = AllPicked is null || value == true;
            foreach (var pick in Shown)
            {
                pick.IsPicked = picked;
            }
        }
    }

    public ImmutableArray<Notice> Notices { get; } =
    [
        new("info", "Builds run on every push."),
        new("warning", "The cache is almost full."),
        new("success", "Deployed to production."),
        new("info", "Two reviewers are assigned."),
    ];

    [ObservableProperty]
    public partial Fruit? Fruit { get; set; }

    [ObservableProperty]
    public partial int Tab { get; set; }

    [ObservableProperty]
    public partial int View { get; set; } = 1;

    [ObservableProperty]
    public partial int Page { get; set; } = 1;

    public int Pages { get; } = 5;

    [ObservableProperty]
    public partial string Summary { get; set; } = "Pick items and apply: the selection posts without JavaScript.";

    [RelayCommand]
    private void Apply() => Summary = $"Fruit: {Fruit?.Name ?? "none"} · tab {Tab + 1} · view {View + 1} · page {Page + 1} of {Pages}.";
}
