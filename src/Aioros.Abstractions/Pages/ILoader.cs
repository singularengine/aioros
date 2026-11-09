namespace Aioros.Pages;

/// <summary>
/// Loads the data of the page, layout or view it sits beside (<c>Page.Loader.cs</c> next to <c>Page.axaml</c>,
/// <c>Layout.Loader.cs</c> next to <c>Layout.axaml</c>, <c>Recent.Loader.cs</c> next to a view <c>Recent.axaml</c>),
/// outside its view model, which receives the data through its constructor. Its own services come through its
/// constructor, from a scope of its own. The loaders of a page, its layouts and the views they place run in parallel.
/// </summary>
/// <typeparam name="TData">The data the view model takes in its constructor.</typeparam>
public interface ILoader<TData>
{
    ValueTask<LoaderResult<TData>> LoadAsync(PageRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Loads the data of every instance of a view repeated in a list at once, given all their keys (<c>a:View.ItemKey</c>, or
/// the items themselves without one), as DataLoader's batch function and Phoenix LiveComponent's <c>update_many/1</c> do:
/// one call for the list instead of one per item. It sits beside the view as a loader does (<c>Card.Loader.cs</c>), runs
/// after the loader of the list's host, in a scope of its own, and returns each key's data; a key it returns nothing for
/// hides that item.
/// </summary>
/// <typeparam name="TKey">The key of an item: the type of its <c>a:View.ItemKey</c>, or the item's type without one.</typeparam>
/// <typeparam name="TData">The data each item's view model takes in its constructor.</typeparam>
public interface IBatchLoader<TKey, TData>
    where TKey : notnull
{
    ValueTask<IReadOnlyDictionary<TKey, TData>> LoadAsync(IReadOnlyList<TKey> keys, PageRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Loads what a page or layout needs besides data, as <see cref="ILoader{TData}"/> does: a redirect, a not found, or the
/// title and metadata; its view model, when it has one, takes no loader data. Its result is
/// <see cref="LoaderResult.Redirect"/>, <see cref="LoaderResult.NotFound"/> or a <see cref="LoaderOutcome"/> with only
/// <see cref="LoaderOutcome.Title"/> and <see cref="LoaderOutcome.Metadata"/>.
/// </summary>
public interface ILoader
{
    ValueTask<LoaderOutcome> LoadAsync(PageRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// What a loader answers: its data, given by returning the data itself, with the page's title and metadata when it sets
/// them; or, instead of data, <see cref="LoaderResult.NotFound"/> or <see cref="LoaderResult.Redirect"/>.
/// </summary>
public readonly record struct LoaderResult<TData>
{
    public LoaderResult(TData data) => Data = data;

    public TData Data { get; }

    /// <summary>The page's title; the deepest loader that sets one wins.</summary>
    public string? Title { get; init; }

    /// <summary>The page's search metadata, merged with its layouts' (the deepest wins where it sets a value).</summary>
    public PageMetadata? Metadata { get; init; }

    /// <summary>Answers the not-found page instead of this page.</summary>
    public bool NotFound { get; init; }

    /// <summary>Sends the visitor to this path or URL instead of this page.</summary>
    public string? RedirectTo { get; init; }

    public static implicit operator LoaderResult<TData>(TData data) => new(data);

    public static implicit operator LoaderResult<TData>(LoaderOutcome outcome) =>
        new(outcome.Data is TData data ? data : default!) { Title = outcome.Title, Metadata = outcome.Metadata, NotFound = outcome.NotFound, RedirectTo = outcome.RedirectTo };

    /// <summary>The same result with its data untyped, as the runtime and the wire carry it.</summary>
    public LoaderOutcome ToOutcome() => new(Data) { Title = Title, Metadata = Metadata, NotFound = NotFound, RedirectTo = RedirectTo };
}

/// <summary>A loader's result with its data untyped: what the generated code hands the runtime.</summary>
public readonly record struct LoaderOutcome(object? Data)
{
    public string? Title { get; init; }

    public PageMetadata? Metadata { get; init; }

    public bool NotFound { get; init; }

    public string? RedirectTo { get; init; }

    /// <summary>Whether the page stops here, answering not found or a redirect, so no view model is created.</summary>
    public bool Stops => NotFound || RedirectTo is not null;
}

/// <summary>The results a loader returns instead of data.</summary>
public static class LoaderResult
{
    /// <summary>Answers the data with the page's title and, when given, its search metadata.</summary>
    public static LoaderResult<TData> With<TData>(TData data, string? title, PageMetadata? metadata = null) => new(data) { Title = title, Metadata = metadata };

    /// <summary>Answers the app's not-found page (404 on the web) instead of the page.</summary>
    public static LoaderOutcome NotFound() => new(null) { NotFound = true };

    /// <summary>
    /// Sends the visitor elsewhere: a path, relative to the page as a link on it, or another site's <c>http</c> or
    /// <c>https</c> URL (302 on the web, or 303 after a post; the page stack on the desktop).
    /// </summary>
    public static LoaderOutcome Redirect(string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        return new(null) { RedirectTo = location };
    }
}
