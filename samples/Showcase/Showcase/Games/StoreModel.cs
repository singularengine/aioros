using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Showcase.Games;

public sealed record StoreData(string? Genre, string Search, string Sort, int Page, StorePage Result);

public sealed partial class StoreModel : ObservableObject
{
    public static readonly System.Collections.Immutable.ImmutableArray<(string Key, string Label)> Sorts = [("top", "Top sellers"), ("rated", "Top rated"), ("new", "New releases"), ("price", "Lowest price")];
    private readonly IAppNavigator _navigator;
    private readonly string? _genre;
    private readonly string _sort;

    public GameCard? Featured { get; }

    public bool HasFeatured => Featured is not null;

    public IReadOnlyList<GameCard> Offers { get; } = [];

    public IReadOnlyList<GameCard> Games { get; } = [];

    public IReadOnlyList<ChipLink> GenreLinks { get; } = [];

    public IReadOnlyList<ChipLink> SortLinks { get; } = [];

    public string Summary { get; } = string.Empty;

    public string Parallel { get; } = string.Empty;

    public string PageText { get; } = string.Empty;

    public string PreviousHref { get; } = string.Empty;

    public string NextHref { get; } = string.Empty;

    public bool HasPrevious { get; }

    public bool HasNext { get; }

    public bool Empty { get; }

    public bool Seeding { get; }

    public string SeedText { get; } = string.Empty;

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    public StoreModel(StoreData data, IAppNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(data);
        _navigator = navigator;
        _genre = data.Genre;
        _sort = data.Sort;
        Search = data.Search;
        var page = data.Page;
        var result = data.Result;

        Featured = result.Featured;
        Offers = result.Offers;
        Games = result.Games;
        Parallel = result.Parallel;
        Seeding = result.Seeding;
        SeedText = result.SeedText;
        var pages = Math.Max(1, (result.Total + GameStore.PageSize - 1) / GameStore.PageSize);
        Empty = result.Total == 0;
        Summary = result.Total == 0 ? "No games match." : string.Create(CultureInfo.InvariantCulture, $"{result.Total:N0} games");
        PageText = string.Create(CultureInfo.InvariantCulture, $"Page {Math.Min(page, pages):N0} of {pages:N0}");
        HasPrevious = page > 1;
        HasNext = page < pages;
        PreviousHref = Url(_genre, Search, _sort, page - 1);
        NextHref = Url(_genre, Search, _sort, page + 1);
        GenreLinks = [new ChipLink("All", Url(null, Search, _sort, 1), _genre is null, result.Genres.Values.Sum().ToString("N0", CultureInfo.InvariantCulture)),
            .. GameCatalog.Genres.Select(genre => new ChipLink(genre, Url(genre, Search, _sort, 1), genre == _genre, result.Genres.GetValueOrDefault(genre).ToString("N0", CultureInfo.InvariantCulture)))];
        SortLinks = [.. Sorts.Select(entry => new ChipLink(entry.Label, Url(_genre, Search, entry.Key, 1), entry.Key == _sort))];
    }

    [RelayCommand]
    private Task FindAsync() => _navigator.GoToAsync(Url(_genre, Search, _sort, 1));

    private static string Url(string? genre, string? search, string sort, int page)
    {
        var parts = new List<string>();
        if (genre is not null)
        {
            parts.Add("genre=" + Uri.EscapeDataString(genre));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            parts.Add("q=" + Uri.EscapeDataString(search.Trim()));
        }

        if (sort != "top")
        {
            parts.Add("sort=" + sort);
        }

        if (page > 1)
        {
            parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "/games" : "/games?" + string.Join('&', parts);
    }
}
