using System.Globalization;

namespace Aioros.Runtime.Slots;

/// <summary>How posted text is read for a field: dates, times and the indexes a list or pager posts.</summary>
public static class FieldValues
{
    private static readonly string[] Times = [@"hh\:mm", @"hh\:mm\:ss"];

    /// <summary>The time formats a time field posts and shows, without and with seconds.</summary>
    public static IReadOnlyList<string> TimeFormats => Times;

    /// <summary>The indexes a list or pager field posts, comma-separated, dropping what is not an index.</summary>
    public static IReadOnlyList<int> Indices(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        return [.. value.Split(',').Select(part => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? index : -1).Where(index => index >= 0)];
    }

    public static bool TryDate(string? text, out DateTime? date)
    {
        date = null;
        if (text is null)
        {
            return false;
        }

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
        }

        return text.Length == 0 || date is not null;
    }

    public static bool TryTime(string? text, out TimeSpan? time)
    {
        time = null;
        if (text is null)
        {
            return false;
        }

        if (TimeSpan.TryParseExact(text, Times, CultureInfo.InvariantCulture, out var parsed))
        {
            time = parsed;
        }

        return text.Length == 0 || time is not null;
    }
}
