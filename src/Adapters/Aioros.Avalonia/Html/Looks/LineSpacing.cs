using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Looks;

internal static class LineSpacing
{
    public static string Rule(Control root) => Ratio(root) is { } ratio
        ? string.Create(CultureInfo.InvariantCulture, $"*{{line-height:round(up,{Math.Round(ratio, 5)}em,1px)}}\n")
        : string.Empty;

    public static double? Line(Control control, double fontSize) => Ratio(control) is { } ratio ? Math.Ceiling(Math.Round(ratio, 5) * fontSize) : null;

    private static double? Ratio(Control control)
    {
        var typeface = new Typeface(control.GetValue(TextElement.FontFamilyProperty));
        if (!FontManager.Current.TryGetGlyphTypeface(typeface, out var glyph) || glyph.Metrics.DesignEmHeight == 0)
        {
            return null;
        }

        var metrics = glyph.Metrics;
        return (Math.Abs(metrics.Ascent) + Math.Abs(metrics.Descent) + metrics.LineGap) / (double)metrics.DesignEmHeight;
    }
}
