using System.Collections.Frozen;
using System.Globalization;
using System.Windows.Input;
using Aioros.Avalonia.Commands;
using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Html.Variants;
using Aioros.Avalonia.Html.Writers;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Avalonia;
using Avalonia.Media;

namespace Aioros.Avalonia.Plans;

internal readonly record struct Format(ValueFormatter? Formatter, SlotSpec? Spec)
{
    public static Format Text { get; } = new(null, null);

    public string Apply(object? value) => Formatter is null ? PlanValues.Format(value) : Formatter(value);
}

internal readonly record struct Slot(ValueGetter Getter, SlotSpec Spec);

/// <summary>The slots the development engine records: the runtime's resolver, with this adapter as its fallback.</summary>
internal static class Slots
{
    public static PlanSlotResolver Resolver { get; } = new(AdapterFallback.Instance);

    public static Format FormatOf(string kind, string? argument = null, params string[] values)
    {
        var spec = new SlotSpec(kind, null, argument) { Values = [.. values] };
        return new Format(Resolver.Formatter(spec), spec);
    }

    public static ValueFormatter? Formatter(SlotSpec? spec) => Resolver.Formatter(spec);

    public static Slot Path(string path) => Of(SlotSpec.Bound(path));

    public static Slot Self { get; } = Of(new SlotSpec("self"));

    public static Slot Test(string path, string test) => Of(new SlotSpec("test", path, test));

    public static Slot FirstPage(string index) => Of(new SlotSpec("first-page", index));

    public static Slot LastPage(string index, string count) => Of(new SlotSpec("last-page", index, count));

    public static Slot Range(string count) => Of(new SlotSpec("range", count));

    public static string Operand(string? path, int value) => path ?? "#" + value.ToString(CultureInfo.InvariantCulture);

    public static Slot Of(SlotSpec spec) => new(Getter(spec), spec);

    public static bool Check(string test, object? value) => PlanSlotResolver.Check(test, value);

    public static ValueGetter Getter(SlotSpec spec) => Resolver.Getter(spec);

    public static ChoicePredicate Predicate(SlotSpec spec) => Resolver.Predicate(spec);

    public static Slot ErrorsOf(string path) => Of(new SlotSpec("errors", path));

    /// <summary>Whether the model reports errors for the member at the path, which marks its field invalid.</summary>
    public static Slot InvalidOf(string path) => Of(new SlotSpec("invalid", path));
}

/// <summary>
/// What the runtime's resolver does not evaluate, done with Avalonia: members read by reflection, converters and
/// multi-bindings rebuilt from their references, enum and tag variants, the formats of Avalonia's value types and
/// charts, command shapes found by reflection and types found by name.
/// </summary>
internal sealed class AdapterFallback : ISlotFallback
{
    private static readonly FrozenDictionary<string, Func<SlotSpec, ValueFormatter>> Formats = new Dictionary<string, Func<SlotSpec, ValueFormatter>>(StringComparer.Ordinal)
    {
        ["points"] = static _ => Points,
        ["color"] = static _ => ColorOf,
        ["hex-color"] = static _ => ColorWriters.Hex,
        ["thickness"] = static _ => ThicknessOf,
        ["track"] = static _ => TrackOf,
        ["shadow"] = static _ => ShadowOf,
        ["corner-radius"] = static _ => CornerRadiusOf,
        ["matrix"] = static _ => MatrixOf,
        ["transform"] = static _ => TransformOf,
        ["chart"] = static spec => Charts.ChartSvg.Formatter(Charts.ChartDefinition.Parse(spec.Argument!)),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static AdapterFallback Instance { get; } = new();

    public Func<object, object?> Getter(Type owner, string member) => ReflectedMembers.Getter(owner, member);

    public ValueGetter Getter(SlotSpec spec, MemberPaths paths)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Kind switch
        {
            "convert" => BoundConverters.Getter(spec, paths),
            "multi" => MultiBindings.Getter(spec, paths),
            "is" => ValueVariants.Getter(spec, paths),
            "tag" => SelectedTags.Getter(spec, paths),
            "static" => Held(BoundConverters.Rebuild(spec.Argument!)),
            "selector" => TemplateSelectors.Getter(spec),
            var kind => throw new InvalidOperationException($"Unknown slot kind '{kind}'."),
        };
    }

    private static ValueGetter Held(object value) => _ => value;

    public (Type ValueType, Action<object, object?> Set) Setter(Type owner, string member) => ReflectedMembers.Setter(owner, member);

