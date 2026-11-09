using System.Collections.Immutable;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Panels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Vectors;

internal readonly record struct Png(string Base64, string Width, string Height);

internal static class Drawings
{
    private static ImmutableDictionary<Type, bool> _selfDrawn = ImmutableDictionary<Type, bool>.Empty;

    public static bool IsSelfDrawn(Control control) =>
        !control.GetVisualChildren().Any()
        && ImmutableInterlocked.GetOrAdd(ref _selfDrawn, control.GetType(), static type =>
            type.Assembly != typeof(Control).Assembly && type.GetMethod(nameof(Visual.Render), [typeof(DrawingContext)])?.DeclaringType?.Assembly == type.Assembly);

    public static Png Render(Control control)
    {
        var size = Lay(control);
        var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width)), Math.Max(1, (int)Math.Ceiling(size.Height)));
        using var bitmap = new RenderTargetBitmap(pixels);
        bitmap.Render(control);
        return Encode(bitmap);
    }

    /// <summary>The control's own drawing as SVG with the given root attributes, or null when it cannot be recorded as SVG.</summary>
    public static string? Svg(Control control, string attributes) => DrawingSvg.Write(control, Lay(control), attributes);

    private static Size Lay(Control control)
    {
        var size = new Size(control.Width, control.Height);
        if (double.IsNaN(size.Width) || double.IsNaN(size.Height))
        {
            control.Measure(Viewports.Of(control));
            size = new Size(double.IsNaN(control.Width) ? control.DesiredSize.Width : control.Width, double.IsNaN(control.Height) ? control.DesiredSize.Height : control.Height);
        }

        control.Measure(size);
        control.Arrange(new Rect(size));
        return size;
    }

    public static Png Encode(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return new Png(
            Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length),
            CssValues.Num(bitmap.Size.Width),
            CssValues.Num(bitmap.Size.Height));
    }
}
