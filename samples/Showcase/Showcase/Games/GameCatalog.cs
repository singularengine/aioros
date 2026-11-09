using System.Collections.Immutable;
using System.Globalization;

namespace Showcase.Games;

/// <summary>Makes up the store's games, players and reviews from a seed, so every run builds the same store.</summary>
public static class GameCatalog
{
    public static readonly ImmutableArray<string> Genres = ["Action", "Adventure", "RPG", "Strategy", "Simulation", "Racing", "Puzzle", "Horror", "Shooter", "Survival", "Sports", "Indie"];

    private static readonly string[] Tags = ["Open World", "Co-op", "Multiplayer", "Roguelike", "Pixel Graphics", "Story Rich", "Sci-fi", "Fantasy", "Atmospheric", "Difficult", "Relaxing", "Sandbox", "Base Building", "Turn-Based", "Platformer", "Metroidvania", "Crafting", "Space", "Cyberpunk", "Exploration", "Physics", "Stealth", "Tactical", "Cozy"];
    private static readonly string[] First = ["Shadow", "Star", "Iron", "Neon", "Crystal", "Hollow", "Echo", "Ember", "Void", "Rogue", "Quantum", "Pixel", "Frost", "Solar", "Night", "Wild", "Last", "Lost", "Eternal", "Crimson", "Silent", "Broken", "Golden", "Astral", "Feral", "Sunken", "Atomic", "Velvet", "Thunder", "Paper"];
    private static readonly string[] Second = ["Kingdom", "Frontier", "Odyssey", "Legends", "Tactics", "Drift", "Protocol", "Harbor", "Dynasty", "Outpost", "Rift", "Saga", "Arena", "Garden", "Circuit", "Expanse", "Covenant", "Horizon", "Abyss", "Citadel", "Colony", "Requiem", "Express", "Hunters", "Ascent", "Siege", "Gambit", "Wardens", "Orbit", "Tides"];
    private static readonly string[] Endings = ["", "", "", "", " II", " III", ": Remastered", ": Origins", " Online", " Deluxe", ": Reborn", " Zero"];
    private static readonly string[] StudioFirst = ["Moonlit", "Brave", "Tiny", "Red Fox", "Northwind", "Copper", "Lantern", "Blue Owl", "Gravity", "Hexagon", "Sparrow", "Obsidian", "Cloudburst", "Pinecone", "Starling"];
    private static readonly string[] StudioSecond = ["Games", "Studios", "Interactive", "Works", "Labs", "Collective", "Entertainment", "Forge"];
    private static readonly string[] Glyphs = ["gamepad-2", "sword", "swords", "rocket", "ghost", "crown", "skull", "gem", "flame", "mountain", "car", "trophy", "puzzle", "shield", "star", "cpu", "target", "crosshair", "tree-pine", "wand-sparkles", "fish", "bot", "cat", "castle"];
    private static readonly string[] Palette = ["#5B5BD6", "#D6336C", "#E8590C", "#0CA678", "#1C7ED6", "#F08C00", "#7048E8", "#C2255C", "#2F9E44", "#1098AD", "#E03131", "#9C36B5", "#3B5BDB", "#F76707", "#087F5B"];
    private static readonly string[] Names = ["ada", "turing", "hopper", "linus", "grace", "radia", "ken", "dennis", "margaret", "edsger", "barbara", "frances", "hedy", "donald", "anita", "bjarne", "sophie", "guido", "yuki", "carol", "leslie", "shafi", "niklaus", "lynn", "vint", "tim", "james", "mary", "katherine", "john"];
    private static readonly string[] Handles = ["_gg", "42", "_x", "pro", "99", "_ttv", "007", "dev", "zz", "_mk", "1337", "_jp", "_mx", "_br", "_es"];
    private static readonly string[] Countries = ["US", "MX", "ES", "DE", "JP", "BR", "CA", "FR", "IN", "AR", "CL", "GB", "IT", "CO", "SE", "KR", "PL"];
    private static readonly string[] Praise = ["Lost a whole weekend to this. No regrets.", "The soundtrack alone is worth it.", "Tight controls and a gorgeous art style.", "Best co-op night we've had in years.", "Deep systems that keep surprising me.", "Runs great, looks better.", "A small studio made something special here.", "Every update makes it better."];
    private static readonly string[] Complaints = ["Fun for an hour, then it repeats.", "Needs more content for the price.", "The ending felt rushed.", "Performance dips in the late game.", "Matchmaking takes forever.", "Great idea, rough execution."];