    public ValueFormatter Formatter(SlotSpec spec, MemberPaths paths)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return Formats.TryGetValue(spec.Kind, out var format) ? format(spec) : throw new InvalidOperationException($"Unknown format '{spec.Kind}'.");
    }

    public string Format(SlotSpec spec, object value)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return Formats[spec.Kind](spec)(value);
    }

    public Func<ICommand, object?, ValueTask> Command(Type type) => AwaitedCommands.Shape(type);

    public Type Type(string name) => System.Type.GetType(name, throwOnError: true)!;

    public Action<object?, object?>? Back(SlotSpec convert, MemberPaths paths) => BoundConverters.Back(convert, paths);

    /// <summary>A color input's post (<c>#rrggbb</c>) given to the bound color with the alpha it has, as the picker's own binder gives it.</summary>
    public FieldBinding? Binding(SlotSpec field, MemberPaths paths)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(paths);
        if (field.Kind == RadioFields.Kind)
        {
            return RadioBindings.Of(field, paths);
        }

        if (field.Kind != "color" || field.Path is not { } path)
        {
            return null;
        }

        var get = paths.Getter(path);
        var set = paths.Setter(path);
        return (scope, value) =>
        {
            if (ColorWriters.Posted(value) is { } rgb && get(scope) is Color current)
            {
                set(scope, Color.FromArgb(current.A, rgb.R, rgb.G, rgb.B));
            }
        };
    }

    private static string Points(object? value) => value is IEnumerable<Point> points
        ? string.Join(' ', points.Select(point => CssValues.Num(Math.Round(point.X, 2)) + "," + CssValues.Num(Math.Round(point.Y, 2))))
        : string.Empty;

    /// <summary>A bound margin or padding as CSS: Avalonia converts no other value to a <c>Thickness</c>, so it keeps the default.</summary>
    private static string TrackOf(object? value) => Html.Looks.BoxStyles.Tracks([value is global::Avalonia.Controls.GridLength length ? length : global::Avalonia.Controls.GridLength.Auto]);

    private static string ThicknessOf(object? value) => BoxStyles.Edges(value is Thickness thickness ? thickness : default);

    /// <summary>A bound corner radius as CSS: Avalonia converts no other value to a <c>CornerRadius</c>, so it keeps the default.</summary>
    private static string CornerRadiusOf(object? value) => BoxStyles.Corners(value is CornerRadius radius ? radius : default);

    /// <summary>A bound matrix as CSS: Avalonia converts no other value to a <c>Matrix</c>, so it keeps the identity.</summary>
    private static string MatrixOf(object? value) => CssValues.Matrix(value is Matrix matrix ? matrix : Matrix.Identity);

    /// <summary>A bound render transform as the matrix Avalonia renders, text parsed as its <c>TransformConverter</c> parses it.</summary>
    private static string TransformOf(object? value) => value switch
    {
        ITransform transform => CssValues.Matrix(transform.Value),
        string text when TryParse(() => global::Avalonia.Media.Transformation.TransformOperations.Parse(text)) is ITransform parsed => CssValues.Matrix(parsed.Value),
        _ => "none",
    };

    /// <summary>A bound box shadow as CSS, converted from text as Avalonia converts it.</summary>
    private static string ShadowOf(object? value)
    {
        var shadows = value switch
        {
            BoxShadows exact => exact,
            BoxShadow single => new BoxShadows(single),
            string text when TryParse(() => BoxShadows.Parse(text)) is BoxShadows parsed => parsed,
            _ => default,
        };
        return shadows.Count == 0 ? "none" : BoxStyles.Shadow(shadows);
    }

    private static object? TryParse(Func<object> parse)
    {
        try
        {
            return parse();
        }
        catch (Exception error) when (error is FormatException or ArgumentException)
        {
            return null;
        }
    }

    private static string ColorOf(object? value) => value switch
    {
        IBrush brush => CssValues.Brush(brush) ?? "transparent",
        Color color => CssValues.Color(color),
        string text when Color.TryParse(text, out var parsed) => CssValues.Color(parsed),
        _ => "transparent",
    };
}

/// <summary>
/// Resolves the slots of plans read from the plans file in the development engine: the runtime's resolver with this
/// adapter as its fallback; with <paramref name="generated"/> false, every member is read by reflection only.
/// </summary>
public sealed class AdapterSlotResolver(bool generated = true) : ISlotResolver, IFieldResolver
{
    private readonly PlanSlotResolver _slots = generated ? Slots.Resolver : new PlanSlotResolver(AdapterFallback.Instance, generated: false);

    public FieldBinding? Binding(SlotSpec field) => _slots.Binding(field);

    public FieldCommand? Command(SlotSpec field) => _slots.Command(field);

    public Action? Kept(SlotSpec field, object? scope) => _slots.Kept(field, scope);

    public ValueGetter Getter(SlotSpec spec) => _slots.Getter(spec);

    public ValueFormatter? Formatter(SlotSpec? spec) => _slots.Formatter(spec);

    public ChoicePredicate Predicate(SlotSpec spec) => _slots.Predicate(spec);

    public Type TypeOf(string name) => _slots.TypeOf(name);

    public Func<object?, int> Chooser(SlotSpec spec) => _slots.Chooser(spec);

    public SlotRender Writer(SlotSpec spec) => _slots.Writer(spec);

    /// <summary>The fallback production gives the runtime's resolver: this adapter, loaded when a plan first needs it.</summary>
    public static ISlotFallback Fallback => AdapterFallback.Instance;
}
