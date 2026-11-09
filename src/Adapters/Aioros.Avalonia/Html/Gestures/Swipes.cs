using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Markup;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Gestures;

/// <summary>
/// What a swiped element declares (<c>a:Web.Swipe</c>): its directions in <c>data-aioros-swipe</c>, lowercased and
/// space separated, and how far or how fast it is let go away in <c>data-aioros-swipe-by</c>, for the framework's swipe
/// module, and the element takes no touch gesture of the browser's own (<c>touch-action:none</c>, as sonner's toasts and
/// vaul's drawer), a scroll area inside it keeping its own. Unknown directions are left out; nothing is written without a
/// known one.
/// </summary>
internal static class Swipes
{
    private static readonly string[] Known = ["up", "down", "left", "right"];

    public static string Attributes(Control control, StyleSheet sheet)
    {
        if (Web.GetSwipe(control) is not { Length: > 0 } declared)
        {
            return string.Empty;
        }

        var directions = declared.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(direction => direction.ToLowerInvariant()).Where(Known.Contains).Distinct().ToList();
        if (directions.Count == 0)
        {
            return string.Empty;
        }

        sheet.AddRule("swipe", "[data-aioros-swipe]{touch-action:none}\n");
        return string.Create(CultureInfo.InvariantCulture, $" data-aioros-swipe=\"{string.Join(' ', directions)}\" data-aioros-swipe-by=\"{Web.GetSwipeDistance(control)} {Web.GetSwipeVelocity(control)}\"");
    }
}
