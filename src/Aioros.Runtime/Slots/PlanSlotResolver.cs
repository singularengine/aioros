using System.Collections.Frozen;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Slots;

/// <summary>
/// Resolves the slots and fields of plans read from the plans file without Avalonia: member paths through the app's
/// generated accessors, the slot kinds and formats that need no Avalonia type, converters, types and command shapes
/// through the app's generated code, and anything else through the fallback (the development engine, which production
/// loads only when a plan needs it). With <paramref name="generated"/> false, every member is read through the fallback.
/// </summary>
public sealed class PlanSlotResolver(ISlotFallback? fallback = null, bool generated = true) : ISlotResolver, IFieldResolver
{
    private static readonly FrozenDictionary<string, Func<object?, bool>> Tests = new Dictionary<string, Func<object?, bool>>(StringComparer.Ordinal)
    {
        ["true"] = static value => value is true,
        ["false"] = static value => value is false,
        ["null"] = static value => value is null,
        ["set"] = static value => value is not null,
        ["current"] = static value => value is "page" or "step" or "location" or "date" or "time" or "true",
        ["first"] = static value => value is int index && index <= 0,
        ["items"] = static value => value is System.Collections.IEnumerable items && items.GetEnumerator().MoveNext(),
        ["no-items"] = static value => value is not System.Collections.IEnumerable items || !items.GetEnumerator().MoveNext(),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly object True = true;

    private static readonly object False = false;

    public MemberPaths Paths { get; } = new(fallback, generated);

    public static bool Check(string test, object? value) => Tests[test](value);

    public ValueGetter Getter(SlotSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        switch (spec.Kind)
        {
            case "self":
                return static scope => scope;
            case "convert":
                return Converted(spec);
            case "multi":
                return ConvertedMany(spec);
            case "enabled":
                return Enabled(spec);
            case "flag":
                return Flag(spec);
            case "one":
                var one = Paths.Getter(spec.Path!);
                return scope => new[] { one(scope) };
            case "test":
                var tested = Paths.Getter(spec.Path!);
                var test = Tests[spec.Argument!];
                return scope => Boxed(test(tested(scope)));
            case "first-page":
                var first = Integer(spec.Path!);
                return scope => Boxed(first(scope) <= 0);
            case "last-page":
                var index = Integer(spec.Path!);
                var count = Integer(spec.Argument!);
                return scope => Boxed(index(scope) >= count(scope) - 1);
            case "errors":
                return Errors(spec.Path!);
            case "invalid":
                var errors = Errors(spec.Path!);
                return scope => Boxed(errors(scope) is System.Collections.IEnumerable items && items.Cast<object?>().Any(error => error is not null));
            case "range":
                var pages = Integer(spec.Path!);
                return scope => Enumerable.Range(0, Math.Max(0, pages(scope)));
            case "path":
                return Paths.Getter(spec.Path!);
            case "value":
                return Valued(spec);
            case "calendar":
                return CalendarMonths.Getter(spec, Paths);
            case "static" when spec.Argument is { } reference && PlanConverters.Find(reference) is { } held:
                return _ => held;
            default:
                return Fallback(spec).Getter(spec, Paths);
        }
    }

    public ValueFormatter? Formatter(SlotSpec? spec) => SlotFormats.Of(spec, Paths);

    public ValueSpanFormatter? SpanFormatter(SlotSpec? spec) => SlotFormats.SpanOf(spec);

    public ValueUtf8Formatter? Utf8Formatter(SlotSpec? spec) => spec is { Kind: "icon" } ? static value => IconSet.Utf8(value as string) : null;

    /// <summary>The slot writer a writer node names, reading members of its data through <see cref="Paths"/>.</summary>
    public SlotRender Writer(SlotSpec spec) => SlotWriters.Create(spec, Paths.Lookup);

    public ChoicePredicate Predicate(SlotSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        switch (spec.Kind)
        {
            case "index-path":
                var selectedIndex = Paths.Getter(spec.Path!);
                return (scope, position, _) => selectedIndex(scope) is int value && value == position;
            case "page":
                var page = Integer(spec.Path!);
                return (scope, position, _) => page(scope) == position;
            case "item-path":
                var selectedItem = Paths.Getter(spec.Path!);
                return (scope, _, item) => Equals(selectedItem(scope), item);
            case "items-path":
                var selectedItems = Paths.Getter(spec.Path!);
                return (scope, _, item) => selectedItems(scope) is System.Collections.IEnumerable chosen && chosen.Cast<object?>().Contains(item);
            case "item-text":
                var selectedText = Paths.Getter(spec.Path!);
                var texts = spec.Values;
                return (scope, position, _) => position < texts.Length && selectedText(scope) is string text && text == texts[position];
            default:
                var indexes = spec.Values.Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToFrozenSet();
                return (_, position, _) => indexes.Contains(position);
        }
    }

    public FieldBinding? Binding(SlotSpec field) => FieldSlots.Binding(field, this);

    /// <summary>
    /// Looks up, without writing, the setter each field of the plan rendered for these models would write through, so
    /// the coverage of generated accessors counts the writes a post would make before any post is made.
    /// </summary>
    public void LookUpSetters(RenderPlan plan, ImmutableArray<object?> models)
    {
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var field in PlanForms.Fields(plan, models).Fields)
        {
            foreach (var path in field.Field is { } spec ? FieldSlots.WrittenPaths(spec) : [])
            {
                var outer = OuterScopes.Swap(field.Outer);
                try
                {
                    Paths.LookUpSetter(field.Scope, path);
                }
                finally
                {
                    OuterScopes.Swap(outer);
                }
            }
        }
    }

