using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Showcase.Games;

public sealed record ReviewRow(string Player, string Initial, string Color, string Text, string Detail, bool Recommended);

public sealed record MonthSales(string Month, double Sales);

public sealed record GameData(int Id, GameDetail Detail);

public sealed partial class GameModel : ObservableObject
{
    private readonly IGameStore _store;
    private GameData _data;

    public GameModel(GameData data, IGameStore store)
    {
        _store = store;
        _data = data;
        Receive(data);
    }

    public GameCard Game => _data.Detail.Card;

    public string Summary => _data.Detail.Summary;

    public string Released => _data.Detail.Released;

    public IReadOnlyList<string> Tags => _data.Detail.Tags;

    public IReadOnlyList<string> Shots { get; private set; } = [];

    public IReadOnlyList<ReviewRow> Reviews => _data.Detail.Reviews;

    public IReadOnlyList<MonthSales> Sales => _data.Detail.Sales;

    public IReadOnlyList<GameCard> Similar => _data.Detail.Similar;

    public string Recommended { get; private set; } = string.Empty;

    public double Liked { get; private set; }

    public bool Owned => _data.Detail.Owned;

    public bool CanBuy => !Owned;

    [ObservableProperty]
    public partial bool Bought { get; set; }

    public string Parallel => _data.Detail.Parallel;

    public void Receive(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        Shots = [.. Enumerable.Range(1, 3).Select(shot => GameArt.Url(data.Id, "shot" + shot.ToString(CultureInfo.InvariantCulture)))];
        var total = data.Detail.Positive + data.Detail.Negative;
        Liked = total == 0 ? 0 : 100.0 * data.Detail.Positive / total;
        Recommended = string.Create(CultureInfo.InvariantCulture, $"{Liked:0}% of {total:N0} reviews recommend it");
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private async Task BuyAsync()
    {
        await _store.BuyAsync(_data.Id);
        Bought = true;
    }
}
