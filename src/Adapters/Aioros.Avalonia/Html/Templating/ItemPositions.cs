using System.Collections.Immutable;
using Aioros.Avalonia.Html.Variants;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// The body of a list's item at each position (first, middle, last, only) when its look may depend on it: the list is a
/// library's, whose containers it may prepare by index (Aioros.Cosmo's breadcrumb marks its first item), or a
/// <c>:nth-child</c> or <c>:nth-last-child</c> style is in its scope. Each body is learned from a stand-in list of the
/// traced item, of three items and of one, as Avalonia's render shows them.
/// </summary>
internal static class ItemPositions
{
    public static bool Matter(ItemsControl items) => LibraryValues.FromLibrary(items.GetType()) || CountsChildren(items);

    /// <summary>The bodies by position (first, middle, last, only), or empty when the item cannot be learned so.</summary>
    public static ImmutableArray<ImmutableArray<IPlanNode>> Learn(ItemsControl items, Control first, Func<Control, ImmutableArray<IPlanNode>> body)
    {
        var item = items.ItemFromContainer(first) ?? first.DataContext;
        if (item is null)
        {
            return [];
        }

        var three = Bodies(items, [item, item, item], body);
        var only = Bodies(items, [item], body);
        Hosting.Realization.Realize(items);
        TopLevel.GetTopLevel(items)?.UpdateLayout();
        if (three.Length != 3 || only.Length != 1)
        {
            return [];
        }

        return [three[0], three[1], three[2], only[0]];
    }

    /// <summary>Whether every position has the same body.</summary>
    public static bool Same(ImmutableArray<ImmutableArray<IPlanNode>> positions) =>
        positions.Select(position => PlanTemplate.Of(position).Fingerprint).Distinct(StringComparer.Ordinal).Count() == 1;

    /// <summary>
    /// What <paramref name="read"/> takes from the containers of a bound list's stand-in lists of its first item, first,
    /// middle, last and only, with the list's own containers realized again after; empty when the list has no item.
    /// </summary>
    public static ImmutableArray<T> Containers<T>(ItemsControl items, Func<Control, T> read)
    {
        if (items.ItemCount == 0 || items.ContainerFromIndex(0) is not { } first || (items.ItemFromContainer(first) ?? first.DataContext) is not { } item)
        {
            return [];
        }

        var three = Read(items, [item, item, item], read);
        var only = Read(items, [item], read);
        Hosting.Realization.Realize(items);
        TopLevel.GetTopLevel(items)?.UpdateLayout();
        return three.Length == 3 && only.Length == 1 ? [three[0], three[1], three[2], only[0]] : [];
    }

    /// <summary>
    /// What <paramref name="read"/> takes from the first container of a stand-in list of three of <paramref name="item"/>
    /// (how the item looks at the first position), with the list's own containers realized again after.
    /// </summary>
    public static T? First<T>(ItemsControl items, object? item, Func<Control, T> read)
    {
        var first = default(T);
        using (StandIns.Fill(items, [item, item, item]))
        {
            if (items.ContainerFromIndex(0) is { } container)
            {
                first = read(container);
            }
        }

        Hosting.Realization.Realize(items);
        TopLevel.GetTopLevel(items)?.UpdateLayout();
        return first;
    }

    private static ImmutableArray<T> Read<T>(ItemsControl items, object?[] list, Func<Control, T> read)
    {
        using var lease = StandIns.Fill(items, list);
        return [.. Enumerable.Range(0, items.ItemCount).Select(items.ContainerFromIndex).OfType<Control>().Select(read)];
    }

    private static ImmutableArray<ImmutableArray<IPlanNode>> Bodies(ItemsControl items, object?[] list, Func<Control, ImmutableArray<IPlanNode>> body)
    {
        using var lease = StandIns.Fill(items, list);
        return items.ItemsPanelRoot is { } panel ? [.. panel.Children.Select(body)] : [];
    }

    public static bool CountsChildren(Control control)
    {
        var sources = new List<IStyle>();
        for (StyledElement? element = control; element is not null; element = element.Parent as StyledElement)
        {
            sources.AddRange(element.Styles);
        }

        if (Application.Current is { } application)
        {
            sources.AddRange(application.Styles);
        }

        return Flatten(sources).OfType<Style>().Any(style => style.Selector?.ToString() is { } selector && selector.Contains(":nth-", StringComparison.Ordinal));
    }

    private static IEnumerable<IStyle> Flatten(IEnumerable<IStyle> styles) =>
        styles.SelectMany(style => style is Styles nested ? Flatten(nested) : style is Style { Children.Count: > 0 } parent ? Flatten(parent.Children).Prepend(style) : [style]);
}
