using System.Collections.Immutable;
using System.Globalization;

namespace Aioros.Runtime.Slots;

/// <summary>A converter a plan names in the plans file (<c>static:Namespace.Type:Member</c>, <c>type:Namespace.Type</c>), with a function that returns it.</summary>
public sealed record ConverterReference(string Reference, Func<object> Create);

/// <summary>
/// The app's converters, generated at build time, and how to run them: a value converter, a multi-value converter, and
/// the converter of a <c>MultiBinding</c>'s <c>StringFormat</c> around an inner converter. Each run returns no value
/// where Avalonia would give the property none (an unset value, nothing to do, a binding error).
/// </summary>
public sealed record ConverterCode(
    ImmutableArray<ConverterReference> Converters,
    Func<object, object?, Type, object?, CultureInfo, object?> Convert,
    Func<object, IList<object?>, Type, object?, CultureInfo, object?> ConvertMany,
    Func<string, object?, object> Format);

public static class PlanConverters
{
    private static ConverterCode? _code;

    private static ImmutableDictionary<string, Func<object>> _references = ImmutableDictionary<string, Func<object>>.Empty.WithComparers(StringComparer.Ordinal);

    private static ImmutableDictionary<string, object?> _created = ImmutableDictionary<string, object?>.Empty.WithComparers(StringComparer.Ordinal);

    public static ConverterCode? Code => Volatile.Read(ref _code);

    public static void Register(ConverterCode? code)
    {
        if (code is null)
        {
            return;
        }

        Volatile.Write(ref _code, code);
        ImmutableInterlocked.Update(ref _references, static (all, added) => all.SetItems(added.Select(reference => KeyValuePair.Create(reference.Reference, reference.Create))), code.Converters);
    }

    /// <summary>
    /// The converter a plans file reference names (assembly names left out, since the generator keys by full name), or
    /// nothing when the app has no generated entry for it; a <c>format:</c> reference builds its string format around the
    /// inner converter.
    /// </summary>
    /// <summary>A converted value as a slot reads it: itself, whether it is not true (<c>not</c>), or whether it is set (<c>set</c>).</summary>
    public static object? Tested(object? value, string test) => test switch
    {
        "not" => value is not true,
        "set" => value is not null,
        _ => value,
    };

    public static object? Find(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return ImmutableInterlocked.GetOrAdd(ref _created, reference, Create);
    }

    /// <summary>
    /// A converter's parameter as a plans file describes it by its text and its kind: none (<c>null</c>), the text
    /// (<c>text</c>), or an enum value by name (<c>enum:</c> and the enum's type name); false when the enum's type is unknown.
    /// </summary>
    public static bool TryParameter(string text, string kind, Func<string, Type?> types, out object? parameter)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(types);
        parameter = kind switch
        {
            "null" => null,
            "text" => text,
            _ when kind.StartsWith(EnumKind, StringComparison.Ordinal) && types(kind[EnumKind.Length..]) is { IsEnum: true } type => Enum.Parse(type, text),
            _ => Unknown,
        };
        return !ReferenceEquals(parameter, Unknown);
    }

    private const string EnumKind = "enum:";

    private static readonly object Unknown = new();

    /// <summary>A plans file reference keyed as the generator keys it: <c>static:</c> and <c>type:</c> with the type's full name.</summary>
    public static string Key(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.StartsWith("type:", StringComparison.Ordinal))
        {
            return "type:" + PlanTypes.FullName(reference[5..]);
        }

        if (reference.StartsWith("static:", StringComparison.Ordinal))
        {
            var last = reference.LastIndexOf(':');
            return "static:" + PlanTypes.FullName(reference[7..last]) + reference[last..];
        }

        return reference;
    }

    private static object? Create(string reference)
    {
        if (!reference.StartsWith("format:", StringComparison.Ordinal))
        {
            return Volatile.Read(ref _references).GetValueOrDefault(Key(reference))?.Invoke();
        }

        var line = reference.IndexOf('\n', StringComparison.Ordinal);
        var inner = reference[7..line];
        if (Code is not { } code)
        {
            return null;
        }

        if (inner.Length == 0)
        {
            return code.Format(reference[(line + 1)..], null);
        }

        return Find(inner) is { } converter ? code.Format(reference[(line + 1)..], converter) : null;
    }
}
