using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Vectors;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A control's tool tip, the WAI-ARIA tooltip pattern as Avalonia shows it: the control is described by a
/// <c>role="tooltip"</c> element (<c>aria-describedby</c>) placed as <c>ToolTip.Placement</c> and its offsets place it
/// (flipped when it would leave the view), shown by CSS while the pointer rests on the control for its
/// <c>ToolTip.ShowDelay</c> and while it has the keyboard focus (<c>:focus-visible</c>, not a click's focus), its bubble
/// and what its template draws around it (an arrow) as the <c>ToolTip</c>'s theme realizes them. The overlay module hides
/// it on Escape and skips the delay of a tip shown within <c>ToolTip.BetweenShowDelay</c> of another.
/// </summary>
internal static class TipWriters
{
    private const string Css =
        ".tip{display:contents;anchor-scope:--tip}.tip>:first-child{anchor-name:--tip}" +
        ".tt{position:fixed;inset:auto;margin:0;position-anchor:--tip;width:max-content;" +
        "z-index:1000;pointer-events:none;opacity:0;visibility:hidden;transition:opacity .15s ease,visibility 0s linear .15s}" +
        ".tip:hover>.tt,.tip:has(:focus-visible)>.tt{opacity:1;visibility:visible;transition:opacity .15s ease var(--tip-delay,.4s),visibility 0s linear var(--tip-delay,.4s)}" +
        ".tip[data-off]>.tt{opacity:0;visibility:hidden;transition:none}[data-aioros-tip-skip] .tip>.tt{--tip-delay:0s}" +
        "@supports not (anchor-name:--a){.tt{display:none}}\n";

    /// <summary>The text of a tip read for its look while a bound tip has none (a word joiner, which shows nothing).</summary>
    private const string Probe = "\u2060";

    public static string Open(Control control, string? marker = null) =>
        string.Create(CultureInfo.InvariantCulture, $"<span class=\"{CssValues.Join("tip", marker)}\" data-aioros-overlay data-between=\"{ToolTip.GetBetweenShowDelay(control)}\">");

    public static void Write(HtmlContext context, Control control, object? tip, string id)
    {
        context.Sheet.AddRule("tip", Css);
        var delay = string.Create(CultureInfo.InvariantCulture, $"--tip-delay:{ToolTip.GetShowDelay(control)}ms;");
        var (bubble, around, room) = Read(context, control, tip);
        var placement = ToolTip.GetPlacement(control) == PlacementMode.Pointer
            ? string.Create(CultureInfo.InvariantCulture, $"position-area:bottom;margin-top:{CssValues.Px(6 + room.Top)};position-try-fallbacks:flip-block;")
            : Placements.Of(ToolTip.GetPlacement(control), ToolTip.GetHorizontalOffset(control), ToolTip.GetVerticalOffset(control), room: room);

        var open = $"<span role=\"tooltip\" id=\"{id}\" class=\"tt {CssValues.Join(bubble, context.Sheet.Class(null, new StringBuilder(delay)))}\" style=\"{placement}";
        var content = tip is ToolTip box ? box.Content : tip;
        if (content is Control child)
        {
            WriteInside(context, control, tip as ToolTip, child, open);
            context.Plan.Literal($"{around}</span>");
            return;
        }

        context.Plan.Literal(open + "\">");
        if (tip is ToolTip)
        {
            context.Plan.Text(content?.ToString());
        }
        else if (tip is not null && ToolTip.GetTip(control) is null)
        {
            context.Plan.Text(tip?.ToString());
        }
        else
        {
            context.Plan.Value(control, ToolTip.TipProperty);
        }

        context.Plan.Literal($"{around}</span>");
    }

    /// <summary>Writes a tip's control content inside the <c>ToolTip</c> Avalonia shows it in, so the styles and templates that reach it there apply.</summary>
    private static void WriteInside(HtmlContext context, Control control, ToolTip? own, Control child, string open)
    {
        var shown = own ?? new ToolTip { Theme = control.FindResource(typeof(ToolTip)) as global::Avalonia.Styling.ControlTheme, Content = child };
        var written = false;
        Probes.With(control, shown, realized =>
        {
            if (realized is ToolTip probe)
            {
                context.Plan.Literal(open + Fit(probe) + "\">");
                context.Write(child, ParentKind.Column);
                written = true;
            }
        });
        if (own is null)
        {
            shown.Content = null;
        }

        if (!written)
        {
            context.Plan.Literal(open + "\">");
            context.Write(child, ParentKind.Column);
        }
    }

