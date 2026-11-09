using System.Globalization;
using System.Text;

namespace Aioros.Runtime.Slots;

/// <summary>
/// A path over the page's own state (decision "Guaranteed scope, closed per control type"): the value of an input no binding
/// writes back, held for one request by <see cref="Rendering.LocalValues"/> as Avalonia's control holds it, with the value
/// the trace saw when the request holds none (<c>@3~bool~VHJ1ZQ</c>: the input's key, its value's type and the value's
/// invariant text, encoded so the path holds no member separator); an input outside the guarantee is marked after its key
/// (<c>@3!~bool</c>), and its posts are answered by the plan only once the page's verification proved it.
/// </summary>
public static class LocalPaths
{
    private const char Mark = '@';

    private const char Separator = '~';

    private const char Unguaranteed = '!';

    private static readonly (string Name, Type Type)[] Types =
    [
        ("bool", typeof(bool)), ("bool?", typeof(bool?)), ("int", typeof(int)), ("int?", typeof(int?)), ("double", typeof(double)), ("double?", typeof(double?)),
        ("decimal", typeof(decimal)), ("decimal?", typeof(decimal?)), ("string", typeof(string)), ("date?", typeof(DateTime?)), ("date-offset?", typeof(DateTimeOffset?)), ("time?", typeof(TimeSpan?)),
    ];

    /// <summary>Whether the page's own state can hold a value of this type: a post's text parses to it without Avalonia.</summary>
    public static bool Holds(Type type) => Array.Exists(Types, known => known.Type == type);

    /// <summary>
    /// The path of the input <paramref name="key"/> holding a value of <paramref name="type"/>, which the trace saw as
    /// <paramref name="initial"/>, marked when the guarantee does not cover it.
    /// </summary>
    public static string Of(int key, Type type, object? initial, bool guaranteed = true)
    {
        var name = Array.Find(Types, known => known.Type == type).Name ?? throw new ArgumentException($"The page's own state does not hold {type}.", nameof(type));
        var path = Mark + key.ToString(CultureInfo.InvariantCulture) + (guaranteed ? string.Empty : Unguaranteed.ToString()) + Separator + name;
        return initial is null ? path : path + Separator + Convert.ToBase64String(Encoding.UTF8.GetBytes(Text(initial))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Whether the path is over the page's own state, with its input's key, its value's type and the value the trace saw.</summary>
    public static bool TryRead(string path, out LocalPath local)
    {
        ArgumentNullException.ThrowIfNull(path);
        local = default;
        if (path.Length < 2 || path[0] != Mark)
        {
            return false;
        }

        var parts = path[1..].Split(Separator);
        var guaranteed = parts[0] is not [.., Unguaranteed];
        if (parts.Length is < 2 or > 3 || !int.TryParse(guaranteed ? parts[0] : parts[0][..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var key) || Array.FindIndex(Types, known => known.Name == parts[1]) is not (>= 0 and var index))
        {
            return false;
        }

        var type = Types[index].Type;
        local = new LocalPath(key, type, parts.Length == 3 ? Parse(Decode(parts[2]), type) : null) { Guaranteed = guaranteed };
        return true;
    }

    private static string Decode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=')));
    }

    private static string Text(object value) => value switch
    {
        bool flag => flag ? "True" : "False",
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
        TimeSpan time => time.ToString("c", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static object? Parse(string text, Type type) => (Nullable.GetUnderlyingType(type) ?? type) switch
    {
        var t when t == typeof(bool) => text == "True",
        var t when t == typeof(int) => int.Parse(text, CultureInfo.InvariantCulture),
        var t when t == typeof(double) => double.Parse(text, CultureInfo.InvariantCulture),
        var t when t == typeof(decimal) => decimal.Parse(text, CultureInfo.InvariantCulture),
        var t when t == typeof(DateTime) => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        var t when t == typeof(DateTimeOffset) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        var t when t == typeof(TimeSpan) => TimeSpan.ParseExact(text, "c", CultureInfo.InvariantCulture),
        _ => text,
    };
}

/// <summary>An input's place in the page's own state: its key, its value's type, the value the trace saw, and whether the guarantee covers it.</summary>
public readonly record struct LocalPath(int Key, Type Type, object? Initial)
{
    /// <summary>Whether the guarantee covers the input, so the plan answers its posts without the page's verification.</summary>
    public bool Guaranteed { get; init; } = true;
}
