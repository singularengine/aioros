using System.Globalization;
using Aioros.Runtime.Rendering;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// The sampled values of a local input's domain, as the browser posts them (decision "Guaranteed scope, closed per control
/// type"): each flag, each index and none, the two ends of a range and its middle, an empty, a short and a long text, two
/// dates and none, two times; the values a page's verification (<see cref="LocalProofs"/>) and a type's domain test walk.
/// </summary>
public static class LocalDomain
{
    /// <summary>The values of the field's domain, from its description and the field as the page wrote it; <see langword="null"/> posts nothing for it.</summary>
    public static IEnumerable<string?> Values(SlotSpec spec, PostedField? posted)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Kind switch
        {
            "check" or "toggle" => [posted?.Values.FirstOrDefault() ?? "on", null],
            "choice-index" when posted?.Values is not { Length: > 1 } && spec.Values is [var count, ..] => [.. Enumerable.Range(0, int.Parse(count, CultureInfo.InvariantCulture)).Select(static index => index.ToString(CultureInfo.InvariantCulture))],
            "choice-index" or "combo" or "choice-item" or "page" => [.. posted?.Values ?? []],
            "page-step" => ["on"],
            "slider" or "number" when posted?.Values is [var low, var high, ..] => [low, Middle(low, high), high],
            "text" when posted?.Posted is { } date && DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) => ["2025-06-14", "2026-01-31", string.Empty],
            "text" when posted?.Posted is { } time && TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) => ["08:15", "23:45"],
            _ => [string.Empty, "x", "A much longer text than the box shows at its width"],
        };
    }

    /// <summary>The fewest values the field's domain must hold to be walked: a step button posts only when pressed.</summary>
    public static int Least(SlotSpec spec) => spec?.Kind == "page-step" ? 1 : 2;

    private static string Middle(string low, string high) =>
        ((double.Parse(low, CultureInfo.InvariantCulture) + double.Parse(high, CultureInfo.InvariantCulture)) / 2).ToString(CultureInfo.InvariantCulture);
}
