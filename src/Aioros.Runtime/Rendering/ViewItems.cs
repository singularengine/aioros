using System.Collections.Immutable;
using System.Globalization;

namespace Aioros.Runtime.Rendering;

/// <summary>One item of a repeated view: its key, the input its batch loader took (the key, or the item), and its model.</summary>
public readonly record struct ViewItem(string Key, object? Input, object? Model);

/// <summary>
/// What a repeated view's slot holds: one model per item of its list, keyed by the item's key (<c>a:View.ItemKey</c>, or
/// the item's position without one), as React's <c>key</c> and LiveView's stream ids name each instance. The plan's view
/// node finds an item's model from the item it renders, so the plan stays one per route.
/// </summary>
public sealed class ViewItems
{
    private readonly Dictionary<string, int> _byKey;
    private readonly Dictionary<object, string>? _byItem;

    /// <summary>The items and their key; the lookups are built here and only read afterwards, so the instance never changes.</summary>
    public ViewItems(ImmutableArray<ViewItem> items, Func<object?, object?>? key)
    {
        Items = items;
        Key = key;
        _byKey = new Dictionary<string, int>(items.Length, StringComparer.Ordinal);
        _byItem = key is null ? new Dictionary<object, string>(items.Length, ReferenceEqualityComparer.Instance) : null;
        for (var index = 0; index < items.Length; index++)
        {
            _byKey.TryAdd(items[index].Key, index);
            if (_byItem is not null && items[index].Input is { } input)
            {
                _byItem.TryAdd(input, items[index].Key);
            }
        }
    }

    public ImmutableArray<ViewItem> Items { get; }

    /// <summary>The item's declared key, or <see langword="null"/> when its position names it.</summary>
    public Func<object?, object?>? Key { get; }

    /// <summary>The key of a list item: its declared key's text, or its position in the list the models were loaded for.</summary>
    public string? KeyOf(object? item) => Key is { } key ? Format(key(item)) : item is not null && _byItem!.TryGetValue(item, out var position) ? position : null;

    /// <summary>The item's model, or the hidden marker for an item the slot holds no model for.</summary>
    public object? Model(string key) => _byKey.TryGetValue(key, out var index) ? Items[index].Model : ViewState.Hidden;

    public ViewItem? Find(string key) => _byKey.TryGetValue(key, out var index) ? Items[index] : null;

    /// <summary>The same items with one item's model replaced.</summary>
    public ViewItems With(string key, object? model) =>
        _byKey.TryGetValue(key, out var index) ? new ViewItems(Items.SetItem(index, Items[index] with { Model = model }), Key) : this;

    /// <summary>A key's text, in the invariant culture.</summary>
    public static string Format(object? key) => Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>An item's key as its region names it, after the instance key: escaped as a URI component.</summary>
    public static string Region(string key) => Uri.EscapeDataString(key);

    /// <summary>An item's key as its fields' names hold it: escaped further so that no <c>.</c> or <c>-</c> is left.</summary>
    public static string Token(string key) => Region(key).Replace(".", "%2E", StringComparison.Ordinal).Replace("-", "%2D", StringComparison.Ordinal);

    /// <summary>The region key of one item of a repeated instance: <c>{instance key}[{item key}]</c>.</summary>
    public static string RegionKey(string instance, string item) => instance + "[" + Region(item) + "]";
}

/// <summary>Where a post's command belongs: a view's slot and, for a repeated view, its item; the page's is slot <c>-1</c>.</summary>
public readonly record struct ViewTarget(int Slot, string? Item = null)
{
    public static ViewTarget Page { get; } = new(-1);
}
