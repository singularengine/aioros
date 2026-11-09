namespace Aioros.Pages;

/// <summary>
/// The loader of a page or layout, as the generator emits it: <see cref="LoadAsync"/> runs the loader and returns its
/// result with the data untyped, and <see cref="CreateModel"/> builds the view model from that data and the scope's
/// services.
/// </summary>
public sealed record LoaderDefinition(
    Func<IServiceProvider, PageRequest, CancellationToken, ValueTask<LoaderOutcome>> LoadAsync,
    Func<IServiceProvider, object?, object?> CreateModel)
{
    /// <summary>
    /// Pushes new data into a view model that takes it (its public <c>Receive(TData)</c>), so the view model is kept after
    /// a revalidation; <see langword="null"/> when the view model does not take new data and is created again from it.
    /// </summary>
    public Action<object, object?>? Receive { get; init; }

    /// <summary>
    /// Loads the data of several instances at once, one result per input in the inputs' order, for a view repeated in a
    /// list whose loader is an <c>IBatchLoader</c>; <see langword="null"/> for a loader of one instance, which a list runs
    /// once per item.
    /// </summary>
    public Func<IServiceProvider, IReadOnlyList<object?>, PageRequest, CancellationToken, ValueTask<IReadOnlyList<LoaderOutcome>>>? LoadManyAsync { get; init; }

    /// <summary>Writes the loader's data as JSON for the installed app, with code the generator emitted for its type.</summary>
    public Action<System.Text.Json.Utf8JsonWriter, object?>? Write { get; init; }

    /// <summary>Reads the loader's data from the JSON <see cref="Write"/> produced, in the installed app.</summary>
    public Func<System.Text.Json.JsonElement, object?>? Read { get; init; }

    /// <summary>
    /// How long the loader's data stays the same for every visitor, from its <c>[Cached(seconds)]</c>; none when the
    /// loader does not declare it or reads the user, and then its page is never kept in the page cache.
    /// </summary>
    public TimeSpan? Lifetime { get; init; }
}
