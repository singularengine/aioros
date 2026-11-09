using System.Collections;
using System.Collections.Frozen;
using System.Globalization;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Slots;

/// <summary>
/// The formats of value slots that need no Avalonia type, as the adapter formats them; any other format, and a value of
/// a type these do not know (a brush for a color), goes to the fallback.
/// </summary>
internal static class SlotFormats
{
    private const double DegreesToRadians = 0.0174532925;

    private static readonly FrozenDictionary<string, Func<SlotSpec, MemberPaths, ValueFormatter>> Owned = new Dictionary<string, Func<SlotSpec, MemberPaths, ValueFormatter>>(StringComparer.Ordinal)
    {
        ["css-number"] = static (_, _) => CssNumber,
        ["points"] = static (spec, paths) => value => Points(spec, paths, value),
        ["color"] = static (spec, paths) => value => Color(spec, paths, value),
        ["tangent"] = static (_, _) => Tangent,
        ["icon"] = static (_, _) => static value => IconSet.Markup(value as string),
        ["href"] = static (_, _) => static value => LinkSchemes.Safe(Convert.ToString(value, CultureInfo.InvariantCulture)),
        ["invariant"] = static (_, _) => Invariant,
        ["string-format"] = static (spec, _) => value => string.Format(CultureInfo.CurrentCulture, spec.Argument!, value),
        ["identity"] = static (_, _) => PlanValues.Format,
        ["access"] = static (_, _) => static value => AccessText(value as string),
        ["content-text"] = static (spec, _) => ContentText(spec),
        ["offset"] = static (spec, _) => Offset(spec),
        ["progress-text"] = static (spec, _) => ProgressText(Number(spec.Values[0]), Number(spec.Values[1]), spec.Values[2]),
        ["error-text"] = static (_, _) => static value => value is Exception error ? error.Message : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        ["except"] = static (spec, _) => value => value is string text && text != spec.Argument ? text : string.Empty,
        ["date"] = static (_, _) => Date,
        ["time"] = static (spec, _) => value => value is TimeSpan time ? time.ToString(FieldValues.TimeFormats[spec.Argument == "seconds" ? 1 : 0], CultureInfo.InvariantCulture) : string.Empty,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, ValueSpanFormatter> Spanned = new Dictionary<string, ValueSpanFormatter>(StringComparer.Ordinal)
    {
        ["color"] = TryColor,
        ["css-number"] = TryCssNumber,
        ["identity"] = PlanValues.TryFormat,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool Owns(string kind) => Owned.ContainsKey(kind);

    /// <summary>The formats that write a value without a string when they can, the rest through their formatter.</summary>
    public static ValueSpanFormatter? SpanOf(SlotSpec? spec) => spec is not null && Spanned.TryGetValue(spec.Kind, out var format) ? format : null;

    public static ValueFormatter? Of(SlotSpec? spec, MemberPaths paths)
    {
        if (spec is null)
        {
            return null;
        }

        return Owned.TryGetValue(spec.Kind, out var format) ? format(spec, paths) : Fallback(spec, paths).Formatter(spec, paths);
    }

    public static string Date(object? value) => value switch
    {
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => string.Empty,
    };

    public static double Number(object? value) => value is IConvertible convertible ? convertible.ToDouble(CultureInfo.InvariantCulture) : 0;

    /// <summary>The text of a label without its access key marker (<c>_Save</c> is <c>Save</c>, <c>__</c> an underscore), as Avalonia shows it.</summary>
    public static string AccessText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var marker = text.IndexOf('_', StringComparison.Ordinal);
        while (marker >= 0 && marker + 1 < text.Length && text[marker + 1] == '_')
        {
            marker = text.IndexOf('_', marker + 2);
        }

        if (marker >= 0 && marker < text.Length - 1)
        {
            text = text.Remove(marker, 1);
        }

        return text.Replace("__", "_", StringComparison.Ordinal);
    }

    private static ISlotFallback Fallback(SlotSpec spec, MemberPaths paths) =>
        paths.Fallback ?? throw new InvalidOperationException($"The '{spec.Kind}' slot needs a fallback that evaluates it, and none was given.");

    private static string CssNumber(object? value) => value is IConvertible convertible ? CssText.Num(convertible.ToDouble(CultureInfo.InvariantCulture)) : "0";

    private static string Invariant(object? value) => value switch
    {
        decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => string.Empty,
    };

    private static string Tangent(object? value) =>
        CssText.Num(Math.Tan((value is IConvertible and not string ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : 0) * DegreesToRadians));

    /// <summary>A bound color: hex text here, a brush, a color or a color's name through the fallback, anything else transparent.</summary>
    private static string Color(SlotSpec spec, MemberPaths paths, object? value) => value switch
    {
        null => "transparent",
        string text when CssText.TryHex(text, out var argb) => CssText.Color(argb),
        string text when text.StartsWith('#') || text.Length == 0 => "transparent",
        _ => Fallback(spec, paths).Format(spec, value),
    };

    private static bool TryColor(object? value, Span<char> destination, out int written)
    {
        written = 0;
        return value is string text && CssText.TryColor(text, destination, out written);
    }

    private static bool TryCssNumber(object? value, Span<char> destination, out int written)
    {
        written = 0;
        return value is IConvertible convertible && CssText.TryNum(convertible.ToDouble(CultureInfo.InvariantCulture), destination, out written);
    }

    /// <summary>A bound list of Avalonia points as SVG points, each coordinate read through its <c>X</c> and <c>Y</c> accessors.</summary>
    private static string Points(SlotSpec spec, MemberPaths paths, object? value)
    {
        if (value is not IEnumerable points || value is string)
        {
            return string.Empty;
        }

        var written = new List<string>();
        foreach (var point in points)
        {
            if (point?.GetType() is not { FullName: "Avalonia.Point" } type)
            {
                return Fallback(spec, paths).Format(spec, value);
            }

            written.Add(CssText.Num(Math.Round(Number(paths.Lookup(type, "X")(point)), 2)) + "," + CssText.Num(Math.Round(Number(paths.Lookup(type, "Y")(point)), 2)));
        }

        return string.Join(' ', written);
    }

    private static double Number(string? text) => double.Parse(text ?? "0", CultureInfo.InvariantCulture);

    /// <summary>
    /// A bound content as the text its presenter shows: the binding's <c>TargetNullValue</c> for a null value, the value
    /// through the binding's <c>StringFormat</c> or as text otherwise, and without its access key marker in an <c>AccessText</c>.
    /// </summary>
    private static ValueFormatter ContentText(SlotSpec spec)
    {
        var fallback = spec.Argument;
        var format = spec.Values[0];
        var access = spec.Values[1] == "access";
        return value =>
        {
            var text = value is null && fallback is not null ? fallback
                : format.Length > 0 ? string.Format(CultureInfo.CurrentCulture, format, value)
                : PlanValues.Format(value);
            return access ? AccessText(text) : text;
        };
    }

    /// <summary>A range's value from its minimum, clamped to its range first as Avalonia's <c>RangeBase</c> coerces it.</summary>
    private static ValueFormatter Offset(SlotSpec spec)
    {
        var minimum = Number(spec.Argument);
        var maximum = spec.Values.IsEmpty ? double.PositiveInfinity : Number(spec.Values[0]);
        return value => CssText.Num(Clamp(Number(value), minimum, maximum) - minimum);
    }

    private static double Clamp(double value, double minimum, double maximum) => Math.Max(minimum, Math.Min(Math.Max(minimum, maximum), value));

    private static ValueFormatter ProgressText(double minimum, double maximum, string format) => value =>
    {
        var number = Clamp(Number(value), minimum, maximum);
        var percentage = maximum > minimum ? (number - minimum) / (maximum - minimum) * 100 : 0;
        return string.Format(CultureInfo.CurrentCulture, format, number, percentage, minimum, maximum);
    };
}
