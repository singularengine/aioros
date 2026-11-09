using System.Globalization;

namespace Aioros.Runtime.Pages;

/// <summary>
/// Converts a route or query value to the type of a loader's parameter, for the binding code the generator emits
/// (ASP.NET Core's Request Delegate Generator emits the same <c>TryParse</c> calls for Minimal APIs).
/// </summary>
public static class LoaderArguments
{
    /// <summary>Converts a non-empty value with the type's <see cref="IParsable{TSelf}"/> and the invariant culture.</summary>
    public static bool TryParse<T>(string? text, out T value)
        where T : IParsable<T>
    {
        if (string.IsNullOrEmpty(text))
        {
            value = default!;
            return false;
        }

        return T.TryParse(text, CultureInfo.InvariantCulture, out value!);
    }

    /// <summary>Converts a value to one of the enum's names, ignoring case; numbers and combinations are refused.</summary>
    public static bool TryEnum<T>(string? text, out T value)
        where T : struct, Enum
    {
        value = default;
        return text is { Length: > 0 } && (char.IsLetter(text[0]) || text[0] == '_') && !text.Contains(',', StringComparison.Ordinal)
            && Enum.TryParse(text, ignoreCase: true, out value) && Enum.IsDefined(value);
    }
}
