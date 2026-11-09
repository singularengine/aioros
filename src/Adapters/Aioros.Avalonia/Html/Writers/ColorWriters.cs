using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

internal static class ColorWriters
{
    public static void WriteColorPicker(HtmlContext context, ColorPicker picker, ParentKind parent)
    {
        ThemeLooks.ExpandTemplates(picker);
        var button = picker.GetVisualDescendants().OfType<DropDownButton>().FirstOrDefault();
        var size = Laid(button ?? (Control)picker);
        var style = context.Box(picker, parent, size: false);
        style.Append(size.Width > 0 ? $"width:{CssValues.Px(size.Width)};height:{CssValues.Px(size.Height)};" : string.Empty);
        if (button is not null)
        {
            BoxStyles.Fill(style, button.Background, button.BorderBrush, button.BorderThickness, button.CornerRadius, button.Padding);
        }

        style.Append("cursor:pointer;");
        context.Plan.Literal("<input type=\"color\" name=\"f");
        context.Field(picker);
        context.Plan.Literal("\"");
        context.Root(picker, null, style);
        context.Plan.Literal(" value=\"");
        context.Plan.Value(picker, ColorView.ColorProperty, ValueFormat.HexColor);
        context.Plan.Literal($"\"{(picker.IsEffectivelyEnabled ? string.Empty : " disabled")}>");
    }

    private static Size Laid(Control control)
    {
        if (control.Bounds.Width > 0)
        {
            return control.Bounds.Size;
        }

        control.Measure(Size.Infinity);
        return control.DesiredSize;
    }

    /// <summary>A posted <c>#rrggbb</c>, the only value a color input posts, given to the picker with the alpha it has.</summary>
    public static void BindColor(ColorPicker picker, string? value)
    {
        if (Posted(value) is { } rgb)
        {
            picker.Color = Color.FromArgb(picker.Color.A, rgb.R, rgb.G, rgb.B);
        }
    }

    /// <summary>The channels a color input posts (<c>#rrggbb</c>), or nothing for any other value.</summary>
    public static Color? Posted(string? value) =>
        value is { Length: 7 } && value[0] == '#' && uint.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)
            ? Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
            : null;

    public static string Hex(object? value) => value is Color color
        ? string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}")
        : "#000000";
}
