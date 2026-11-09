using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Showcase.Games;

public sealed class GamesDb(DbContextOptions<GamesDb> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Game> Games => Set<Game>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<Purchase> Purchases => Set<Purchase>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        if (Database.IsSqlite())
        {
            builder.Entity<Game>().Property(entry => entry.Price).HasConversion<double>();
            builder.Entity<Purchase>().Property(entry => entry.Price).HasConversion<double>();
        }

        builder.Entity<Game>(game =>
        {
            game.Property(entry => entry.Id).ValueGeneratedNever();
            game.Property(entry => entry.Price).HasPrecision(8, 2);
            game.HasIndex(entry => entry.Genre);
            game.HasIndex(entry => entry.Owners);
            game.HasIndex(entry => entry.Released);
        });
        builder.Entity<Player>().Property(entry => entry.Id).ValueGeneratedNever();
        builder.Entity<Review>(review =>
        {
            review.HasIndex(entry => new { entry.GameId, entry.At });
            review.HasOne<Game>().WithMany().HasForeignKey(entry => entry.GameId);
            review.HasOne<Player>().WithMany().HasForeignKey(entry => entry.PlayerId);
        });
        builder.Entity<Purchase>(purchase =>
        {
            purchase.Property(entry => entry.Price).HasPrecision(8, 2);
            purchase.HasIndex(entry => new { entry.GameId, entry.At });
            purchase.HasIndex(entry => entry.UserId);
            purchase.HasOne<Game>().WithMany().HasForeignKey(entry => entry.GameId);
        });
    }
}

public sealed class Game
{
    public int Id { get; init; }

    public required string Title { get; init; }

    public required string Studio { get; init; }

    public required string Genre { get; init; }

    public required List<string> Tags { get; init; }

    public required string Summary { get; init; }

    public decimal Price { get; init; }

    public int Discount { get; init; }

    public DateOnly Released { get; init; }

    public required string Cover { get; init; }

    public required string Accent { get; init; }

    public required string Glyph { get; init; }

    public int Positive { get; set; }

    public int Negative { get; set; }

    public int Owners { get; set; }

    public int Peak { get; init; }
}

public sealed class Player
{
    public int Id { get; init; }

    public required string Name { get; init; }

    public required string Country { get; init; }

    public int Level { get; init; }
}

public sealed class Review
{
    public long Id { get; init; }

    public int GameId { get; init; }

    public int PlayerId { get; init; }

    public bool Recommended { get; init; }

    public int Hours { get; init; }

    public required string Text { get; init; }

    public DateTime At { get; init; }
}

public sealed class Purchase
{
    public long Id { get; init; }

    public int GameId { get; init; }

    public int? PlayerId { get; init; }

    public string? UserId { get; init; }

    public decimal Price { get; init; }

    public DateTime At { get; init; }
}
