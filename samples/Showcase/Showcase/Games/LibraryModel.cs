using System.Globalization;

namespace Showcase.Games;

public sealed record LibraryEntry(GameCard Game, string Bought);

public sealed class LibraryModel(LibraryPage library)
{
    public IReadOnlyList<LibraryEntry> Entries { get; } = library.Entries;

    public string Summary { get; } = string.Create(CultureInfo.InvariantCulture, $"{library.Entries.Count} games · {library.Spent:$#,0.00} spent");

    public bool Empty { get; } = library.Entries.Count == 0;
}
