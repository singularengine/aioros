using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Plans;

internal static class ValueFormats
{
    public static ValueFormatter? For(ValueFormat format) => Of(format).Formatter;

    public static Format Of(ValueFormat format) => format switch
    {
        ValueFormat.CssNumber => Slots.FormatOf("css-number"),
        ValueFormat.Points => Slots.FormatOf("points"),
        ValueFormat.Color => Slots.FormatOf("color"),
        ValueFormat.Icon => Slots.FormatOf("icon"),
        ValueFormat.Thickness => Slots.FormatOf("thickness"),
        ValueFormat.Shadow => Slots.FormatOf("shadow"),
        ValueFormat.CornerRadius => Slots.FormatOf("corner-radius"),
        ValueFormat.Tangent => Slots.FormatOf("tangent"),
        ValueFormat.Matrix => Slots.FormatOf("matrix"),
        ValueFormat.Transform => Slots.FormatOf("transform"),
        ValueFormat.HexColor => Slots.FormatOf("hex-color"),
        ValueFormat.Href => Slots.FormatOf("href"),
        ValueFormat.Track => Slots.FormatOf("track"),
        _ => Format.Text,
    };

    public static string Apply(ValueFormat format, object? value) => Of(format).Apply(value);
}
