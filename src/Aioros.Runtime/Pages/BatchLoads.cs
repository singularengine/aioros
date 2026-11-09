using Aioros.Pages;

namespace Aioros.Runtime.Pages;

/// <summary>Runs a batch loader for the generated code: the keys typed, and one result per key in their order.</summary>
public static class BatchLoads
{
    /// <summary>
    /// Calls the loader once with every key and answers each key's data, or not found for a key it returned nothing for,
    /// which hides that item as a view's not found hides the view.
    /// </summary>
    public static async ValueTask<IReadOnlyList<LoaderOutcome>> LoadAsync<TKey, TData>(IBatchLoader<TKey, TData> loader, IReadOnlyList<object?> inputs, PageRequest request, CancellationToken cancellationToken)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(inputs);
        var keys = new TKey[inputs.Count];
        for (var index = 0; index < keys.Length; index++)
        {
            keys[index] = (TKey)inputs[index]!;
        }

        var data = await loader.LoadAsync(keys, request, cancellationToken);
        var outcomes = new LoaderOutcome[keys.Length];
        for (var index = 0; index < keys.Length; index++)
        {
            outcomes[index] = data.TryGetValue(keys[index], out var value) ? new LoaderOutcome(value) : LoaderResult.NotFound();
        }

        return outcomes;
    }
}