    public static Game Game(int id)
    {
        var random = new Random(id * 7919);
        var price = random.NextDouble() < 0.12 ? 0m : new[] { 4.99m, 9.99m, 14.99m, 19.99m, 24.99m, 29.99m, 39.99m, 59.99m, 69.99m }[random.Next(9)];
        var popularity = Math.Pow(random.NextDouble(), 3);
        var genre = Genres[random.Next(Genres.Length)];
        var tags = Tags.OrderBy(_ => random.Next()).Take(3 + random.Next(3)).ToList();
        var title = First[random.Next(First.Length)] + " " + Second[random.Next(Second.Length)] + Endings[random.Next(Endings.Length)];
        return new Game
        {
            Id = id,
            Title = title,
            Studio = StudioFirst[random.Next(StudioFirst.Length)] + " " + StudioSecond[random.Next(StudioSecond.Length)],
            Genre = genre,
            Tags = tags,
            Summary = Summary(random, genre, tags),
            Price = price,
            Discount = price > 0 && random.NextDouble() < 0.3 ? new[] { 10, 20, 25, 33, 50, 75 }[random.Next(6)] : 0,
            Released = new DateOnly(2012, 1, 1).AddDays(random.Next(5300)),
            Cover = Palette[random.Next(Palette.Length)],
            Accent = Palette[random.Next(Palette.Length)],
            Glyph = Glyphs[random.Next(Glyphs.Length)],
            Peak = (int)(popularity * 250_000) + random.Next(40),
        };
    }

    public static Player Player(int id)
    {
        var random = new Random(id * 104_729);
        return new Player
        {
            Id = id,
            Name = Names[random.Next(Names.Length)] + Handles[random.Next(Handles.Length)] + id.ToString(CultureInfo.InvariantCulture)[^Math.Min(2, id.ToString(CultureInfo.InvariantCulture).Length)..],
            Country = Countries[random.Next(Countries.Length)],
            Level = 1 + (int)(Math.Pow(random.NextDouble(), 2) * 300),
        };
    }

    /// <summary>How many reviews and sales a game gets, from its popularity: a few hits and a long tail.</summary>
    public static (int Reviews, int Sales, double Liked) Reach(Game game, double scale)
    {
        ArgumentNullException.ThrowIfNull(game);
        var random = new Random(game.Id * 31);
        var weight = Math.Max(0.02, game.Peak / 250_000.0);
        return ((int)(weight * 240 * scale) + random.Next(3), (int)(weight * 360 * scale) + random.Next(4), 0.45 + (random.NextDouble() * 0.53));
    }

    public static Review Review(Random random, int game, int player, double liked, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(random);
        var recommended = random.NextDouble() < liked;
        return new Review
        {
            GameId = game,
            PlayerId = player,
            Recommended = recommended,
            Hours = 1 + (int)(Math.Pow(random.NextDouble(), 2) * 900),
            Text = recommended ? Praise[random.Next(Praise.Length)] : Complaints[random.Next(Complaints.Length)],
            At = at,
        };
    }

    private static string Summary(Random random, string genre, List<string> tags) =>
        random.Next(4) switch
        {
            0 => $"A {genre.ToLowerInvariant()} game about a crew that has one night to save a city that does not want saving. {tags[0]} meets {tags[1].ToLowerInvariant()}.",
            1 => $"Build, explore and survive in a world that changes every run. Hand-made {tags[0].ToLowerInvariant()} levels, {tags[1].ToLowerInvariant()} and a story you shape.",
            2 => $"From a tiny studio with big ideas: {genre.ToLowerInvariant()} at its core, with {tags[0].ToLowerInvariant()} and {tags[1].ToLowerInvariant()} woven through every level.",
            _ => $"Lead your squad across a shattered frontier. {tags[0]}, {tags[1].ToLowerInvariant()} and a soundtrack you will hum for weeks.",
        };
}
