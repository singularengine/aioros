using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class KeyedRow(string id, List<string> saved) : ObservableObject
{
    public string Id { get; } = id;

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Note { get; set; } = "";

    [RelayCommand]
    private void Save() => saved.Add(Id + "=" + Text);
}

public sealed partial class KeyedRows : ObservableObject
{
    private readonly List<KeyedRow> _all;

    public KeyedRows(params string[] ids)
    {
        _all = [.. ids.Select(id => new KeyedRow(id, Saved))];
        Rows = new ObservableCollection<KeyedRow>(_all);
    }

    public List<string> Saved { get; } = [];

    public ObservableCollection<KeyedRow> Rows { get; }

    public string Label { get; set; } = "";

    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    public List<string> FilterHistory { get; } = [];

    partial void OnFilterChanged(string value)
    {
        FilterHistory.Add(value + ":" + string.Join(",", Rows.Select(row => row.Text)));
        Rows.Clear();
        foreach (var row in _all.Where(row => row.Id.Contains(value, StringComparison.Ordinal)))
        {
            Rows.Add(row);
        }
    }
}

public sealed record CodedItem(string Id, string Code)
{
    public string Text { get; set; } = "";
}

public sealed record PlainItem(string Name)
{
    public string Text { get; set; } = "";
}

public sealed class IdentityLists(IReadOnlyList<CodedItem> coded, IReadOnlyList<string> names, IReadOnlyList<PlainItem> plain)
{
    public IReadOnlyList<CodedItem> Coded { get; } = coded;

    public IReadOnlyList<string> Names { get; } = names;

    public IReadOnlyList<PlainItem> Plain { get; } = plain;
}

public sealed class PlainRow
{
    public string Text { get; set; } = "";
}

public sealed class PlainRows(params PlainRow[] rows)
{
    public IReadOnlyList<PlainRow> Rows { get; } = rows;

    public string Label { get; set; } = "";
}

public sealed partial class KeyedBranch(string id, params KeyedBranch[] children) : ObservableObject
{
    public string Id { get; } = id;

    public IReadOnlyList<KeyedBranch> Children { get; } = children;

    [ObservableProperty]
    public partial string Text { get; set; } = "";
}

public sealed class KeyedTree(params KeyedBranch[] roots)
{
    public IReadOnlyList<KeyedBranch> Roots { get; } = roots;
}
