using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// A bound value read as a flag the way an Avalonia binding gives it to a <c>bool</c> property (the runtime's resolver
/// reads it). The spec's values are the negation, the default and whether the slot is the flag's complement.
/// </summary>
internal static class Flags
{
    public static Slot Of(string path, bool negate, bool fallback, bool complement = false) =>
        Slots.Of(new SlotSpec("flag", path) { Values = [negate ? "not" : string.Empty, fallback ? "true" : "false", complement ? "complement" : string.Empty] });
}