    public FieldCommand? Command(SlotSpec field) => FieldSlots.Command(field, this);

    /// <summary>Reads the value at each path the field writes, and puts them back through the same setters a post binds through.</summary>
    public Action? Kept(SlotSpec field, object? scope)
    {
        ArgumentNullException.ThrowIfNull(field);
        var kept = FieldSlots.WrittenPaths(field).Select(path => (Set: Paths.Setter(path), Value: Paths.Getter(path)(scope))).ToList();
        return () => kept.ForEach(value => value.Set(scope, value.Value));
    }

    /// <summary>A template selector's chooser: the app's generated code, or the fallback's, which rebuilds the selector by reflection.</summary>
    public Func<object?, int> Chooser(SlotSpec spec)
    {
        if (PlanSelectors.Chooser(spec) is { } built)
        {
            return built;
        }

        var choose = Fallback(spec).Getter(spec, Paths);
        return item => choose(item) is int index ? index : -1;
    }

    /// <summary>
    /// Whether the scope (or the value at the slot's path) is the slot's value: an enum value or a <c>bool</c> by its type's
    /// full name and its text, or nothing for a slot without a type, compared without Avalonia or reflection.
    /// </summary>
    private ValueGetter Valued(SlotSpec spec)
    {
        var get = spec.Path is null ? static scope => scope : Paths.Getter(spec.Path);
        var type = spec.Argument;
        var text = spec.Values.IsEmpty ? string.Empty : spec.Values[0];
        return type is null
            ? scope => Boxed(get(scope) is null)
            : scope => Boxed(get(scope) is { } value && value.GetType().FullName == type && value.ToString() == text);
    }

    public Type TypeOf(string name) => PlanTypes.Find(name) ?? Fallback(new SlotSpec("type", null, name)).Type(name);

    /// <summary>Runs a command with the app's generated shapes, or the shape the fallback finds.</summary>
    public ValueTask RunAsync(ICommand? command, object? parameter) => PlanCommands.RunAsync(command, parameter, fallback is null ? null : fallback.Command);

    internal Func<object?, int> Integer(string operand)
    {
        if (operand.StartsWith('#'))
        {
            var constant = int.Parse(operand.AsSpan(1), CultureInfo.InvariantCulture);
            return _ => constant;
        }

        var get = Paths.Getter(operand);
        return scope => get(scope) is int value ? value : 0;
    }