    /// <summary>
    /// The width Avalonia lays a tip out at when its content wraps at the tip's <c>MaxWidth</c>: its widest line and the
    /// padding, which CSS cannot shrink a wrapped box to (a box of wrapped text takes its whole available width).
    /// </summary>
    private static string Fit(ToolTip tip) =>
        double.IsFinite(tip.MaxWidth) && SurfaceLooks.Root(tip) is { } root && root.DesiredSize.Width < tip.MaxWidth - 1
            && tip.GetVisualDescendants().OfType<TextBlock>().Any(text => text.TextLayout.TextLines.Count > 1)
            ? string.Create(CultureInfo.InvariantCulture, $"width:{CssValues.Px(Math.Ceiling(root.DesiredSize.Width) + 1)};")
            : string.Empty;

    private static ImmutableDictionary<string, (string Css, string? Around, Thickness Room, string? Motion)> _looks = ImmutableDictionary<string, (string Css, string? Around, Thickness Room, string? Motion)>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>Forgets the tips' looks, when a theme or a style changes.</summary>
    public static void Clear() => _looks = _looks.Clear();

    /// <summary>
    /// The look of the tip Avalonia shows for the control (a bound tip with no text read with a probe text), or Fluent's
    /// resources when it shows none; a tip of text, content or a control takes the look of its theme in the control's theme variant,
    /// read once (a tip of its own <c>ToolTip</c> is read each time, its theme and classes being its own).
    /// </summary>
    private static (string Bubble, string? Around, Thickness Room) Read(HtmlContext context, Control control, object? tip)
    {
        var key = tip is ToolTip ? null : string.Create(CultureInfo.InvariantCulture, $"{control.ActualThemeVariant}|{StyleContext.Theme(control.FindResource(typeof(ToolTip)) as global::Avalonia.Styling.ControlTheme)}|{StyleContext.Of(control)}");
        if (key is not null && _looks.TryGetValue(key, out var known))
        {
            return (Bubble(context, known), known.Around, known.Room);
        }

        (string Css, string? Around, Thickness Room, string? Motion)? look = null;
        var shown = tip as ToolTip ?? new ToolTip { Content = tip as string ?? Probe, Theme = control.FindResource(typeof(ToolTip)) as global::Avalonia.Styling.ControlTheme };
        if (tip is null or string or ToolTip or Control)
        {
            Probes.With(control, shown, realized => look = realized is ToolTip probe ? Look(probe) : null);
        }

        if (look is not { } read)
        {
            return (Fallback(context, control), null, default);
        }

        if (key is not null)
        {
            _looks = _looks.SetItem(key, read);
        }

        return (Bubble(context, read), read.Around, read.Room);
    }

    private static string Bubble(HtmlContext context, (string Css, string? Around, Thickness Room, string? Motion) look)
    {
        var bubble = context.Sheet.Class(null, new StringBuilder(look.Css));
        if (look.Motion is not { } motion)
        {
            return bubble;
        }

        var name = "tm" + CssValues.Hash(motion);
        context.Sheet.AddRule(name, motion.Replace("$", name, StringComparison.Ordinal));
        return bubble + " " + name;
    }

    /// <summary>
    /// How the tip enters and leaves when its theme moves its template's root (Aioros.Cosmo's tooltip: tw-animate-css's
    /// <c>fade-in-0 zoom-in-95 slide-in-from-bottom-2</c> in, <c>fade-out-0 zoom-out-95</c> out): the entrance as an
    /// animation from the look the root is shown at, played once the tip's delay has passed, and the exit as transitions to
    /// the root's look while the tip is <c>:closing</c>, from wherever the tip stands (a tip left before it showed does
    /// not flash); none of it for a visitor who asks for less motion. Rules with <c>$</c> for their class; null when the
    /// theme moves nothing, so the tip keeps Aioros's fade.
    /// </summary>
    private static string? Motion(ToolTip tip)
    {
        var entrances = Entrances.Settle(tip);
        var root = tip.GetVisualChildren().OfType<Control>().FirstOrDefault();
        var moving = entrances.ContainsKey(tip) ? tip : root;
        if (moving is null || entrances.GetValueOrDefault(moving) is not { } entrance || Entrances.Leaving(tip, moving) is not { } leaving || moving.Transitions is not { Count: > 0 } transitions)
        {
            return null;
        }

        var moves = transitions.OfType<TransitionBase>().ToList();
        var longest = moves.Select(move => move.Duration + move.Delay).DefaultIfEmpty().Max();
        var ease = CssValues.Ease(moves.FirstOrDefault()?.Easing);
        var origin = CssValues.Origin(moving.RenderTransformOrigin);
        var leave = leaving.Transitions ?? MotionStyles.TransitionList(transitions);
        var shown = ".tip:hover>.tt.$,.tip:has(:focus-visible)>.tt.$";
        return string.Create(CultureInfo.InvariantCulture,
            $".tt.${{{leaving.Css}transform-origin:{origin};transition:{leave},visibility 0s linear {CssValues.Ms(leaving.Longest)}}}\n" +
            $"{shown}{{opacity:1;transform:none;transition:visibility 0s linear var(--tip-delay,.4s);animation:k$ {CssValues.Ms(longest)} {ease} var(--tip-delay,.4s) backwards}}\n" +
            $".tip[data-off]>.tt.${{transition:none;animation:none}}@keyframes k${{from{{{entrance}}}}}\n" +
            $"@media (prefers-reduced-motion:reduce){{.tt.$,{shown}{{transition:visibility 0s;animation:none}}}}\n");
    }

