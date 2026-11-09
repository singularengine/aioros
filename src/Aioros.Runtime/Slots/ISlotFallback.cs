using System.Windows.Input;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Slots;

/// <summary>
/// What a plan reads when no generated code covers it: a member read or written by reflection, a slot kind or value the
/// runtime does not evaluate (one that needs Avalonia's types), a command's shape and a type by name. The development
/// engine gives its own; production gives one that loads it only when a plan needs it.
/// </summary>
public interface ISlotFallback
{
    Func<object, object?> Getter(Type owner, string member);

    /// <summary>A getter for a slot kind the runtime does not evaluate, reading member paths through <paramref name="paths"/>.</summary>
    ValueGetter Getter(SlotSpec spec, MemberPaths paths);

    (Type ValueType, Action<object, object?> Set) Setter(Type owner, string member);

    /// <summary>A formatter for a format the runtime does not own, reading member paths through <paramref name="paths"/>.</summary>
    ValueFormatter Formatter(SlotSpec spec, MemberPaths paths);

    /// <summary>A value of a format the runtime owns, but of a type it does not know (an Avalonia brush for a color).</summary>
    string Format(SlotSpec spec, object value);

    Func<ICommand, object?, ValueTask> Command(Type type);

    /// <summary>How a post binds a field of a kind the runtime does not bind, one whose value is an Avalonia type (a color), or nothing.</summary>
    FieldBinding? Binding(SlotSpec field, MemberPaths paths) => null;

    /// <summary>What writes a value back through a converter a path carries (<see cref="ConvertedPaths"/>), or nothing when the fallback runs no converters.</summary>
    Action<object?, object?>? Back(SlotSpec convert, MemberPaths paths) => null;

    Type Type(string name);
}