    private ISlotFallback Fallback(SlotSpec spec) =>
        fallback ?? throw new InvalidOperationException($"The '{spec.Kind}' slot{(spec.Path is null ? string.Empty : " of '" + spec.Path + "'")} needs a fallback that evaluates it, and none was given.");

    private ValueGetter Errors(string path)
    {
        var last = path.LastIndexOf('.');
        var owner = last < 0 ? static scope => scope : Paths.Getter(path[..last]);
        var member = path[(last + 1)..];
        return scope => owner(scope) is INotifyDataErrorInfo model ? model.GetErrors(member) : null;
    }

    /// <summary>Whether the scope enables the control: its <c>IsEnabled</c> path is true and its command can execute.</summary>
    private ValueGetter Enabled(SlotSpec spec)
    {
        var enabled = spec.Path is { } path ? Paths.Getter(path) : null;
        var command = spec.Argument is { } commandPath ? Paths.Getter(commandPath) : null;
        var parameter = spec.Values[0] switch
        {
            "path" => Paths.Getter(spec.Values[1]),
            "text" => _ => spec.Values[1],
            _ => static _ => null,
        };
        var negate = spec.Values.Length > 2;
        return scope =>
        {
            var on = (enabled is null || enabled(scope) is true)
                && (command is null || command(scope) is not ICommand executable || executable.CanExecute(parameter(scope)));
            return Boxed(on != negate);
        };
    }

    /// <summary>
    /// A bound value read as a flag the way an Avalonia binding gives it to a <c>bool</c> property: a <c>!</c> negates the
    /// value after converting it as Avalonia's logical not does, a plain path converts it as Avalonia's default converter
    /// does, and a value neither can convert leaves the property's default.
    /// </summary>
    private ValueGetter Flag(SlotSpec spec)
    {
        var get = Paths.Getter(spec.Path!);
        var negate = spec.Values[0] == "not";
        var fallbackValue = spec.Values[1] == "true";
        var complement = spec.Values[2] == "complement";
        return scope =>
        {
            var value = get(scope);
            var flag = negate ? (Logical(value) is { } logical ? !logical : fallbackValue) : Plain(value) ?? fallbackValue;
            return Boxed(flag != complement);
        };
    }

    private static object Boxed(bool value) => value ? True : False;

    private static bool? Logical(object? value) => value switch
    {
        bool flag => flag,
        string text => bool.TryParse(text, out var parsed) ? parsed : null,
        _ => Converted(value),
    };

    private static bool? Plain(object? value) => value switch
    {
        bool flag => flag,
        null => null,
        string text => bool.TryParse(text, out var parsed) ? parsed : null,
        IConvertible => Converted(value),
        _ => null,
    };

    private static bool? Converted(object? value)
    {
        try
        {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException)
        {
            return null;
        }
    }

    /// <summary>A bound value through its converter, as Avalonia runs it: the value, the target type, the parameter and the culture.</summary>
    private ValueGetter Converted(SlotSpec spec)
    {
        if (PlanConverters.Code is not { } code || PlanConverters.Find(spec.Argument!) is not { } converter || PlanTypes.Find(spec.Values[2]) is not { } target
            || !PlanConverters.TryParameter(spec.Values[0], spec.Values[1], PlanTypes.Find, out var parameter))
        {
            return Fallback(spec).Getter(spec, Paths);
        }

        var source = Paths.Getter(spec.Path!);
        var test = spec.Values[3];
        return scope => PlanConverters.Tested(code.Convert(converter, source(scope), target, parameter, CultureInfo.CurrentCulture), test);
    }

    private ValueGetter ConvertedMany(SlotSpec spec)
    {
        if (PlanConverters.Code is not { } code || PlanConverters.Find(spec.Argument!) is not { } converter || PlanTypes.Find(spec.Values[2]) is not { } target)
        {
            return Fallback(spec).Getter(spec, Paths);
        }

        var sources = spec.Path!.Split('|').Select(Paths.Getter).ToArray();
        var parameter = spec.Values[1] == "null" ? null : spec.Values[0];
        return scope => code.ConvertMany(converter, [.. sources.Select(source => source(scope))], target, parameter, CultureInfo.CurrentCulture);
    }
}
