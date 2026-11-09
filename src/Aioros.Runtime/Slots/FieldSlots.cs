using System.Collections;
using System.Globalization;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Slots;

/// <summary>
/// Binds a posted form's values into the model as the adapter's field binders do, from each field's description, and
/// runs a button's command with its parameter; no field needs Avalonia.
/// </summary>
internal static class FieldSlots
{
    /// <summary>How a field names a list held by a static member (<c>static:Type:Member</c>) instead of a member path of its scope.</summary>
    private const string StaticPrefix = "static:";

    public static FieldBinding? Binding(SlotSpec field, PlanSlotResolver slots)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.Kind == CompositeFields.Kind)
        {
            return Composite(field, slots);
        }

        if (field.Kind == RadioFields.Kind)
        {
            return slots.Paths.Fallback?.Binding(field, slots.Paths);
        }

        if (field.Path is not { } path || field.Kind is "command" or "drop")
        {
            return null;
        }

        var paths = slots.Paths;
        var get = paths.Getter(path);
        var set = paths.Setter(path);
        return field.Kind switch
        {
            "text" or "choice" => (scope, value) => SetIf(set, scope, value is not null, value),
            "check" => Check(field, set),
            "toggle" => (scope, value) => SetIf(set, scope, field.Argument == "on", value is not null),
            "number" => Number(field, set),
            "state" => State(field, set),
            "slider" => Slider(field, set),
            "date" => (scope, value) => SetIf(set, scope, FieldValues.TryDate(value, out var date), date),
            "date-offset" => (scope, value) => SetIf(set, scope, FieldValues.TryDate(value, out var date), Offset(date, get(scope))),
            "time" => (scope, value) => SetIf(set, scope, FieldValues.TryTime(value, out var time), time),
            "combo" => Combo(field, set, slots),
            "choice-index" => Choice(field, set, paths, (_, index) => index),
            "choice-item" => Choice(field, set, paths, (items, index) => items[index]),
            "page" => Page(field, set, slots),
            "page-step" => PageStep(field, get, set, slots),
            _ => paths.Fallback?.Binding(field, paths),
        };
    }

    /// <summary>A travelling state's posted text written as its property's type, as the engine parses it; text that does not parse is left out.</summary>
    private static FieldBinding? State(SlotSpec field, Action<object?, object?> set) => field.Argument switch
    {
        "string" => (scope, value) => SetIf(set, scope, value is not null, value),
        "int" => (scope, value) => SetIf(set, scope, int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number), number),
        "double" => (scope, value) => SetIf(set, scope, double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real), real),
        "bool" => (scope, value) => SetIf(set, scope, bool.TryParse(value, out var flag), flag),
        "bool?" => (scope, value) => SetIf(set, scope, value?.Length == 0 || bool.TryParse(value, out _), value is { Length: > 0 } text && bool.TryParse(text, out var flag) ? flag : null),
        _ => null,
    };

    /// <summary>The paths a field writes when a post binds it: its members' for a composite field, its own for any other.</summary>
    public static IEnumerable<string> WrittenPaths(SlotSpec field) =>
        field.Kind switch
        {
            CompositeFields.Kind => CompositeFields.Members(field).Select(Written).OfType<string>(),
            RadioFields.Kind => RadioFields.Radios(field).Select(radio => radio.Path).OfType<string>().Distinct(StringComparer.Ordinal),
            _ => Written(field) is { } path ? [path] : [],
        };

    /// <summary>The path a field writes when a post binds it: none for a command, a drop target or a disabled box.</summary>
    public static string? Written(SlotSpec field) => field switch
    {
        { Kind: "check" or "toggle", Argument: not "on" } => null,
        { Kind: "text" or "choice" or "check" or "toggle" or "number" or "state" or "slider" or "date" or "date-offset" or "time" or "combo" or "choice-index" or "choice-item" or "page" or "page-step" or "color" or "flip" } => field.Path,
        _ => null,
    };

    public static FieldCommand? Command(SlotSpec field, PlanSlotResolver slots)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.Kind == "drop")
        {
            return Drop(field, slots);
        }

        if (field is { Kind: "flip", Path: { } flipped })
        {
            var current = slots.Paths.Getter(flipped);
            var set = slots.Paths.Setter(flipped);
            return new FieldCommand(static _ => null, (scope, _) =>
            {
                set(scope, current(scope) is not true);
                return ValueTask.CompletedTask;
            });
        }

        if (field.Kind != "command")
        {
            return null;
        }

        var paths = slots.Paths;
        var parameter = field.Argument is { } path ? paths.Getter(path) : Constant(field.Values.IsEmpty ? null : field.Values[0]);
        var command = field.Path is { } commandPath ? paths.Getter(commandPath) : static _ => null;
        return new FieldCommand(parameter, (scope, argument) => slots.RunAsync(command(scope) as System.Windows.Input.ICommand, argument));
    }

    private static FieldCommand Drop(SlotSpec field, PlanSlotResolver slots)
    {
        var paths = slots.Paths;
        var target = field.Argument is { } path ? paths.Getter(path) : Constant(field.Values[2]);
        var command = paths.Getter(field.Path!);
        var items = field.Values[0].Length > 0 ? paths.Getter(field.Values[0]) : null;
        var key = field.Values[1].Length > 0 ? paths.Getter(field.Values[1]) : null;
        return new FieldCommand(target, static (_, _) => ValueTask.CompletedTask)
        {
            DropAsync = (scope, parameter, move) => DropMoves.RunAsync(
                argument => slots.RunAsync(command(scope) as System.Windows.Input.ICommand, argument),
                PlanValues.Format(parameter),
                items is null || key is null ? null : [.. ((items(scope) as IEnumerable)?.Cast<object?>() ?? []).Select(item => PlanValues.Format(key(item)))],
                move),
        };
    }

    /// <summary>
    /// A composite field: its posted value decoded by its renderer's registered decoder, from its members' current values,
    /// into one value per member, each bound as that member field is; none when the renderer has no entry or a member
    /// cannot be bound, so the plan leaves its posts to the engine.
    /// </summary>
    private static FieldBinding? Composite(SlotSpec field, PlanSlotResolver slots)
    {
        if (field.Argument is not { } writer || SlotWriters.Find(writer)?.Decode is not { } decode)
        {
            return null;
        }

        var members = CompositeFields.Members(field);
        var bindings = new FieldBinding[members.Length];
        var getters = new ValueGetter[members.Length];
        for (var index = 0; index < members.Length; index++)
        {
            if (members[index].Path is not { } path || Binding(members[index], slots) is not { } binding)
            {
                return null;
            }

            (bindings[index], getters[index]) = (binding, slots.Paths.Getter(path));
        }

        var key = CompositeFields.Key(field);
        return (scope, value) =>
        {
            if (value is null)
            {
                return;
            }

            var current = new object?[getters.Length];
            for (var index = 0; index < getters.Length; index++)
            {
                current[index] = getters[index](scope);
            }

            var decoded = new string?[getters.Length];
            if (!decode(key, value, current, decoded))
            {
                return;
            }

            for (var index = 0; index < bindings.Length; index++)
            {
                bindings[index](scope, decoded[index]);
            }
        };
    }

    private static ValueGetter Constant(string? value) => _ => value;

    private static void SetIf(Action<object?, object?> set, object? scope, bool apply, object? value)
    {
        if (apply)
        {
            set(scope, value);
        }
    }

    private static FieldBinding Check(SlotSpec field, Action<object?, object?> set) => (scope, value) =>
    {
        if (field.Argument != "on")
        {
            return;
        }

        if (field.Values[0] == "two")
        {
            set(scope, value is not null);
            return;
        }

        SetIf(set, scope, value is "true" or "false" or "null", value switch { "true" => true, "false" => (bool?)false, _ => null });
    };

    private static FieldBinding Number(SlotSpec field, Action<object?, object?> set)
    {
        var minimum = decimal.Parse(field.Values[0], CultureInfo.InvariantCulture);
        var maximum = decimal.Parse(field.Values[1], CultureInfo.InvariantCulture);
        var clip = field.Values[2] == "clip";
        return (scope, value) =>
        {
            if (value is null)
            {
                return;
            }

            if (value.Length == 0)
            {
                set(scope, null);
            }
            else if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                var clamped = Math.Clamp(parsed, minimum, maximum);
                SetIf(set, scope, clip || clamped == parsed, clamped);
            }
        };
    }

    private static FieldBinding Slider(SlotSpec field, Action<object?, object?> set)
    {
        var minimum = double.Parse(field.Values[0], CultureInfo.InvariantCulture);
        var maximum = double.Parse(field.Values[1], CultureInfo.InvariantCulture);
        return (scope, value) =>
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
            {
                set(scope, Math.Clamp(number, minimum, Math.Max(minimum, maximum)));
            }
        };
    }

    private static DateTimeOffset? Offset(DateTime? date, object? current) =>
        date is { } day ? new DateTimeOffset(day, (current as DateTimeOffset?)?.Offset ?? TimeSpan.Zero) : null;

    private static FieldBinding Combo(SlotSpec field, Action<object?, object?> set, PlanSlotResolver slots)
    {
        var items = field.Argument switch
        {
            null => null,
            var reference when reference.StartsWith(StaticPrefix, StringComparison.Ordinal) => slots.Getter(new SlotSpec("static", null, reference)),
            var path => slots.Paths.Getter(path),
        };
        return (scope, value) =>
        {
            var options = items is null ? [.. field.Values] : (items(scope) as IEnumerable)?.Cast<object?>().ToList() ?? [];
            if (FieldValues.Indices(value) is [var index, ..] && index < options.Count)
            {
                set(scope, options[index]);
            }
        };
    }

    private static FieldBinding Choice(SlotSpec field, Action<object?, object?> set, MemberPaths paths, Func<IList<object?>, int, object?> pick)
    {
        var items = field.Argument is { } path ? paths.Getter(path) : null;
        var count = field.Values.IsEmpty ? 0 : int.Parse(field.Values[0], CultureInfo.InvariantCulture);
        return (scope, value) =>
        {
            var list = items is null ? [] : (items(scope) as IEnumerable)?.Cast<object?>().ToList() ?? [];
            var total = items is null ? count : list.Count;
            if (FieldValues.Indices(value).Where(index => index < total).ToList() is [var first, ..])
            {
                set(scope, pick(list, first));
            }
        };
    }

    private static FieldBinding Page(SlotSpec field, Action<object?, object?> set, PlanSlotResolver slots)
    {
        var count = slots.Getter(new SlotSpec("range", field.Argument));
        return (scope, value) =>
        {
            var total = count(scope) is IEnumerable<int> pages ? pages.Count() : 0;
            if (FieldValues.Indices(value) is [var first, ..] && first < total)
            {
                set(scope, first);
            }
        };
    }

    /// <summary>A pager's previous or next button, pressed: the selected page moves by its step, within the pages, as Avalonia's pager moves it.</summary>
    private static FieldBinding PageStep(SlotSpec field, ValueGetter get, Action<object?, object?> set, PlanSlotResolver slots)
    {
        var count = slots.Getter(new SlotSpec("range", field.Argument));
        var step = int.Parse(field.Values[0], CultureInfo.InvariantCulture);
        return (scope, value) =>
        {
            var total = count(scope) is IEnumerable<int> pages ? pages.Count() : 0;
            if (value is not null && get(scope) is int current)
            {
                set(scope, Math.Clamp(current + step, 0, Math.Max(0, total - 1)));
            }
        };
    }
}
