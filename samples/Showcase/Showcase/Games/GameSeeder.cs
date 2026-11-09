using System.Threading.Channels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Showcase.Games;

/// <summary>
/// Creates the store on first start: the schema, the <c>admin</c> account, then players, games, reviews and sales
/// written by several workers at once, each with its own <see cref="GamesDb"/>, while the store is already browsable.
/// </summary>
public sealed class GameSeeder(IDbContextFactory<GamesDb> factory, IServiceScopeFactory scopes, SeedProgress progress, GamesOptions options, ILogger<GameSeeder> logger) : BackgroundService
{
    public const int GameCount = 8_000;
    public const int PlayerCount = 20_000;
    private const int ChunkSize = 50;
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tracking = progress.RunAsync(stoppingToken);
        try
        {
            await SeedAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Seeding the games store failed.");
        }

        await tracking;
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using (var db = await factory.CreateDbContextAsync(cancellationToken))
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
            if (db.Database.IsSqlite())
            {
                await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
            }
        }

        await AdminAsync();
        await using (var db = await factory.CreateDbContextAsync(cancellationToken))
        {
            if (await db.Games.AnyAsync(cancellationToken))
            {
                var rows = await db.Games.LongCountAsync(cancellationToken) + await db.Players.LongCountAsync(cancellationToken)
                    + await db.Reviews.LongCountAsync(cancellationToken) + await db.Purchases.LongCountAsync(cancellationToken);
                progress.Finish(options.Database, rows, seeded: false);
                return;
            }
        }

        var games = Enumerable.Range(1, GameCount).Select(GameCatalog.Game).ToList();
        var target = PlayerCount + GameCount + games.Sum(game => (long)GameCatalog.Reach(game, options.Scale).Reviews + GameCatalog.Reach(game, options.Scale).Sales);
        progress.Begin(options.Database, options.Workers, target);

        var chunks = Channel.CreateUnbounded<int[]>();
        foreach (var chunk in Enumerable.Range(1, PlayerCount).Chunk(2_000))
        {
            chunks.Writer.TryWrite(chunk);
        }

        chunks.Writer.Complete();
        await Task.WhenAll(Enumerable.Range(0, options.Workers).Select(worker => Task.Run(() => PlayersAsync(worker, chunks.Reader, cancellationToken), cancellationToken)));

        var work = Channel.CreateUnbounded<Game[]>();
        foreach (var chunk in games.Chunk(ChunkSize))
        {
            work.Writer.TryWrite(chunk);
        }

        work.Writer.Complete();
        await Task.WhenAll(Enumerable.Range(0, options.Workers).Select(worker => Task.Run(() => GamesAsync(worker, work.Reader, cancellationToken), cancellationToken)));
        await LibraryAsync(cancellationToken);
        progress.Finish(options.Database, target, seeded: true);
    }

    public const int Accounts = 256;

    public static string Account(int index) => "player" + index.ToString("000", System.Globalization.CultureInfo.InvariantCulture);

    private async Task AdminAsync()
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if (await users.FindByNameAsync(Account(Accounts)) is null)
        {
            for (var index = 1; index <= Accounts; index++)
            {
                if (await users.FindByNameAsync(Account(index)) is null)
                {
                    await users.CreateAsync(new IdentityUser(Account(index)));
                }
            }
        }

        if (await users.FindByNameAsync("admin") is null)
        {
            var created = await users.CreateAsync(new IdentityUser("admin") { Email = "admin@showcase.test", EmailConfirmed = true }, "admin");
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(string.Join(' ', created.Errors.Select(error => error.Description)));
            }
        }
    }

    private async Task LibraryAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var admin = await db.Users.Where(user => user.UserName == "admin").Select(user => user.Id).SingleAsync(cancellationToken);
        var owned = await db.Games.OrderByDescending(game => game.Owners).Take(6).Select(game => new { game.Id, game.Price, game.Discount }).ToListAsync(cancellationToken);
        db.Purchases.AddRange(owned.Select((game, index) => new Purchase { GameId = game.Id, UserId = admin, Price = game.Price * (100 - game.Discount) / 100, At = Now.AddDays(-30 * (index + 1)) }));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PlayersAsync(int worker, ChannelReader<int[]> chunks, CancellationToken cancellationToken)
    {
        await foreach (var chunk in chunks.ReadAllAsync(cancellationToken))
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            db.Players.AddRange(chunk.Select(GameCatalog.Player));
            await db.SaveChangesAsync(cancellationToken);
            progress.Report(worker, chunk.Length, "Players");
        }
    }

    private async Task GamesAsync(int worker, ChannelReader<Game[]> chunks, CancellationToken cancellationToken)
    {
        await foreach (var chunk in chunks.ReadAllAsync(cancellationToken))
        {
            var random = new Random(chunk[0].Id);
            var reviews = new List<Review>();
            var sales = new List<Purchase>();
            foreach (var game in chunk)
            {
                var (reviewCount, saleCount, liked) = GameCatalog.Reach(game, options.Scale);
                for (var index = 0; index < reviewCount; index++)
                {
                    var review = GameCatalog.Review(random, game.Id, 1 + random.Next(PlayerCount), liked, Now.AddMinutes(-random.Next(1_000_000)));
                    reviews.Add(review);
                    if (review.Recommended)
                    {
                        game.Positive++;
                    }
                    else
                    {
                        game.Negative++;
                    }
                }

                var price = game.Price * (100 - game.Discount) / 100;
                sales.AddRange(Enumerable.Range(0, saleCount).Select(_ => new Purchase { GameId = game.Id, PlayerId = 1 + random.Next(PlayerCount), Price = price, At = Now.AddMinutes(-random.Next(1_000_000)) }));
                game.Owners = saleCount;
            }

            await SaveAsync(worker, chunk, "Games", cancellationToken);
            foreach (var batch in reviews.Chunk(4_000))
            {
                await SaveAsync(worker, batch, "Reviews", cancellationToken);
            }

            foreach (var batch in sales.Chunk(4_000))
            {
                await SaveAsync(worker, batch, "Sales", cancellationToken);
            }
        }
    }

    private async Task SaveAsync<T>(int worker, T[] rows, string phase, CancellationToken cancellationToken)
        where T : class
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        db.Set<T>().AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);
        progress.Report(worker, rows.Length, phase);
    }
}
