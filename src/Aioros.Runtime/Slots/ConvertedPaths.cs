using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Slots;

/// <summary>
/// A member path read through its binding's converter (decision "Deterministic plan coverage"): the path a plan holds for
/// a property bound with a converter carries the converter's description after it, so every slot and every field that
/// reads or writes the path runs the converter as Avalonia's binding does, its <c>Convert</c> when read and its
/// <c>ConvertBack</c> when a post writes it, whatever the control. The description is a <c>convert</c> slot's: the
/// converter's reference, its parameter and the parameter's kind, the bound property's type and the slot's test.
/// </summary>
public static class ConvertedPaths
{
    private const char Mark = '\u001f';

    /// <summary>The path read through the converter <paramref name="convert"/> describes (a <c>convert</c> slot over the path).</summary>
    public static string Of(SlotSpec convert)
    {
        ArgumentNullException.ThrowIfNull(convert);
        return string.Join(Mark, [convert.Path, convert.Argument, .. convert.Values]);
    }

    /// <summary>Whether the path is read through a converter, and that converter's <c>convert</c> slot.</summary>
    public static bool TryRead(string path, out SlotSpec convert)
    {
        ArgumentNullException.ThrowIfNull(path);
        var parts = path.Split(Mark);
        convert = parts.Length > 2 ? new SlotSpec("convert", parts[0], parts[1]) { Values = [.. parts[2..]] } : null!;
        return parts.Length > 2;
    }

    /// <summary>The path without its converter: the members it reads.</summary>
    public static string Inner(string path) => TryRead(path, out var convert) ? convert.Path! : path;
}
