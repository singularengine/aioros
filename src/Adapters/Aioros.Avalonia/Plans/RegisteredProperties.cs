using System.Collections.Immutable;
using Avalonia;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The properties a control of a type can hold, its own and the attached ones, each once, in the registry's order; listed
/// again when a property was registered since, as Avalonia's registry lists them.
/// </summary>
internal static class RegisteredProperties
{
    private static ImmutableDictionary<Type, Listed> _types = ImmutableDictionary<Type, Listed>.Empty;

    public static AvaloniaProperty[] Of(Type type)
    {
        var registry = AvaloniaPropertyRegistry.Instance;
        var (own, attached) = (registry.GetRegistered(type), registry.GetRegisteredAttached(type));
        if (_types.TryGetValue(type, out var known) && known.Own == own.Count && known.Attached == attached.Count)
        {
            return known.All;
        }

        var listed = new Listed(own.Count, attached.Count, [.. own.Concat(attached).Distinct()]);
        ImmutableInterlocked.AddOrUpdate(ref _types, type, listed, (_, _) => listed);
        return listed.All;
    }

    private sealed record Listed(int Own, int Attached, AvaloniaProperty[] All);
}
