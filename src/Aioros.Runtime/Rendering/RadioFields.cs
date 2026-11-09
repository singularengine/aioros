using System.Collections.Immutable;
using System.Globalization;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// The description of a radio group's field as a plan holds it: the field kind <c>radios</c> and, for each radio in the
/// group's order, what checking or unchecking it writes, flattened into the spec's values so the plans file keeps one spec
/// per field: its bound path through its binding's converter (<c>convert</c>), a path of its own (<c>check</c>), or
/// nothing (<c>none</c>, a radio not bound to write back).
/// </summary>
public static class RadioFields
{
    public const string Kind = "radios";

    /// <summary>The spec of a radio group over its radios' specs.</summary>
    public static SlotSpec Spec(IReadOnlyList<SlotSpec> radios)
    {
        ArgumentNullException.ThrowIfNull(radios);
        var values = ImmutableArray.CreateBuilder<string>();
        values.Add(Invariant(radios.Count));
        foreach (var radio in radios)
        {
            values.Add(radio.Kind);
            values.Add(radio.Path ?? string.Empty);
            values.Add(radio.Argument ?? string.Empty);
            values.Add(Invariant(radio.Values.Length));
            values.AddRange(radio.Values);
        }

        return new SlotSpec(Kind) { Values = values.ToImmutable() };
    }

    /// <summary>The radios' specs a group's spec holds, in the group's order.</summary>
    public static ImmutableArray<SlotSpec> Radios(SlotSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var values = spec.Values;
        var count = int.Parse(values[0], CultureInfo.InvariantCulture);
        var radios = ImmutableArray.CreateBuilder<SlotSpec>(count);
        var at = 1;
        for (var index = 0; index < count; index++)
        {
            var length = int.Parse(values[at + 3], CultureInfo.InvariantCulture);
            radios.Add(new SlotSpec(values[at], Empty(values[at + 1]), Empty(values[at + 2])) { Values = values.Slice(at + 4, length) });
            at += 4 + length;
        }

        return radios.MoveToImmutable();
    }

    private static string? Empty(string value) => value.Length == 0 ? null : value;

    private static string Invariant(int number) => number.ToString(CultureInfo.InvariantCulture);
}
