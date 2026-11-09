using System.Collections.Immutable;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Slots;

/// <summary>One template a selector holds, as the plans file names it: the member that holds it and its key there (empty for a property).</summary>
public sealed record SelectorEntry(string Member, string Key);

/// <summary>
/// An app's template selector, by its full name, with generated code that rebuilds it from its entries and returns a
/// function giving, for an item, the index of the entry its own <c>Build</c> picks (-1 for none), or nothing when an
/// entry names a member it does not have.
/// </summary>
public sealed record SelectorCode(string Name, Func<ImmutableArray<SelectorEntry>, Func<object?, int>?> Create);

/// <summary>
/// The app's template selectors, generated at build time, found by the name a plan's <c>selector</c> slot gives them
/// (the selector's type, and its entries as member and key pairs), instead of rebuilt by reflection.
/// </summary>
public static class PlanSelectors
{
    private static ImmutableDictionary<string, Func<ImmutableArray<SelectorEntry>, Func<object?, int>?>> _generated =
        ImmutableDictionary<string, Func<ImmutableArray<SelectorEntry>, Func<object?, int>?>>.Empty.WithComparers(StringComparer.Ordinal);

    public static void Register(ImmutableArray<SelectorCode> selectors)
    {
        if (selectors.IsDefaultOrEmpty)
        {
            return;
        }

        ImmutableInterlocked.Update(ref _generated, static (all, added) => all.SetItems(added.Select(selector => KeyValuePair.Create(selector.Name, selector.Create))), selectors);
    }

    /// <summary>A selector slot: the selector's type name, and each entry as its member, a line break and its key.</summary>
    public static SlotSpec Spec(string type, IEnumerable<SelectorEntry> entries) =>
        new("selector", null, type) { Values = [.. entries.Select(entry => entry.Member + "\n" + entry.Key)] };

    public static ImmutableArray<SelectorEntry> Entries(SlotSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return [.. spec.Values.Select(static value => value.Split('\n', 2) is [var member, var key] ? new SelectorEntry(member, key) : new SelectorEntry(value, string.Empty))];
    }

    /// <summary>The chooser the app's generated code builds for this slot, or nothing when the app has none for its selector.</summary>
    public static Func<object?, int>? Chooser(SlotSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Argument is { } name && Volatile.Read(ref _generated).GetValueOrDefault(PlanTypes.FullName(name)) is { } create ? create(Entries(spec)) : null;
    }
}
