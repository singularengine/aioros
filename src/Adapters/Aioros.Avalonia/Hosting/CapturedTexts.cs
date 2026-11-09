using System.Collections.Immutable;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// The texts of a captured window with the bounds of their glyph lines in window pixels, as the compatibility bench
/// compares them with the browser's client rects of the same texts.
/// </summary>
internal static class CapturedTexts
{
    public static ImmutableArray<LaidOutText> Of(Window window) =>
        [.. window.GetVisualDescendants().OfType<Control>().Where(Shown).Select(control => Laid(window, control)).OfType<LaidOutText>()];

    private static LaidOutText? Laid(Window window, Control control)
    {
        var (text, layout, origin) = control switch
        {
            AccessText access => (Displayed(access.Text), access.TextLayout, new Point(access.Padding.Left, access.Padding.Top)),
            TextBlock block => (block.Inlines is { Count: > 0 } inlines ? inlines.Text : block.Text, block.TextLayout, new Point(block.Padding.Left, block.Padding.Top)),
            TextPresenter presenter => (presenter.Text, presenter.TextLayout, default(Point)),
            _ => (null, null, default),
        };
        if (string.IsNullOrWhiteSpace(text) || layout is null || layout.TextLines.Count == 0 || control.TransformToVisual(window) is not { } transform)
        {
            return null;
        }

        var left = layout.TextLines.Min(line => line.Start);
        var right = layout.TextLines.Max(Right);
        var bounds = new Rect(origin.X + left, origin.Y, Math.Max(0, right - left), layout.Height).TransformToAABB(transform);
        return bounds.Width <= 0 || bounds.Height <= 0 ? null : new LaidOutText(text, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static string? Displayed(string? text) =>
        text?.Replace("__", "\0", StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).Replace("\0", "_", StringComparison.Ordinal);

    private static double Right(TextLine line) => line.Start + line.Width;

    private static bool Shown(Control control) =>
        control.IsEffectivelyVisible && control.GetSelfAndVisualAncestors().All(visual => visual.Opacity > 0);
}
