using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

public sealed record SlotSpec(string Kind, string? Path = null, string? Argument = null)
{
    public ImmutableArray<string> Values { get; init; } = [];

    public static SlotSpec Bound(string path) => new("path", path);
}

public interface ISlotResolver
{
    ValueGetter Getter(SlotSpec spec);

    ValueFormatter? Formatter(SlotSpec? spec);

    /// <summary>The format of a value slot written without a string when it can be, beside its <see cref="Formatter"/>; none by default.</summary>
    ValueSpanFormatter? SpanFormatter(SlotSpec? spec) => null;

    /// <summary>The format of a raw value slot as UTF-8 bytes encoded once; none by default.</summary>
    ValueUtf8Formatter? Utf8Formatter(SlotSpec? spec) => null;

    ChoicePredicate Predicate(SlotSpec spec);

    /// <summary>The chooser of a template selector slot, from the generated selectors by default.</summary>
    Func<object?, int> Chooser(SlotSpec spec) => Slots.PlanSelectors.Chooser(spec) ?? throw new System.Text.Json.JsonException($"The template selector '{spec?.Argument}' has no generated entry.");

    /// <summary>The slot writer a writer node names, reading members of its data through the generated accessors by default.</summary>
    SlotRender Writer(SlotSpec spec) => SlotWriters.Create(spec, static (owner, member) =>
        MemberAccessors.Getter(owner, member) ?? throw new InvalidOperationException($"No generated getter for {owner.FullName}.{member}."));

    /// <summary>The type a typed template's case names by its assembly-qualified name, from the generated types by default.</summary>
    Type TypeOf(string name) => Slots.PlanTypes.Find(name) ?? throw new System.Text.Json.JsonException($"The type '{name}' has no generated entry.");
}
