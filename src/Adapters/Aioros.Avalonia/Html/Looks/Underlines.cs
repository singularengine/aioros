using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Looks;

internal static class Underlines
{
    public static string Offset(AvaloniaObject text, TextDecorationCollection? decorations)
    {
        if (decorations?.FirstOrDefault(decoration => decoration.Location == TextDecorationLocation.Underline) is not { StrokeOffsetUnit: not TextDecorationUnit.FontRecommended, StrokeOffset: not 0 } underline)
        {
            return "auto";
        }

        var size = text.GetValue(TextElement.FontSizeProperty);
        var typeface = new Typeface(text.GetValue(TextElement.FontFamilyProperty), text.GetValue(TextElement.FontStyleProperty), text.GetValue(TextElement.FontWeightProperty), text.GetValue(TextElement.FontStretchProperty));
        var (position, recommended) = FontManager.Current.TryGetGlyphTypeface(typeface, out var glyph) && glyph.Metrics.DesignEmHeight != 0
            ? (glyph.Metrics.UnderlinePosition * size / glyph.Metrics.DesignEmHeight, glyph.Metrics.UnderlineThickness * size / glyph.Metrics.DesignEmHeight)
            : (0, 0);
        var thickness = underline.StrokeThicknessUnit switch
        {
            TextDecorationUnit.FontRecommended => recommended,
            TextDecorationUnit.FontRenderingEmSize => underline.StrokeThickness * size,
            _ => underline.StrokeThickness,
        };
        var offset = underline.StrokeOffsetUnit == TextDecorationUnit.Pixel ? underline.StrokeOffset : underline.StrokeOffset * size;
        return CssValues.Px(position + offset - thickness / 2);
    }
}
