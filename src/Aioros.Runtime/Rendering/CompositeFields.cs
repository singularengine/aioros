using System.Collections.Immutable;
using System.Globalization;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// The description of a slot renderer's composite field (<see cref="SlotPart.Composite"/>) as a plan holds it: the field
/// kind <c>composite</c>, the renderer whose decoder reads its posts, its key, and the description of each member field it
/// is decoded into, flattened into the spec's values so the plans file keeps one spec per field.
/// </summary>
public static class CompositeFields
{
    public const string Kind = "composite";

    /// <summary>The spec of a composite field under <paramref name="key"/> of the renderer <paramref name="writer"/>, over its members' specs.</summary>
    public static SlotSpec Spec(string writer, string key, IReadOnlyList<SlotSpec> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var values = ImmutableArray.CreateBuilder<string>();
        values.Add(key);
        values.Add(Invariant(members.Count));
        foreach (var member in members)
        {
            values.Add(member.Kind);
            values.Add(member.Path ?? string.Empty);
            values.Add(Invariant(member.Values.Length));
            values.AddRange(member.Values);
        }

        return new SlotSpec(Kind, null, writer) { Values = values.ToImmutable() };
    }

    /// <summary>The key a composite field's spec was written under.</summary>
    public static string Key(SlotSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Values[0];
    }

    /// <summary>The member fields' specs a composite field's spec holds, in its decoder's order.</summary>
    public static ImmutableArray<SlotSpec> Members(SlotSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var values = spec.Values;
        var count = int.Parse(values[1], CultureInfo.InvariantCulture);
        var members = ImmutableArray.CreateBuilder<SlotSpec>(count);
        var at = 2;
        for (var index = 0; index < count; index++)
        {
            var length = int.Parse(values[at + 2], CultureInfo.InvariantCulture);
            members.Add(new SlotSpec(values[at], values[at + 1].Length == 0 ? null : values[at + 1]) { Values = values.Slice(at + 3, length) });
            at += 3 + length;
        }

        return members.MoveToImmutable();
    }

    private static string Invariant(int number) => number.ToString(CultureInfo.InvariantCulture);
}