    /// <summary>
    /// The bubble's look (its template's root border), the SVG of what the template draws outside the bubble (an arrow)
    /// placed from the bubble's side and middle as Avalonia places it, so it holds for a bubble of any size, and the room
    /// the template leaves around the bubble for it, which the placement keeps between the bubble and the control.
    /// </summary>
    private static (string Css, string? Around, Thickness Room, string? Motion) Look(ToolTip tip)
    {
        var motion = Motion(tip);
        var css = SurfaceLooks.Of(tip).ToString() + TextWriters.TextWrap(tip);
        if (SurfaceLooks.Root(tip) is not { } root || root.TranslatePoint(default, tip) is not { } at)
        {
            return (css, null, default, motion);
        }

        var size = tip.Bounds.Size;
        var bubble = new Rect(at, root.Bounds.Size);
        var room = new Thickness(at.X, at.Y, size.Width - bubble.Right, size.Height - bubble.Bottom);
        var parts = tip.GetVisualDescendants().OfType<Control>()
            .Where(part => part is Border or Shape && !ReferenceEquals(part, root) && !root.IsVisualAncestorOf(part) && Probes.Shows(part, tip) && part.Bounds.Width > 0)
            .ToList();
        if (parts.Count == 0)
        {
            return (css, null, room, motion);
        }

        var drawn = parts.Select(part => (Part: part, Transform: part.TransformToVisual(tip) ?? Matrix.Identity)).ToList();
        var union = drawn.Select(entry => new Rect(entry.Part.Bounds.Size).TransformToAABB(entry.Transform)).Aggregate((sum, box) => sum.Union(box));
        var svg = DrawingSvg.Parts(drawn.Select(entry => ((Visual)entry.Part, entry.Transform * Matrix.CreateTranslation(-union.X, -union.Y), entry.Part.Opacity)), union.Size);
        var across = string.Create(CultureInfo.InvariantCulture, $"left:calc(50% + {CssValues.Px(union.X - bubble.Center.X)})");
        var along = union.Center.Y > bubble.Center.Y
            ? string.Create(CultureInfo.InvariantCulture, $"top:calc(100% + {CssValues.Px(union.Y - bubble.Bottom)})")
            : string.Create(CultureInfo.InvariantCulture, $"top:{CssValues.Px(union.Y - bubble.Y)}");
        var around = svg?.Insert(4, $" aria-hidden=\"true\" style=\"position:absolute;{across};{along};overflow:visible;pointer-events:none\"");
        return (css, around, room, motion);
    }

    /// <summary>The bubble of a tip Avalonia did not show: Fluent's resources.</summary>
    private static string Fallback(HtmlContext context, Control control)
    {
        var background = ThemeResources.Css(control, "ToolTipBackground", "#f9f9f9");
        var foreground = ThemeResources.Css(control, "ToolTipForeground", "inherit");
        var border = ThemeResources.Css(control, "ToolTipBorderBrush", "rgba(0,0,0,.1)");
        var thickness = ThemeResources.Css(control, "ToolTipBorderThemeThickness", "1px");
        var padding = ThemeResources.Css(control, "ToolTipBorderThemePadding", "8px 5px 8px 7px");
        var size = ThemeResources.Css(control, "ToolTipContentThemeFontSize", "12px");
        var radius = ThemeResources.Css(control, "OverlayCornerRadius", "8px");
        var width = ThemeResources.Css(control, "ToolTipContentMaxWidth", "320px");
        return context.Sheet.Class(null, new StringBuilder($"background:{background};color:{foreground};border-color:{border};border-style:solid;border-width:{thickness};padding:{padding};font-size:{size};border-radius:{radius};max-width:{width};"));
    }
}
