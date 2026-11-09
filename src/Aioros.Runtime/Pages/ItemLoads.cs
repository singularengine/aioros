using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Pages;

/// <summary>One item's result of a repeated view's loader: its key, the input the loader took, and what it answered.</summary>
public readonly record struct ItemResult(string Key, object? Input, LoaderOutcome Outcome);

/// <summary>What a repeated view's loader answered for the items of its list, in list order, one per distinct key.</summary>
public sealed class ItemResults(ImmutableArray<ItemResult> items)
{
    public ImmutableArray<ItemResult> Items { get; } = items;
}

/// <summary>
/// Loads the views repeated in lists, once the models of their hosts exist: the keys come from the host model's items, as
/// Phoenix LiveComponent's <c>update_many/1</c> receives every instance of a component once the parent rendered, and a
/// batch loader runs once for all of them with the distinct keys in list order, as DataLoader batches a tick's loads; a
/// loader for one instance runs once per item (the N+1 shape, <c>AIOROS020</c>). Each run has its scope, its error
/// boundary and its keys, under the load's memo and limits.
/// </summary>
internal static class ItemLoads
{
    private static readonly LoaderOutcome Hidden = new(ViewState.Hidden);

    private static readonly LoaderOutcome Failed = new(ViewState.Failed);

    /// <summary>
    /// Loads every repeated slot that runs (all of them by default, otherwise those given) and has no results yet, from the
    /// models of their hosts, writing each slot's results into <paramref name="results"/> and its keys into the keys.
    /// </summary>
    public static async ValueTask<ImmutableArray<string>[]?> LoadAsync(IServiceProvider services, PageDefinition page, PageRequest request, PageLoad load, object?[] models, LoaderOutcome[] results, ImmutableArray<string>[]? keys, ImmutableArray<int> only, CancellationToken cancellationToken)
    {
        var slots = new List<(int Slot, ImmutableArray<(string Key, object? Input)> Entries)>();
        for (var slot = page.FirstView; slot < page.ModelCount; slot++)
        {
            if (PageLoader.Repeated(page, slot) && PageLoader.LoaderAt(page, slot) is not null && (only.IsDefault || only.Contains(slot)) && results[slot].Data is not ItemResults)
            {
                slots.Add((slot, Entries(page, slot, models)));
            }
        }

        if (slots.Count == 0)
        {
            return keys;
        }

        var run = new LoaderRuns.Run(services, request, load, slots.Sum(slot => PageLoader.LoaderAt(page, slot.Slot)!.LoadManyAsync is null ? slot.Entries.Length : 1), cancellationToken);
        var loads = slots.Select(slot => SlotAsync(page, run, slot.Slot, slot.Entries).AsTask()).ToList();
        await Task.WhenAll(loads);
        for (var index = 0; index < slots.Count; index++)
        {
            var (outcome, read) = await loads[index];
            results[slots[index].Slot] = outcome;
            if (!read.IsDefaultOrEmpty)
            {
                keys ??= new ImmutableArray<string>[page.ModelCount];
                keys[slots[index].Slot] = read;
            }
        }

        return keys;
    }

    /// <summary>Runs a repeated view's loader for one item again, with that item's input alone.</summary>
    public static ValueTask<(LoaderOutcome Outcome, ImmutableArray<string> Keys)> LoadItemAsync(IServiceProvider services, PageDefinition page, PageRequest request, int slot, string key, object? input, CancellationToken cancellationToken) =>
        SlotAsync(page, new LoaderRuns.Run(services, request, PageLoad.For(services), 1, cancellationToken), slot, [(key, input)]);

    /// <summary>
    /// The keys and inputs of a repeated slot's items, from its host's model, distinct and in list order: the declared key's
    /// text and value, or the item's position and the item itself.
    /// </summary>
    public static ImmutableArray<(string Key, object? Input)> Entries(PageDefinition page, int slot, IReadOnlyList<object?> models)
    {
        var instance = page.Views[slot - page.FirstView];
        var host = models[instance.Parent < 0 ? instance.Segment : page.FirstView + instance.Parent];
        if (host is ViewState || instance.List!.Items(host) is not { } items)
        {
            return [];
        }

        var entries = ImmutableArray.CreateBuilder<(string, object?)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        foreach (var item in items)
        {
            var input = instance.List.Key is { } read ? read(item) : item;
            var key = instance.List.Key is null ? (position++).ToString(System.Globalization.CultureInfo.InvariantCulture) : ViewItems.Format(input);
            if (seen.Add(key))
            {
                entries.Add((key, input));
            }
        }

        return entries.ToImmutable();
    }

