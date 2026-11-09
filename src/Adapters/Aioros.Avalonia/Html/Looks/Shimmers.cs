using Aioros.Markup;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// The text shimmer a library asks for with <see cref="Web.ShimmerProperty"/>, written as shadcn/ui's <c>shimmer</c>
/// utility (the shadcn package 4.21.5): a gradient of the text's color and a lighter highlight, clipped to the text and
/// swept across it over two seconds, its highlight the theme's (lighter and stronger in the dark), reversed right to left
/// and still for a visitor who asks for less motion.
/// </summary>
internal static class Shimmers
{
    private const string Gradient =
        "--_spread:var(--shimmer-spread,calc(3ch + 40px));--_base:currentColor;" +
        "background-image:linear-gradient(calc(90deg + 20deg),var(--_base) calc(50% - var(--_spread)),color-mix(in oklch,var(--_highlight),var(--_base) 50%) calc(50% - var(--_spread) * 0.5),var(--_highlight) 50%,color-mix(in oklch,var(--_highlight),var(--_base) 50%) calc(50% + var(--_spread) * 0.5),var(--_base) calc(50% + var(--_spread)));" +
        "background-repeat:no-repeat;background-size:calc(200% + var(--_spread) * 2) 100%;background-position:0 0;background-clip:text;-webkit-background-clip:text;-webkit-text-fill-color:transparent;animation:ashm 2s linear infinite}";

    private const string Light = ".shm{--_highlight:var(--shimmer-color,oklch(from currentColor l c h / calc(alpha * 0.2)));" + Gradient;

    private const string Dark = ".shmd{--_highlight:var(--shimmer-color,oklch(from currentColor max(0.8, calc(l + 0.4)) c h / calc(alpha + 0.4)));" + Gradient;

    private const string Shared =
        "@keyframes ashm{from{background-position:100% 0}to{background-position:0 0}}" +
        ":where(.shm,.shmd):where([dir=rtl],[dir=rtl] *){animation-direction:reverse}" +
        "@media (prefers-reduced-motion:reduce){:where(.shm,.shmd){animation:none;background-image:none;-webkit-text-fill-color:currentColor}}\n";

    /// <summary>The control's classes with its shimmer's, its rules added to the sheet once, or the classes as they are.</summary>
    public static string? Mark(Control control, StyleSheet sheet, string? classes)
    {
        if (!Web.GetShimmer(control))
        {
            return classes;
        }

        var dark = control.ActualThemeVariant == ThemeVariant.Dark;
        sheet.AddRule(dark ? "shimmer-dark" : "shimmer", (dark ? Dark : Light) + "\n");
        sheet.AddRule("shimmer-shared", Shared);
        return CssValues.Join(classes, dark ? "shmd" : "shm");
    }
}
