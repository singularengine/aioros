using System.Collections.Immutable;

namespace Aioros.Pages;

/// <summary>
/// A view an app places inside its pages and layouts: an <c>.axaml</c> with a view model (<c>x:DataType</c>), a
/// <c>UserControl</c> with <c>x:Class</c> or a page embedded with <c>&lt;a:View Source="…"/&gt;</c>, with the loader
/// beside it (<c>&lt;View&gt;.Loader.cs</c>) when it has one, as the generator emits it.
/// </summary>
public sealed record ViewDefinition(string Source, Func<IServiceProvider, object?> CreateModel)
{
    /// <summary>
    /// The view's class (<c>x:Class</c>), which the host's XAML creates, returned when asked for so that listing the views
    /// loads no control type; <see langword="null"/> for a page embedded by its source.
    /// </summary>
    public Func<Type>? Type { get; init; }

    /// <summary>The view's loader, when it has one; its view model is then created from the loader's data.</summary>
    public LoaderDefinition? Loader { get; init; }

    /// <summary>The <c>[Authorize]</c> rules of the view's loader; a visitor who does not meet them gets nothing of the view.</summary>
    public ImmutableArray<PageAccess> Access { get; init; } = [];
}

/// <summary>
/// One place where a view appears in a page: its key (the host's file and the XAML position, extended by the keys of the
/// views around it), the segment (layout or page, outermost first) whose XAML holds it, and the instance it is nested in,
/// or <c>-1</c>. Its view model takes the slot after the page's model and the instances before it.
/// </summary>
public sealed record ViewInstance(string Key, int Segment, ViewDefinition View)
{
    /// <summary>The index of the instance whose view holds this one, or <c>-1</c> when the segment's XAML holds it.</summary>
    public int Parent { get; init; } = -1;

    /// <summary>
    /// The list whose item template places the view, when the instance repeats once per item; its slot then holds one model
    /// per item, keyed by the item's key.
    /// </summary>
    public ViewList? List { get; init; }
}

/// <summary>
/// Where a repeated view instance gets its items: the list's ordinal among the lists with an item template in its host
/// (the segment's or the view's XAML around it), the host model's items (its <c>ItemsSource</c> path), and the item's
/// key (<c>a:View.ItemKey</c>) with its parser; without a key the item's position in the list names it and the item
/// itself is the batch loader's input, as React's <c>key</c> falls back to the index.
/// </summary>
public sealed record ViewList(int List, Func<object?, System.Collections.IEnumerable?> Items)
{
    /// <summary>The item's declared key, or <see langword="null"/> when the item's position names it.</summary>
    public Func<object?, object?>? Key { get; init; }

    /// <summary>Reads a key from the text that names it in the item's region, when its type can be parsed.</summary>
    public Func<string, object?>? Parse { get; init; }
}
