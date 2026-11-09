using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// A typed getter, and setter when the member can be assigned, generated at build time for one public member of a
/// type that plans read, so a plan reads it without compiling a delegate at run time.
/// </summary>
public sealed record MemberAccessor(Type Owner, string Name, Func<object, object?> Get)
{
    /// <summary>
    /// The type a value must have to be assigned through <see cref="Set"/>, returned when asked for, so creating the
    /// accessors loads no type of a member's value.
    /// </summary>
    public Func<Type>? ValueType { get; init; }

    public Action<object, object?>? Set { get; init; }
}

/// <summary>
/// The generated accessors of a type the app does not load until a value of it is read (an Avalonia <c>Point</c>), by
/// the type's full name, created the first time a plan reads a member of one.
/// </summary>
public sealed record DeferredAccessors(string Owner, Func<ImmutableArray<MemberAccessor>> Create);

/// <summary>How many of the member reads and writes of plans were served by generated accessors, and which were not.</summary>
public sealed record AccessorCoverage(int Generated, int Read, ImmutableArray<string> Runtime)
{
    /// <summary>How many of the writes among them were served by generated setters.</summary>
    public int GeneratedWrites { get; init; }

    /// <summary>How many of them were writes.</summary>
    public int Writes { get; init; }
}

/// <summary>
/// The generated accessors of the app, by the exact type a value has at run time and the member's name, as a plan looks
/// a member up; and what each lookup found, for the precompilation report.
/// </summary>
public static class MemberAccessors
{
    private static ImmutableDictionary<(Type Owner, string Name), MemberAccessor> _all =
        ImmutableDictionary<(Type Owner, string Name), MemberAccessor>.Empty;

    private static ImmutableDictionary<string, Func<ImmutableArray<MemberAccessor>>> _deferred =
        ImmutableDictionary<string, Func<ImmutableArray<MemberAccessor>>>.Empty.WithComparers(StringComparer.Ordinal);

    private static ImmutableDictionary<(Type Owner, string Name, bool Set), bool> _read =
        ImmutableDictionary<(Type Owner, string Name, bool Set), bool>.Empty;

    public static int Count => Volatile.Read(ref _all).Count;

    public static void Register(ImmutableArray<MemberAccessor> accessors)
    {
        if (accessors.IsDefaultOrEmpty)
        {
            return;
        }

        ImmutableInterlocked.Update(
            ref _all,
            static (all, added) => all.SetItems(added.Select(accessor => KeyValuePair.Create((accessor.Owner, accessor.Name), accessor))),
            accessors);
    }

    public static void Defer(ImmutableArray<DeferredAccessors> deferred)
    {
        if (deferred.IsDefaultOrEmpty)
        {
            return;
        }

        ImmutableInterlocked.Update(
            ref _deferred,
            static (all, added) => all.SetItems(added.Select(group => KeyValuePair.Create(group.Owner, group.Create))),
            deferred);
    }

    /// <summary>The generated getter of a member, noted as read either way.</summary>
    public static Func<object, object?>? Getter(Type owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var found = Find(owner, name)?.Get;
        ImmutableInterlocked.TryAdd(ref _read, (owner, name, false), found is not null);
        return found;
    }

    /// <summary>The generated setter of a member with the type its value must have, noted as written either way.</summary>
    public static (Type ValueType, Action<object, object?> Set)? Setter(Type owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var found = Find(owner, name) is { Set: { } set, ValueType: { } type } ? (type(), set) : ((Type, Action<object, object?>)?)null;
        ImmutableInterlocked.TryAdd(ref _read, (owner, name, true), found is not null);
        return found;
    }

    private static MemberAccessor? Find(Type owner, string name)
    {
        if (Volatile.Read(ref _all).GetValueOrDefault((owner, name)) is { } found)
        {
            return found;
        }

        if (owner.FullName is not { } full || !ImmutableInterlocked.TryRemove(ref _deferred, full, out var create))
        {
            return null;
        }

        Register(create());
        return Volatile.Read(ref _all).GetValueOrDefault((owner, name));
    }

    public static AccessorCoverage Coverage()
    {
        var read = Volatile.Read(ref _read);
        return new AccessorCoverage(
            read.Count(entry => entry.Value),
            read.Count,
            [.. read.Where(entry => !entry.Value).Select(entry => entry.Key.Owner.FullName + "." + entry.Key.Name + (entry.Key.Set ? " (set)" : string.Empty)).Order(StringComparer.Ordinal)])
        {
            GeneratedWrites = read.Count(entry => entry.Key.Set && entry.Value),
            Writes = read.Count(entry => entry.Key.Set),
        };
    }
}
