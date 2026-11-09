using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Aioros.Runtime.Slots;

/// <summary>A type a plan names, by its full name, with a function that returns it when it is asked for.</summary>
public sealed record PlanType(string Name, Func<Type> Resolve);

/// <summary>
/// The types plans name in the plans file (a typed template's cases, a converter's target), found by name among the
/// framework's own and the app's generated entries instead of by <c>Type.GetType</c>.
/// </summary>
public static class PlanTypes
{
    private static readonly FrozenDictionary<string, Type> Framework = new[]
    {
        typeof(object), typeof(string), typeof(bool), typeof(bool?), typeof(int), typeof(int?), typeof(long), typeof(long?),
        typeof(double), typeof(double?), typeof(float), typeof(float?), typeof(decimal), typeof(decimal?), typeof(DateTime), typeof(DateTime?),
        typeof(DateTimeOffset), typeof(DateTimeOffset?), typeof(TimeSpan), typeof(TimeSpan?), typeof(Uri),
    }.ToFrozenDictionary(type => type.FullName!, StringComparer.Ordinal);

    private static ImmutableDictionary<string, Func<Type>> _generated = ImmutableDictionary<string, Func<Type>>.Empty.WithComparers(StringComparer.Ordinal);

    public static void Register(ImmutableArray<PlanType> types)
    {
        if (types.IsDefaultOrEmpty)
        {
            return;
        }

        ImmutableInterlocked.Update(ref _generated, static (all, added) => all.SetItems(added.Select(type => KeyValuePair.Create(type.Name, type.Resolve))), types);
    }

    /// <summary>The type an assembly-qualified or full name names, or nothing when neither the framework nor the app lists it.</summary>
    public static Type? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var full = FullName(name);
        return Framework.TryGetValue(full, out var known) ? known : Volatile.Read(ref _generated).GetValueOrDefault(full)?.Invoke();
    }

    /// <summary>The full name in an assembly-qualified name: everything before the first comma outside brackets.</summary>
    public static string FullName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var depth = 0;
        for (var index = 0; index < name.Length; index++)
        {
            switch (name[index])
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return name[..index].Trim();
            }
        }

        return name.Trim();
    }
}
