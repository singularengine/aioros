using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Vectors;
using Aioros.Avalonia.Plans;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Aioros.Avalonia.Html.Writers;

internal static class MediaWriters
{
    public static void WriteImage(HtmlContext context, Image image, ParentKind parent)
    {
        if (AsyncImageLoader.ImageLoader.GetSource(image) is not null || LocalBindings.IsBound(image, AsyncImageLoader.ImageLoader.SourceProperty))
        {
            WriteRemoteImage(context, image, parent);
            return;
        }

        if (context.Plan.Planning && LocalBindings.IsBound(image, Image.SourceProperty))
        {
            context.Plan.Fail("Image.Source is bound");
        }

        if (image.Source is not Bitmap bitmap)
        {
            return;
        }

        var png = Drawings.Encode(bitmap);
        var style = context.Box(image, parent);
        style.Append((double.IsNaN(image.Width), double.IsNaN(image.Height)) switch
        {
            (false, true) => "height:auto;",
            (true, false) => "width:auto;",
            (true, true) when Covers(image, parent) => "width:100%;height:100%;",
            (true, true) => "max-width:100%;height:auto;",
            _ => string.Empty,
        });
        style.Append(Fit(image.Stretch));
        context.Plan.Literal($"<img src=\"data:image/png;base64,{png.Base64}\" alt=\"{CssValues.Encode(AutomationProperties.GetName(image))}\" width=\"{png.Width}\" height=\"{png.Height}\"");
        context.Root(image, null, style, attributes: false);
        context.Plan.Literal(">");
    }

    private static void WriteRemoteImage(HtmlContext context, Image image, ParentKind parent)
    {
        var style = context.Box(image, parent);
        if (double.IsNaN(image.Width))
        {
            style.Append("width:100%;");
        }

        if (double.IsNaN(image.Height))
        {
            style.Append("height:auto;");
        }

        style.Append(Fit(image.Stretch));
        context.Plan.Literal("<img src=\"");
        context.Plan.Value(image, AsyncImageLoader.ImageLoader.SourceProperty);
        context.Plan.Literal("\" alt=\"");
        context.Plan.Value(image, AutomationProperties.NameProperty);
        context.Plan.Literal("\" loading=\"lazy\" decoding=\"async\"");
        context.Root(image, null, style, attributes: false);
        context.Plan.Literal(">");
    }

    /// <summary>
    /// Whether Avalonia arranges the image over its whole box: stretched both ways to fill it (<c>Fill</c> or
    /// <c>UniformToFill</c>) outside a stack, which would measure it with no room along its direction.
    /// </summary>
    private static bool Covers(Image image, ParentKind parent) =>
        image.Stretch is Stretch.Fill or Stretch.UniformToFill
        && parent is not (ParentKind.Column or ParentKind.Row or ParentKind.Wrap)
        && image.HorizontalAlignment == global::Avalonia.Layout.HorizontalAlignment.Stretch
        && image.VerticalAlignment == global::Avalonia.Layout.VerticalAlignment.Stretch;

    private static string Fit(Stretch stretch) => stretch switch
    {
        Stretch.None => "object-fit:none;",
        Stretch.Fill => "object-fit:fill;",
        Stretch.UniformToFill => "object-fit:cover;",
        _ => "object-fit:contain;",
    };

    public static void WriteViewbox(HtmlContext context, Viewbox viewbox, ParentKind parent)
    {
        if (viewbox.Child is not { } child)
        {
            return;
        }

        child.Measure(Size.Infinity);
        var size = child.DesiredSize;
        var style = context.Box(viewbox, parent, size: viewbox.Stretch != Stretch.None);
        style.Append(viewbox.Stretch == Stretch.None ? string.Create(CultureInfo.InvariantCulture, $"width:{CssValues.Px(size.Width)};height:{CssValues.Px(size.Height)};") : string.Empty);
        style.Append(double.IsNaN(viewbox.Width) && double.IsNaN(viewbox.Height) && viewbox.Stretch != Stretch.None ? "width:100%;" : string.Empty);
        style.Append(viewbox.StretchDirection switch
        {
            StretchDirection.DownOnly => string.Create(CultureInfo.InvariantCulture, $"max-width:{CssValues.Px(size.Width)};max-height:{CssValues.Px(size.Height)};"),
            StretchDirection.UpOnly => string.Create(CultureInfo.InvariantCulture, $"min-width:{CssValues.Px(size.Width)};min-height:{CssValues.Px(size.Height)};"),
            _ => string.Empty,
        });
        var preserve = viewbox.Stretch switch
        {
            Stretch.Fill => "none",
            Stretch.UniformToFill => "xMidYMid slice",
            _ => "xMidYMid meet",
        };
        var width = CssValues.Num(size.Width);
        var height = CssValues.Num(size.Height);
        context.Plan.Literal($"<svg viewBox=\"0 0 {width} {height}\" preserveAspectRatio=\"{preserve}\" class=\"{context.Classes(viewbox, null, style)}\"><foreignObject width=\"{width}\" height=\"{height}\">");
        context.Write(child, ParentKind.Single);
        context.Plan.Literal("</foreignObject></svg>");
    }
}