    /// <summary>
    /// The models of a repeated slot from its loader's results: a kept item's model receives its fresh data when the view
    /// model takes it, any other is created from its data; an item whose result stopped is hidden, one whose view model
    /// cannot be created failed.
    /// </summary>
    public static object? Models(IServiceProvider services, PageDefinition page, int slot, LoaderOutcome result, object? kept)
    {
        if (result.Data is not ItemResults results)
        {
            return result.Data is ViewState state ? state : ViewState.Hidden;
        }

        var loader = PageLoader.LoaderAt(page, slot)!;
        var previous = kept as ViewItems;
        var items = ImmutableArray.CreateBuilder<ViewItem>(results.Items.Length);
        foreach (var item in results.Items)
        {
            items.Add(new ViewItem(item.Key, item.Input, Model(services, page, slot, loader, item.Outcome, previous?.Find(item.Key)?.Model)));
        }

        return new ViewItems(items.MoveToImmutable(), page.Views[slot - page.FirstView].List!.Key);
    }

    /// <summary>One item's model after its loader ran: its marker when it is not shown, the kept model receiving the data, or a new one.</summary>
    public static object? Model(IServiceProvider services, PageDefinition page, int slot, LoaderDefinition loader, LoaderOutcome outcome, object? kept)
    {
        if (outcome.Data is ViewState state)
        {
            return state;
        }

        try
        {
            if (loader.Receive is { } receive && kept is not null and not ViewState)
            {
                receive(kept, outcome.Data);
                return kept;
            }

            return loader.CreateModel(services, outcome.Data);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            LoaderRuns.Log(services, page, slot, error);
            return ViewState.Failed;
        }
    }

    private static async ValueTask<(LoaderOutcome Outcome, ImmutableArray<string> Keys)> SlotAsync(PageDefinition page, LoaderRuns.Run run, int slot, ImmutableArray<(string Key, object? Input)> entries)
    {
        var loader = PageLoader.LoaderAt(page, slot)!;
        if (!await ViewAccess.AllowsAsync(run.Services, run.Request.User, page.Views[slot - page.FirstView].View.Access, run.CancellationToken))
        {
            return (Hidden, default);
        }

        if (loader.LoadManyAsync is { } many)
        {
            return await BatchAsync(page, run, slot, many, entries);
        }

        var runs = entries.Select(entry => OneAsync(page, run, slot, loader, entry).AsTask()).ToList();
        await Task.WhenAll(runs);
        var items = ImmutableArray.CreateBuilder<ItemResult>(entries.Length);
        var keys = ImmutableArray<string>.Empty;
        foreach (var each in runs)
        {
            var (item, read) = await each;
            items.Add(item);
            keys = read.IsDefaultOrEmpty ? keys : keys.AddRange(read.Where(key => !keys.Contains(key)));
        }

        return (new LoaderOutcome(new ItemResults(items.MoveToImmutable())), keys);
    }

    private static async ValueTask<(LoaderOutcome, ImmutableArray<string>)> BatchAsync(PageDefinition page, LoaderRuns.Run run, int slot, Func<IServiceProvider, IReadOnlyList<object?>, PageRequest, CancellationToken, ValueTask<IReadOnlyList<LoaderOutcome>>> many, ImmutableArray<(string Key, object? Input)> entries)
    {
        IReadOnlyList<LoaderOutcome> outcomes;
        ImmutableArray<string> keys;
        try
        {
            (outcomes, keys) = entries.IsEmpty ? ([], default) : await LoaderRuns.ScopedAsync(run, (services, request, cancellationToken) => many(services, [.. entries.Select(static entry => entry.Input)], request, cancellationToken));
        }
        catch (Exception error) when (!run.CancellationToken.IsCancellationRequested)
        {
            LoaderRuns.Log(run.Services, page, slot, error);
            return (new LoaderOutcome(new ItemResults([.. entries.Select(static entry => new ItemResult(entry.Key, entry.Input, Failed))])), default);
        }

        var items = ImmutableArray.CreateBuilder<ItemResult>(entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            var outcome = index < outcomes.Count ? outcomes[index] : Hidden;
            items.Add(new ItemResult(entries[index].Key, entries[index].Input, outcome.Stops ? Hidden : outcome));
        }

        return (new LoaderOutcome(new ItemResults(items.MoveToImmutable())), keys);
    }

    private static async ValueTask<(ItemResult, ImmutableArray<string>)> OneAsync(PageDefinition page, LoaderRuns.Run run, int slot, LoaderDefinition loader, (string Key, object? Input) entry)
    {
        try
        {
            var (outcome, keys) = await LoaderRuns.ScopedAsync(run, loader.LoadAsync);
            return (new ItemResult(entry.Key, entry.Input, outcome.Stops ? Hidden : outcome), keys);
        }
        catch (Exception error) when (!run.CancellationToken.IsCancellationRequested)
        {
            LoaderRuns.Log(run.Services, page, slot, error);
            return (new ItemResult(entry.Key, entry.Input, Failed), default);
        }
    }
}
