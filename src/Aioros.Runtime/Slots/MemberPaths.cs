using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Slots;

/// <summary>
/// Getters and setters for member paths (<c>Owner.Name</c>, <c>^1.Delete</c> for the scope one level up,
/// <c>Items[1].Name</c> through an integer index of a list), each member read through the app's generated accessor for the
/// value's run-time type, or through the fallback when there is none; with <c>generated</c> false, through the fallback
/// only, to check that both read the same. An index reads a list's element through <see cref="IList"/>, or a read-only
/// list's through its <c>[n]</c> accessor; out of range, or on a value that is no list, it reads nothing, as Avalonia's
/// binding then gives no value.
/// </summary>
public sealed class MemberPaths(ISlotFallback? fallback, bool generated = true)
{
    private ImmutableDictionary<(Type Type, string Member), Func<object, object?>> _getters =
        ImmutableDictionary<(Type Type, string Member), Func<object, object?>>.Empty;

    private ImmutableDictionary<(Type Type, string Member), (Type Value, Action<object, object?> Set)> _setters =
        ImmutableDictionary<(Type Type, string Member), (Type Value, Action<object, object?> Set)>.Empty;

    public ISlotFallback? Fallback => fallback;

    public bool Generated => generated;

    public ValueGetter Getter(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.StartsWith('^'))
        {
            var dot = path.IndexOf('.', StringComparison.Ordinal);
            var depth = int.Parse(path.AsSpan(1, (dot < 0 ? path.Length : dot) - 1), CultureInfo.InvariantCulture);
            var rest = dot < 0 ? static scope => scope : Getter(path[(dot + 1)..]);
            return _ => rest(OuterScopes.At(depth));
        }

        if (path.Length == 0)
        {
            return static scope => scope;
        }

        if (ConvertedPaths.TryRead(path, out var convert))
        {
            return fallback?.Getter(convert, this) ?? Getter(convert.Path!);
        }

        if (LocalPaths.TryRead(path, out var local))
        {
            return Local(local);
        }

