using Aioros.Functions;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Showcase.Games;

/// <summary>
/// The store on EF Core. Each page's queries run at the same time, each on its own pooled context, and the answer says
/// how long they took together and on which threads.
/// </summary>
[Authorize]
public sealed class GameStore(IDbContextFactory<GamesDb> factory, IHttpContextAccessor http, SeedProgress seed, TimeProvider time) : IGameStore
{
    public const int PageSize = 24;

    private string? User => http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<StorePage> BrowseAsync(string? genre, string? search, string sort, int page)
    {
        var (featured, offers, (games, total), genres, report) = await ParallelReport.RunAsync(
            FeaturedAsync,
            OffersAsync,
            () => ResultsAsync(genre, search, sort, Math.Max(1, page)),
            GenresAsync);
        var snapshot = seed.Snapshot;
        var text = string.Create(CultureInfo.InvariantCulture,
            $"{snapshot.Phase}: {snapshot.Share:0%} of {snapshot.Target:N0} rows, {snapshot.Workers} {(snapshot.Workers == 1 ? "worker" : "workers")}. The store fills in as you browse.");
        return new StorePage(featured is null ? null : GameCards.From(featured), [.. offers.Select(GameCards.From)], [.. games.Select(GameCards.From)],
            total, genres, report.Text, !snapshot.Done, text);
    }

    public async Task<GameDetail?> GameAsync(int id)
    {
        Game? game;
        await using (var db = await factory.CreateDbContextAsync())
        {
            game = await db.Games.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == id);
        }

        if (game is null)
        {
            return null;
        }

        var user = User;
        var (reviews, sales, similar, owned, report) = await ParallelReport.RunAsync(
            () => ReviewsAsync(id),
            () => SalesAsync(id),
            () => SimilarAsync(id, game.Genre),
            () => OwnedAsync(id, user));
        return new GameDetail(GameCards.From(game), game.Summary, game.Tags, game.Released.ToString("d MMM, yyyy", CultureInfo.InvariantCulture),
            game.Positive, game.Negative, reviews, sales, [.. similar.Select(GameCards.From)], owned, report.Text);
    }

    public async Task BuyAsync(int id)
    {
        var user = User ?? throw new UnauthorizedAccessException();
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Purchases.AnyAsync(purchase => purchase.UserId == user && purchase.GameId == id))
        {
            return;
        }

        var game = await db.Games.AsNoTracking().Where(entry => entry.Id == id).Select(entry => new { entry.Price, entry.Discount }).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("That game is not in the store.");
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Purchases.Add(new Purchase { GameId = id, UserId = user, Price = game.Price * (100 - game.Discount) / 100, At = time.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync();
            await db.Games.Where(entry => entry.Id == id).ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.Owners, entry => entry.Owners + 1));
            await transaction.CommitAsync();
        });
    }

    public async Task<LibraryPage> LibraryAsync()
    {
        if (User is not { } user)
        {
            return new LibraryPage([], 0);
        }

        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.Purchases.AsNoTracking().Where(purchase => purchase.UserId == user).OrderByDescending(purchase => purchase.At)
            .Join(db.Games, purchase => purchase.GameId, game => game.Id, (purchase, game) => new { game, purchase.At, purchase.Price })
            .ToListAsync();
        return new LibraryPage(
            [.. rows.Select(row => new LibraryEntry(GameCards.From(row.game), "Bought " + row.At.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + " for " + GameCards.Money(row.Price)))],
            rows.Sum(row => row.Price));
    }

    private async Task<Game?> FeaturedAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Games.AsNoTracking().Where(game => game.Discount > 0 && game.Positive * 5 > (game.Positive + game.Negative) * 4)
            .OrderByDescending(game => game.Owners).FirstOrDefaultAsync();
    }

    private async Task<List<Game>> OffersAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Games.AsNoTracking().Where(game => game.Discount >= 33).OrderByDescending(game => game.Positive).Skip(1).Take(4).ToListAsync();
    }

    private async Task<(List<Game>, int)> ResultsAsync(string? genre, string? search, string sort, int page)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Games.AsNoTracking();
        if (genre is not null)
        {
            query = query.Where(game => game.Genre == genre);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLowerInvariant();
            query = query.Where(game => game.Title.ToLower().Contains(text) || game.Studio.ToLower().Contains(text));
        }

        var total = await query.CountAsync();
        var ordered = sort switch
        {
            "rated" => query.Where(game => game.Positive + game.Negative >= 20).OrderByDescending(game => (double)game.Positive / (game.Positive + game.Negative)).ThenByDescending(game => game.Positive),
            "new" => query.OrderByDescending(game => game.Released),
            "price" => query.OrderBy(game => game.Price * (100 - game.Discount)).ThenByDescending(game => game.Owners),
            _ => query.OrderByDescending(game => game.Owners),
        };
        return (await ordered.ThenBy(game => game.Id).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(), total);
    }

    private async Task<IReadOnlyDictionary<string, int>> GenresAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Games.AsNoTracking().GroupBy(game => game.Genre).Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Count);
    }

    private async Task<IReadOnlyList<ReviewRow>> ReviewsAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.Reviews.AsNoTracking().Where(review => review.GameId == id).OrderByDescending(review => review.At).Take(6)
            .Join(db.Players, review => review.PlayerId, player => player.Id, (review, player) => new { review, player.Name, player.Level })
            .ToListAsync();
        return [.. rows.Select(row => new ReviewRow(
            row.Name,
            row.Name[..1].ToUpperInvariant(),
            row.review.Recommended ? "#2A475E" : "#4B2A2A",
            row.review.Text,
            string.Create(CultureInfo.InvariantCulture, $"{row.review.Hours:N0} h on record · level {row.Level} · {row.review.At:d MMM yyyy}"),
            row.review.Recommended))];
    }

    private async Task<IReadOnlyList<MonthSales>> SalesAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var since = time.GetUtcNow().UtcDateTime.AddMonths(-12);
        var months = await db.Purchases.AsNoTracking().Where(purchase => purchase.GameId == id && purchase.At >= since)
            .GroupBy(purchase => new { purchase.At.Year, purchase.At.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
            .ToListAsync();
        var start = new DateTime(since.Year, since.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return [.. Enumerable.Range(1, 12).Select(start.AddMonths).Select(month => new MonthSales(
            month.ToString("MMM", CultureInfo.InvariantCulture),
            months.FirstOrDefault(entry => entry.Year == month.Year && entry.Month == month.Month)?.Count ?? 0))];
    }

    private async Task<List<Game>> SimilarAsync(int id, string genre)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Games.AsNoTracking().Where(game => game.Genre == genre && game.Id != id).OrderByDescending(game => game.Owners).Take(4).ToListAsync();
    }

    private async Task<bool> OwnedAsync(int id, string? user)
    {
        await using var db = await factory.CreateDbContextAsync();
        return user is not null && await db.Purchases.AnyAsync(purchase => purchase.UserId == user && purchase.GameId == id);
    }
}

[Authorize]
public sealed class LabFunctions(StoreTraffic traffic, SeedProgress seed, GamesOptions options) : ILabFunctions
{
    [Cached(1, Shared = true)]
    [Reads("lab")]
    public Task<LabSnapshot> SnapshotAsync() =>
        Task.FromResult(new LabSnapshot(traffic.Snapshot, seed.Snapshot, ThreadPool.ThreadCount, options.Database, options.Workers, options.Crews));

    [Invalidates("lab")]
    public Task SetPlayersAsync(int players)
    {
        traffic.SetPlayers(Math.Clamp(players, 0, options.Crews.Max()));
        return Task.CompletedTask;
    }
}
