using System.Globalization;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// A radio group's post bound through the plan as Avalonia binds it (<see cref="RadioFields"/>): the radio at the posted
/// position becomes checked and the one checked before unchecked, each writing back only when its state changes, the
/// unchecked one first, as checking a radio unchecks the rest of its group.
/// </summary>
internal static class RadioBindings
{
    public static FieldBinding Of(SlotSpec field, MemberPaths paths)
    {
        var radios = RadioFields.Radios(field).Select(radio => Radio(radio, paths)).ToList();
        return (scope, value) =>
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var chosen) || chosen >= radios.Count)
            {
                return;
            }

            for (var index = 0; index < radios.Count; index++)
            {
                if (index != chosen && radios[index] is { } other && other.Checked(scope))
                {
                    other.Write(scope, false);
                }
            }

            if (radios[chosen] is { } picked && !picked.Checked(scope))
            {
                picked.Write(scope, true);
            }
        };
    }

    private static (Func<object?, bool> Checked, Action<object?, bool> Write)? Radio(SlotSpec radio, MemberPaths paths) => radio.Kind switch
    {
        "convert" => Converted(radio, paths),
        "check" => Own(paths.Getter(radio.Path!), paths.Setter(radio.Path!)),
        _ => null,
    };

    private static (Func<object?, bool>, Action<object?, bool>) Converted(SlotSpec radio, MemberPaths paths)
    {
        var get = BoundConverters.Getter(radio, paths);
        var back = BoundConverters.Back(radio, paths);
        return (scope => get(scope) is true, (scope, on) => back(scope, on));
    }

    private static (Func<object?, bool>, Action<object?, bool>) Own(ValueGetter get, Action<object?, object?> set) =>
        (scope => get(scope) is true, (scope, on) => set(scope, on));
}
