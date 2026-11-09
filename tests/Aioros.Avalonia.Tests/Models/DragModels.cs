using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed record Chip(string Id, string Name);

public sealed class Column(string key, params Chip[] chips)
{
    public string Key => key;

    public IReadOnlyList<Chip> Chips => chips;
}

public sealed partial class Sorting : ObservableObject
{
    public IReadOnlyList<Column> Columns { get; set; } =
    [
        new("todo", new Chip("a", "Alpha"), new Chip("b", "Beta"), new Chip("c", "Gamma")),
        new("done", new Chip("d", "Delta")),
    ];

    public List<(string Item, string Target, int Index)> Moves { get; } = [];

    public List<string> Saved { get; } = [];

    [RelayCommand]
    private void Move((string Item, string Target, int Index) move) => Moves.Add(move);

    [RelayCommand]
    private void Save(string? note) => Saved.Add(note ?? string.Empty);
}
