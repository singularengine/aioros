using System.Collections;
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Book(string name) : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = name;
}

/// <summary>A list that is only an <see cref="IReadOnlyList{T}"/>, as an app's own ranking may be.</summary>
public sealed class Ranking(params Book[] books) : IReadOnlyList<Book>
{
    public int Count => books.Length;

    public Book this[int index] => books[index];

    public IEnumerator<Book> GetEnumerator() => ((IEnumerable<Book>)books).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class Bookshelf(params string[] names)
{
    public List<Book> Items { get; } = [.. names.Select(name => new Book(name))];

    public Book[] Array { get; } = [.. names.Select(name => new Book(name + " (array)"))];

    public ImmutableArray<Book> Frozen { get; } = [.. names.Select(name => new Book(name + " (frozen)"))];

    public Ranking Ranking { get; } = new([.. names.Select(name => new Book(name + " (ranked)"))]);

    public List<List<int>> Grid { get; } = [[1, 2], [3, 4]];

    public string Label { get; } = "shelf";
}

public sealed record Keyed(Dictionary<string, Tint> Lookup);
