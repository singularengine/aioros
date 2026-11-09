namespace Showcase.Games;

/// <param name="Database">The database the store runs on, as shown in the lab.</param>
/// <param name="Workers">How many workers seed the store at once (one for SQLite, which allows a single writer).</param>
/// <param name="Scale">How many reviews and sales to seed, relative to the PostgreSQL store.</param>
/// <param name="Crews">The virtual player counts the lab offers. SQLite's .NET provider waits for its write lock on a
/// thread-pool thread, so many writers starve the pool; PostgreSQL's provider is asynchronous all the way.</param>
public sealed record GamesOptions(string Database, int Workers, double Scale, IReadOnlyList<int> Crews);
