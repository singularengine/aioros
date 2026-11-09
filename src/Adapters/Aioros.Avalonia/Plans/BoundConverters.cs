using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// Converters in plans: a slot runs the binding's converter the way Avalonia does (the value, the target type, the
/// parameter and the culture). A converter is described for the plans file when it can be rebuilt: a static member,
/// or a type with a parameterless constructor and no settings.
/// </summary>
internal static class BoundConverters
{
    private static readonly Type[] Builtins = [typeof(BoolConverters), typeof(ObjectConverters), typeof(StringConverters)];
    private static ImmutableDictionary<string, IValueConverter> _resolved = ImmutableDictionary<string, IValueConverter>.Empty;
    private static Type[]? _holders;

    /// <summary>The slot of a bound property whose binding has a converter, or nothing when it has none.</summary>
    public static (ValueGetter Getter, SlotSpec? Spec)? Of(PlanRecorder plan, AvaloniaObject target, AvaloniaProperty property, bool negate = false)
    {
        var local = LocalBindings.Of(target, property);
        var binding = local ?? Templated(plan, target, property);
        if (binding is null || BindingFormats.Converter(binding) is not { } converter
            || (local is null ? TemplatedPath(plan, target, binding) : plan.RawBoundPath(target, property)) is not { } path)
        {
            return null;
        }

        var getter = Getter(PropertyPaths.Getter(path), converter, BindingFormats.Parameter(binding), property.PropertyType, negate ? "not" : string.Empty);
        return (getter, Spec(binding, path, property.PropertyType, negate ? "not" : string.Empty));
    }

