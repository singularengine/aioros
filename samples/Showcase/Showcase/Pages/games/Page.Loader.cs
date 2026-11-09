using Aioros.Pages;
using Showcase.Games;

namespace Showcase.Pages.Games;

public sealed class StoreLoader(IGameStore store)
{
    public async Task<LoaderResult<StoreData>> LoadAsync(string? genre, string? sort, string q = "", int page = 1)
    {
        var known = GameCatalog.Genres.FirstOrDefault(name => name == genre);
        var order = sort is not null && StoreModel.Sorts.Any(entry => entry.Key == sort) ? sort : "top";
        var current = Math.Max(1, page);
        return LoaderResult.With(
            new StoreData(known, q, order, current, await store.BrowseAsync(known, q, order, current)),
            "Store · Aioros Games",
            new PageMetadata("A game store on EF Core: 8,000 games, 20,000 players and over a million reviews and sales, seeded by parallel workers."));
    }
}
