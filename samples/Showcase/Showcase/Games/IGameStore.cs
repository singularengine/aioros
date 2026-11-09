namespace Showcase.Games;

public sealed record StorePage(
    GameCard? Featured, IReadOnlyList<GameCard> Offers, IReadOnlyList<GameCard> Games, int Total,
    IReadOnlyDictionary<string, int> Genres, string Parallel, bool Seeding, string SeedText);

public sealed record GameDetail(
    GameCard Card, string Summary, IReadOnlyList<string> Tags, string Released, int Positive, int Negative,
    IReadOnlyList<ReviewRow> Reviews, IReadOnlyList<MonthSales> Sales, IReadOnlyList<GameCard> Similar, bool Owned, string Parallel);

public sealed record LibraryPage(IReadOnlyList<LibraryEntry> Entries, decimal Spent);

/// <summary>The store's data. It always runs on the server, next to EF Core and the database.</summary>
public interface IGameStore
{
    Task<StorePage> BrowseAsync(string? genre, string? search, string sort, int page);

    Task<GameDetail?> GameAsync(int id);

    Task BuyAsync(int id);

    Task<LibraryPage> LibraryAsync();
}

public sealed record LabSnapshot(TrafficSnapshot Traffic, SeedSnapshot Seed, int Pool, string Database, int Workers, IReadOnlyList<int> Crews);

/// <summary>The concurrency lab's controls: the virtual players and the seeder run on the server.</summary>
public interface ILabFunctions
{
    Task<LabSnapshot> SnapshotAsync();

    Task SetPlayersAsync(int players);
}