    /// <summary>
    /// Whether a property's value is one the writers write as it is (a flag, a text, a number, an enum, edges, corners),
    /// so a converter on it is run by the plan's path for any value; a value whose writing depends on measuring it (a
    /// geometry's bounds, an image) is learned per value instead.
    /// </summary>
    public static bool Plain(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var plain
        && (plain.IsPrimitive || plain.IsEnum || plain == typeof(string) || plain == typeof(decimal) || plain == typeof(Thickness) || plain == typeof(CornerRadius));

    /// <summary>The <c>convert</c> slot of a binding with a converter over this path, or nothing when its converter or its parameter cannot be rebuilt from a plans file.</summary>
    public static SlotSpec? Spec(BindingExpressionBase binding, string path, Type target, string test = "") =>
        BindingFormats.Converter(binding) is { } converter && Reference(converter) is { } reference && BindingFormats.Parameter(binding) is var parameter && parameter is null or string or Enum
            ? new SlotSpec("convert", path, reference) { Values = [parameter?.ToString() ?? string.Empty, ParameterKind(parameter), target.AssemblyQualifiedName!, test] }
            : null;

    /// <summary>
    /// A template part's <c>TemplateBinding</c> with a converter of a property its templated control binds to the model in
    /// the current scope (a part visible while <c>Title</c> is not null), which reads the control's binding through the
    /// converter; nothing for any other binding, or while the control's value is learned as a constant.
    /// </summary>
    private static BindingExpressionBase? Templated(PlanRecorder plan, AvaloniaObject target, AvaloniaProperty property) =>
        target is global::Avalonia.Controls.Control { TemplatedParent: StyledElement parent } && BindingOperations.GetBindingExpressionBase(target, property) is { } expression
        && TemplateConverters.Is(expression) && TemplateBindings.Property(expression, parent) is { } shown && LocalBindings.Own(parent, shown) is not null
        && !ConstantBindings.IsLearnedConstant(parent, shown) && plan.InScope(parent)
            ? expression
            : null;

    private static string? TemplatedPath(PlanRecorder plan, AvaloniaObject target, BindingExpressionBase binding) =>
        target is global::Avalonia.Controls.Control { TemplatedParent: StyledElement parent } && TemplateBindings.Property(binding, parent) is { } shown ? plan.BoundPath(parent, shown) : null;

    /// <summary>How the plans file describes a parameter: none, text, or an enum value with its type (<see cref="Runtime.Slots.PlanConverters.TryParameter"/>).</summary>
    private static string ParameterKind(object? parameter) => parameter switch
    {
        null => "null",
        Enum value => "enum:" + value.GetType().AssemblyQualifiedName,
        _ => "text",
    };

    public static ValueGetter Getter(SlotSpec spec, Runtime.Slots.MemberPaths paths)
    {
        var converter = ImmutableInterlocked.GetOrAdd(ref _resolved, spec.Argument!, Resolve);
        var target = Type.GetType(spec.Values[2], throwOnError: true)!;
        Runtime.Slots.PlanConverters.TryParameter(spec.Values[0], spec.Values[1], name => Type.GetType(name, throwOnError: true), out var parameter);
        return Getter(paths.Getter(spec.Path!), converter, parameter, target, spec.Values[3]);
    }

    /// <summary>
    /// What a post writes when the control's value becomes <c>true</c> or <c>false</c> through a converted binding: the
    /// converter's <c>ConvertBack</c> of it, given the source's type and the binding's parameter as Avalonia gives them,
    /// written to the bound path unless the converter answers that it does nothing.
    /// </summary>
    public static Action<object?, object?> Back(SlotSpec spec, Runtime.Slots.MemberPaths paths)
    {
        var converter = ImmutableInterlocked.GetOrAdd(ref _resolved, spec.Argument!, Resolve);
        Runtime.Slots.PlanConverters.TryParameter(spec.Values[0], spec.Values[1], name => Type.GetType(name, throwOnError: true), out var parameter);
        var get = paths.Getter(spec.Path!);
        var set = paths.Setter(spec.Path!);
        return (scope, posted) =>
        {
            var value = converter.ConvertBack(posted, get(scope)?.GetType() ?? typeof(object), parameter, CultureInfo.CurrentCulture);
            if (value != BindingOperations.DoNothing && value != AvaloniaProperty.UnsetValue && value is not BindingNotification)
            {
                set(scope, value);
            }
        };
    }

    /// <summary>A converted value, or, by its test, whether it is not true (<c>not</c>, a negated binding) or set (<c>set</c>, a tip shown only with content).</summary>
    private static ValueGetter Getter(ValueGetter source, IValueConverter converter, object? parameter, Type target, string test) => scope =>
    {
        var value = converter.Convert(source(scope), target, parameter, CultureInfo.CurrentCulture);
        value = value is BindingNotification || value == AvaloniaProperty.UnsetValue ? null : value;
        return Runtime.Slots.PlanConverters.Tested(value, test);
    };

    internal static string? Reference(object converter)
    {
        if (StaticMember(converter, Builtins.Append(converter.GetType())) is { } member)
        {
            return member;
        }

        var type = converter.GetType();
        return type.GetConstructor(Type.EmptyTypes) is not null && !type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(item => item.CanWrite)
            ? "type:" + type.AssemblyQualifiedName
            : StaticMember(converter, Holders());
    }

    /// <summary>
    /// The public classes of the app's assemblies (not .NET's or Avalonia's) with a public static member typed as a
    /// converter, which can hold one that is not of their own type (a static class of <c>FuncValueConverter</c>s).
    /// </summary>
    private static Type[] Holders()
    {
        if (Volatile.Read(ref _holders) is { } holders)
        {
            return holders;
        }

        holders = [.. AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && assembly.GetName().Name is { } name && !Framework(name))
            .SelectMany(Exported)
            .Where(type => type is { IsClass: true, ContainsGenericParameters: false } && HoldsConverter(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)];
        Volatile.Write(ref _holders, holders);
        return holders;
    }

    private static Type[] Exported(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (Exception error) when (error is ReflectionTypeLoadException or FileNotFoundException or FileLoadException or NotSupportedException)
        {
            return [];
        }
    }

    private static bool Framework(string name) =>
        name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft", StringComparison.Ordinal) || name.StartsWith("Avalonia", StringComparison.Ordinal)
        || name is "netstandard" or "mscorlib";

    private static bool HoldsConverter(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static).Any(field => Converts(field.FieldType))
        || type.GetProperties(BindingFlags.Public | BindingFlags.Static).Any(property => property.GetIndexParameters().Length == 0 && Converts(property.PropertyType));

    private static bool Converts(Type type) => typeof(IValueConverter).IsAssignableFrom(type) || typeof(IMultiValueConverter).IsAssignableFrom(type);

    /// <summary>
    /// The reference of the public static member of one of the owners that holds this very object
    /// (<c>static:Type:Member</c>), as <c>x:Static</c> names it, or nothing when none holds it.
    /// </summary>
    internal static string? StaticMember(object value, IEnumerable<Type> owners)
    {
        foreach (var owner in owners)
        {
            var member = owner.GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => ReferenceEquals(field.GetValue(null), value)).Select(field => field.Name)
                .Concat(owner.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(item => item.GetIndexParameters().Length == 0 && ReferenceEquals(item.GetValue(null), value)).Select(item => item.Name))
                .FirstOrDefault();
            if (member is not null)
            {
                return "static:" + owner.AssemblyQualifiedName + ":" + member;
            }
        }

        return null;
    }

    private static IValueConverter Resolve(string reference) => (IValueConverter)Rebuild(reference);

    internal static object Rebuild(string reference)
    {
        if (reference.StartsWith("type:", StringComparison.Ordinal))
        {
            return Activator.CreateInstance(Type.GetType(reference[5..], throwOnError: true)!)!;
        }

        var last = reference.LastIndexOf(':');
        var owner = Type.GetType(reference[7..last], throwOnError: true)!;
        var name = reference[(last + 1)..];
        return owner.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? owner.GetProperty(name, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
    }
}
