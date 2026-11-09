using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Aioros.Pages;
using Aioros.Runtime.Functions;

namespace Aioros.Runtime.Pages;

/// <summary>
/// Which loaders a command in a view runs again, from the keys each loader read in its last load and the keys the
/// command's server functions invalidated: SvelteKit reruns the loads that depend on an invalidated key, Next.js
/// revalidates what carries an updated tag.
/// </summary>
public static class PageKeys
{
    /// <summary>
    /// The slots a command in the view at <paramref name="view"/> refreshes, in slot order: every slot with a loader when
    /// the command called a server function without key information (the safe default), otherwise the view's own slot and
    /// every slot, the page's and its layouts' included, whose read keys meet the invalidated ones; the view's own slot
    /// alone when the command invalidated nothing.
    /// </summary>
    public static ImmutableArray<int> Invalidated(PageDefinition page, int view, LoadedPage loaded, DataKeys? changes)
    {
        ArgumentNullException.ThrowIfNull(page);
        var invalidated = changes?.Invalidated;
        if (changes is not { Unknown: true } && (invalidated is null || invalidated.IsEmpty || loaded.Keys.IsDefaultOrEmpty))
        {
            return view >= page.FirstView && view < page.ModelCount && PageLoader.LoaderAt(page, view) is not null ? [view] : [];
        }

        var slots = ImmutableArray.CreateBuilder<int>();
        for (var slot = 0; slot < page.ModelCount; slot++)
        {
            if (PageLoader.LoaderAt(page, slot) is not null && (changes!.Unknown || slot == view || Meets(loaded.Keys, slot, invalidated!)))
            {
                slots.Add(slot);
            }
        }

        return slots.ToImmutable();
    }

    /// <summary>
    /// Whether what a command changed reaches a slot through its own read keys, or through a server function without key
    /// information, rather than only because the command ran in it: a repeated view's item refreshes alone otherwise.
    /// </summary>
    public static bool Reaches(LoadedPage loaded, int slot, DataKeys? changes) =>
        changes is { Unknown: true } || (changes?.Invalidated is { IsEmpty: false } invalidated && !loaded.Keys.IsDefaultOrEmpty && Meets(loaded.Keys, slot, invalidated));

    /// <summary>Whether the slots include the page's or a layout's, which answer together and so run again together.</summary>
    public static bool ReachesPage(PageDefinition page, ImmutableArray<int> slots)
    {
        ArgumentNullException.ThrowIfNull(page);
        return !slots.IsDefaultOrEmpty && slots[0] < page.FirstView;
    }

    /// <summary>
    /// The slots an invalidation outside the page's own commands reaches, in slot order: those with a loader whose last
    /// load read an invalidated key, or every slot with a loader after a change whose keys are unknown (SvelteKit's
    /// <c>invalidate(key)</c> and <c>invalidateAll()</c>).
    /// </summary>
    public static ImmutableArray<int> Reading(PageDefinition page, LoadedPage loaded, Caching.Invalidation invalidation)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(invalidation);
        var slots = ImmutableArray.CreateBuilder<int>();
        for (var slot = 0; slot < page.ModelCount; slot++)
        {
            if (PageLoader.LoaderAt(page, slot) is not null && (invalidation.All || (!loaded.Keys.IsDefault && slot < loaded.Keys.Length && invalidation.Meets(loaded.Keys[slot]))))
            {
                slots.Add(slot);
            }
        }

        return slots.ToImmutable();
    }

    /// <summary>Every key the page's loaders read in their last load, once each and in ordinal order: what a live page watches.</summary>
    public static ImmutableArray<string> All(ImmutableArray<ImmutableArray<string>> keys) =>
        keys.IsDefaultOrEmpty ? [] : [.. keys.Where(static own => !own.IsDefault).SelectMany(static own => own).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private static bool Meets(ImmutableArray<ImmutableArray<string>> keys, int slot, ImmutableHashSet<string> invalidated) =>
        slot < keys.Length && !keys[slot].IsDefaultOrEmpty && keys[slot].Any(invalidated.Contains);

    /// <summary>
    /// The keys per slot after loaders ran: the fresh keys for the slots that ran, the kept keys for the others; empty while
    /// no slot has any, so a page without keys allocates nothing for them.
    /// </summary>
    internal static ImmutableArray<ImmutableArray<string>> After(ImmutableArray<string>[]? fresh, ImmutableArray<ImmutableArray<string>> kept, int count, Func<int, bool>? ran = null)
    {
        if (fresh is null && kept.IsDefaultOrEmpty)
        {
            return [];
        }

        var keys = new ImmutableArray<string>[count];
        for (var slot = 0; slot < count; slot++)
        {
            var own = ran is null || ran(slot) ? Of(fresh, slot) : Of(kept, slot);
            keys[slot] = own.IsDefault ? [] : own;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(keys);
    }

    private static ImmutableArray<string> Of(ImmutableArray<string>[]? keys, int slot) => keys is not null && slot < keys.Length ? keys[slot] : [];

    private static ImmutableArray<string> Of(ImmutableArray<ImmutableArray<string>> keys, int slot) => !keys.IsDefault && slot < keys.Length ? keys[slot] : [];
}
