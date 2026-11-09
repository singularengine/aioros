using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;

namespace Aioros.Avalonia.Plans;

/// <summary>Member paths for the development engine: the runtime's, with members read by reflection where no generated accessor serves them.</summary>
internal static class PropertyPaths
{
    public static ValueGetter Getter(string path) => Slots.Resolver.Paths.Getter(path);

    public static Action<object?, object?> Setter(string path) => Slots.Resolver.Paths.Setter(path);
}

/// <summary>Getters and setters compiled at run time from a member's metadata, for members no generated accessor covers.</summary>
internal static class ReflectedMembers
{
    private static ImmutableDictionary<(Type Type, string Member), Func<object, object?>> _getters =
        ImmutableDictionary<(Type Type, string Member), Func<object, object?>>.Empty;

    private static ImmutableDictionary<(Type Type, string Member), (Type Property, Action<object, object?> Set)> _setters =
        ImmutableDictionary<(Type Type, string Member), (Type Property, Action<object, object?> Set)>.Empty;

    public static Func<object, object?> Getter(Type type, string member) => ImmutableInterlocked.GetOrAdd(ref _getters, (type, member), Compile);

    /// <summary>Whether the type has this public instance property.</summary>
    public static bool Has(Type type, string member) => ImmutableInterlocked.GetOrAdd(ref _members, (type, member), static key => key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance) is not null);

    private static ImmutableDictionary<(Type, string), bool> _members = ImmutableDictionary<(Type, string), bool>.Empty;

    public static (Type Property, Action<object, object?> Set) Setter(Type type, string member) => ImmutableInterlocked.GetOrAdd(ref _setters, (type, member), CompileSetter);

    /// <summary>
    /// The getter of a member, or one that reads nothing when the type has no such public member, as an Avalonia binding
    /// to it produces no value; the compiled plan is checked against Avalonia's render either way.
    /// </summary>
    private static Func<object, object?> Compile((Type Type, string Member) key)
    {
        if (MethodCommand.TryParse(key.Member, out var method, out var guard))
        {
            return MethodOf(key.Type, method, guard);
        }

        if (key.Member == MemberPaths.Stream)
        {
            return StreamValues.Reader(key.Type);
        }

        if (MemberPaths.IsIndex(key.Member, out var index))
        {
            return Element(key.Type, index);
        }

        if ((key.Type.GetProperty(key.Member) is not { } named || named.GetIndexParameters().Length > 0) && key.Type.GetField(key.Member) is null)
        {
            return static _ => null;
        }

        if (!RuntimeFeature.IsDynamicCodeCompiled)
        {
            return key.Type.GetProperty(key.Member) is { } property ? property.GetValue : key.Type.GetField(key.Member)!.GetValue;
        }

        var parameter = Expression.Parameter(typeof(object));
        var body = Expression.Convert(Expression.PropertyOrField(Expression.Convert(parameter, key.Type), key.Member), typeof(object));
        return Expression.Lambda<Func<object, object?>>(body, parameter).Compile();
    }

    /// <summary>
    /// The element at an index of a read-only list (a type that is an <c>IReadOnlyList&lt;T&gt;</c> and no <c>IList</c>,
    /// which member paths read directly), read through the interface, or nothing out of range or on any other type.
    /// </summary>
    private static Func<object, object?> Element(Type type, int index)
    {
        if (type.GetInterfaces().FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)) is not { } list)
        {
            return static _ => null;
        }

        var count = typeof(IReadOnlyCollection<>).MakeGenericType(list.GenericTypeArguments).GetProperty("Count")!;
        var item = list.GetProperty("Item")!;
        return target => (int)count.GetValue(target)! > index ? item.GetValue(target, [index]) : null;
    }

    /// <summary>A method as a command, read by reflection where no generated accessor serves it, or nothing when the type has no single method of that name.</summary>
    private static Func<object, object?> MethodOf(Type type, string name, string? guard)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(method => method.Name == name && !method.IsGenericMethodDefinition && method.GetParameters().Length <= 1).ToList();
        if (methods is not [var found])
        {
            return static _ => null;
        }

        var parameters = found.GetParameters();
        var can = guard is null && type.GetMethod("Can" + name, [typeof(object)]) is { } check && check.ReturnType == typeof(bool) ? check : null;
        var guarded = guard is null ? null : type.GetProperty(guard);
        return target => new MethodCommand(
            parameter => AwaitedAsync(found.Invoke(target, parameters.Length == 0 ? [] : [MethodCommand.Argument(parameter, parameters[0].ParameterType)])),
            can is not null ? parameter => can.Invoke(target, [parameter]) is true : guarded is not null ? _ => guarded.GetValue(target) is true : null);
    }

    private static ValueTask AwaitedAsync(object? result) => result is Task task ? new ValueTask(task) : ValueTask.CompletedTask;

    private static (Type Property, Action<object, object?> Set) CompileSetter((Type Type, string Member) key)
    {
        var property = key.Type.GetProperty(key.Member) ?? throw new MissingMemberException(key.Type.Name, key.Member);
        if (!RuntimeFeature.IsDynamicCodeCompiled)
        {
            return (property.PropertyType, property.SetValue);
        }

        var target = Expression.Parameter(typeof(object));
        var value = Expression.Parameter(typeof(object));
        var assign = Expression.Assign(Expression.Property(Expression.Convert(target, key.Type), property), Expression.Convert(value, property.PropertyType));
        return (property.PropertyType, Expression.Lambda<Action<object, object?>>(assign, target, value).Compile());
    }
}