        var members = Steps(path).Select(name => new Member(this, name)).ToArray();
        return scope =>
        {
            var current = scope;
            foreach (var member in members)
            {
                if (current is null)
                {
                    return null;
                }

                current = member.Read(current);
            }

            return current;
        };
    }

    private static ValueGetter Local(LocalPath local) => _ => LocalValues.TryGet(local.Key, out var value) ? value : local.Initial;

    /// <summary>
    /// The steps a path reads, each a member or an index (<c>[1]</c>): <c>Items[1][0].Name</c> is <c>Items</c>,
    /// <c>[1]</c>, <c>[0]</c>, <c>Name</c>. A member read through Avalonia's stream operator (<c>Title^</c>) is the
    /// member, then <see cref="Stream"/> read on its value: an observable's latest value or a task's result.
    /// </summary>
    public static IEnumerable<string> Steps(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return StepsOf(ConvertedPaths.Inner(path));
    }

    private static IEnumerable<string> StepsOf(string path)
    {
        foreach (var segment in path.Split('.'))
        {
            var bracket = segment.IndexOf('[', StringComparison.Ordinal);
            var name = bracket < 0 ? segment : segment[..bracket];
            if (name.Length > 1 && name[^1] == '^')
            {
                yield return name[..^1];
                yield return Stream;
            }
            else if (name.Length > 0 || bracket < 0)
            {
                yield return name;
            }

            while (bracket >= 0)
            {
                var close = segment.IndexOf(']', bracket);
                yield return segment[bracket..(close + 1)];
                bracket = segment.IndexOf('[', close);
            }
        }
    }

    /// <summary>Whether a step is an integer index (<c>[1]</c>), and the index.</summary>
    public static bool IsIndex(string step, out int index)
    {
        ArgumentNullException.ThrowIfNull(step);
        index = -1;
        return step.Length > 2 && step[0] == '[' && step[^1] == ']'
            && int.TryParse(step.AsSpan(1, step.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    private static readonly Action<object?, object?> Ignored = static (_, _) => { };

    /// <summary>The member a path reads on a stream (an observable or a task) to get its current value.</summary>
    public const string Stream = "^";

    public Action<object?, object?> Setter(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (ConvertedPaths.TryRead(path, out var convert))
        {
            return fallback?.Back(convert, this) ?? Ignored;
        }

        if (LocalPaths.TryRead(path, out var local))
        {
            return (_, value) =>
            {
                if (Convert(value, local.Type, out var converted))
                {
                    LocalValues.Set(local.Key, converted);
                }
            };
        }

        var (owner, member) = Last(path);
        if (IsIndex(member, out var index))
        {
            return (scope, value) =>
            {
                if (owner(scope) is IList { IsReadOnly: false } list && index < list.Count && Compatible(list[index], value))
                {
                    list[index] = value;
                }
            };
        }

        return (scope, value) =>
        {
            if (owner(scope) is not { } target)
            {
                return;
            }

            var setter = ImmutableInterlocked.GetOrAdd(ref _setters, (target.GetType(), member), FindSetter);
            if (Convert(value, setter.Value, out var converted))
            {
                setter.Set(target, converted);
            }
        };
    }

    /// <summary>The getter of what a path's last step is read on, and that step.</summary>
    private (ValueGetter Owner, string Member) Last(string path)
    {
        var bracket = path.EndsWith(']') ? path.LastIndexOf('[') : -1;
        var dot = path.LastIndexOf('.');
        var split = Math.Max(bracket, dot);
        var owner = split <= 0 ? static scope => scope : Getter(path[..split]);
        return (owner, path[(split == dot ? split + 1 : Math.Max(split, 0))..]);
    }

    /// <summary>Whether a value can take an element's place in a list: of the element's type, or null where the element is.</summary>
    private static bool Compatible(object? element, object? value) =>
        element is null || value is null ? element is null || !element.GetType().IsValueType : element.GetType().IsInstanceOfType(value);

    /// <summary>
    /// Looks up the generated setter a path would write through for this scope, noting it in the coverage as a write
    /// does, without writing.
    /// </summary>
    public void LookUpSetter(object? scope, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (LocalPaths.TryRead(path, out _))
        {
            return;
        }

        var (owner, member) = Last(ConvertedPaths.Inner(path));
        if (generated && !member.StartsWith('^') && !member.StartsWith('[') && owner(scope) is { } target)
        {
            MemberAccessors.Setter(target.GetType(), member);
        }
    }

    /// <summary>The getter of one member of a type.</summary>
    public Func<object, object?> Lookup(Type type, string member) => ImmutableInterlocked.GetOrAdd(ref _getters, (type, member), FindGetter);

    private Func<object, object?> FindGetter((Type Type, string Member) key) =>
        (generated ? MemberAccessors.Getter(key.Type, key.Member) : null)
        ?? fallback?.Getter(key.Type, key.Member)
        ?? throw new InvalidOperationException($"No generated getter for {key.Type.FullName}.{key.Member} and no fallback to read it.");

    private (Type Value, Action<object, object?> Set) FindSetter((Type Type, string Member) key) =>
        (generated ? MemberAccessors.Setter(key.Type, key.Member) : null)
        ?? fallback?.Setter(key.Type, key.Member)
        ?? throw new InvalidOperationException($"No generated setter for {key.Type.FullName}.{key.Member} and no fallback to write it.");

    private static bool Convert(object? value, Type type, out object? converted)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        converted = value;
        if (value is null)
        {
            return !type.IsValueType || underlying is not null;
        }

        var target = underlying ?? type;
        if (target.IsInstanceOfType(value))
        {
            return true;
        }

        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
        {
            converted = System.Convert.ChangeType(value is decimal number ? NumberRanges.Clamp(number, target) : value, target, CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    /// <summary>
    /// One member of a path, with the getter of the first type it was read on kept beside it, as a monomorphic inline
    /// cache: a binding reads the same type on every render, so it skips the shared lookup; another type looks it up.
    /// </summary>
    private sealed class Member(MemberPaths paths, string name)
    {
        private readonly int _index = IsIndex(name, out var index) ? index : -1;

        private Known? _first;

        public object? Read(object target)
        {
            if (_index >= 0 && target is IList list)
            {
                return _index < list.Count ? list[_index] : null;
            }

            var type = target.GetType();
            var first = Volatile.Read(ref _first);
            if (first?.Type == type)
            {
                return first.Get(target);
            }

            var get = paths.Lookup(type, name);
            if (first is null)
            {
                Volatile.Write(ref _first, new Known(type, get));
            }

            return get(target);
        }
    }

    private sealed record Known(Type Type, Func<object, object?> Get);
}
