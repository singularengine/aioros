using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Writers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// A slider written as a range input drawn as its control theme draws it: the input as tall (or, vertical, as wide) as
/// Avalonia lays the slider out, the track as thick and round as the theme's, filled up to the thumb from the value the
/// input holds (a <c>--v</c> property the server writes and the framework's script follows while the thumb moves), and
/// the thumb with the theme's size, fill, border, radius and box shadow, overflowing the input as Avalonia's does; the
/// fills and the thumb's look in each combination of pointed at, pressed and focused from the keyboard, read with the
/// slider in those states (its thumb pointed at and pressed with it, and the slider really focused, so a thumb theme
/// that follows <c>IsKeyboardFocusWithin</c> shows its ring).
/// </summary>
internal static class SliderLooks
{
    public static string Class(StyleSheet sheet, Slider slider, bool vertical)
    {
        ThemeLooks.ExpandTemplates(slider);
        var thumb = RangeParts.Part<Track>(slider, "PART_Track")?.Thumb;
        var decrease = RangeParts.Part<RepeatButton>(slider, "PART_DecreaseButton");
        var increase = RangeParts.Part<RepeatButton>(slider, "PART_IncreaseButton");
        StateLooks.LayOut(slider);
        var across = vertical ? slider.DesiredSize.Width - slider.Margin.Left - slider.Margin.Right : slider.DesiredSize.Height - slider.Margin.Top - slider.Margin.Bottom;
        var (width, height) = (thumb is null || double.IsNaN(thumb.Width) ? 20 : thumb.Width, thumb is null || double.IsNaN(thumb.Height) ? 20 : thumb.Height);
        var length = vertical ? height : width;
        var band = RangeParts.Number(slider.TryFindResource("SliderTrackThemeHeight", slider.ActualThemeVariant, out var thickness) ? thickness : 4);
        var radius = TrackRadius(decrease, band);
        IReadOnlyList<WebState> states = slider.IsEffectivelyEnabled ? [new(":pointerover", ":hover"), new(":pressed", ":active"), new(":focus-visible", ":focus-visible")] : [];
        var look = StateLooks.Resolve(slider, states, () => Snapshot(slider, thumb, decrease, increase));
        var rest = look.Rest;
        var range = Math.Max(slider.Maximum - slider.Minimum, double.Epsilon);
        var css = new StringBuilder("{appearance:none;-webkit-appearance:none;cursor:pointer;border:0;margin:0;padding:0;");
        css.Append(vertical ? $"writing-mode:vertical-lr;direction:rtl;width:{CssValues.Px(across)};" : $"height:{CssValues.Px(across)};");
        css.Append(SliderTicks(slider, vertical, length));
        css.Append(CultureInfo.InvariantCulture, $"--p:calc((var(--v,{CssValues.Num(slider.Minimum)}) - {CssValues.Num(slider.Minimum)}) / {CssValues.Num(range)} * (100% - {CssValues.Px(length)}) + {CssValues.Px(length / 2)});");
        css.Append(string.Concat(rest.Select(entry => $"{entry.Key}:{entry.Value};")));
        css.Append(slider.IsEffectivelyEnabled ? string.Empty : "cursor:default;");
        css.Append('}');
        var bandSize = vertical ? $"width:{CssValues.Px(band)};" : $"height:{CssValues.Px(band)};";
        var thumbCss = $"box-sizing:border-box;width:{CssValues.Px(width)};height:{CssValues.Px(height)};border-radius:{CssValues.Px(thumb?.CornerRadius.TopLeft is > 0 ? thumb.CornerRadius.TopLeft : ThumbRadius(thumb, Math.Min(width, height)))};background:var(--tb);border:var(--tw) solid var(--tc);box-shadow:var(--ts)";
        css.Append(Bands(vertical, bandSize, radius, Inset(decrease, vertical, atEnd: false), Inset(increase, vertical, atEnd: true), band, width, height, thumbCss));
        css.Append(string.Concat(look.Rules.Select(rule => $"${rule.Selector}{{{string.Concat(rule.Rule.Select(entry => $"{entry.Key}:{entry.Value};"))}}}")));
        var name = "rg" + CssValues.Hash(css.ToString());
        sheet.AddRule(name, "." + name + css.ToString().Replace("$", "." + name, StringComparison.Ordinal) + "\n");
        return name;
    }

    private static readonly string[] Shared = [":pointerover", ":pressed"];

    /// <summary>The fills and the thumb's look as custom properties of the input; with the slider pointed at or pressed its thumb is too, and with it focused from the keyboard it is really focused.</summary>
    private static Dictionary<string, string> Snapshot(Slider slider, Thumb? thumb, RepeatButton? decrease, RepeatButton? increase)
    {
        var shared = thumb is null ? [] : Shared.Where(state => slider.Classes.Contains(state) && !thumb.Classes.Contains(state)).ToList();
        shared.ForEach(state => ((IPseudoClasses)thumb!.Classes).Add(state));
        var top = TopLevel.GetTopLevel(slider);
        var previous = top?.FocusManager?.GetFocusedElement();
        var focusing = slider.Classes.Contains(":focus-visible") && !slider.IsKeyboardFocusWithin;
        var focused = focusing && slider.Focus(NavigationMethod.Tab);
        var classes = focusing && !focused && thumb is not null ? FocusClasses(thumb).Where(name => !thumb.Classes.Contains(name)).ToList() : [];
        classes.ForEach(name => thumb!.Classes.Add(name));
        try
        {
            return Face(thumb, decrease, increase);
        }
        finally
        {
            if (focused && !(previous is InputElement element && element.Focus()))
            {
                top?.Focus();
            }

            classes.ForEach(name => thumb!.Classes.Remove(name));
            shared.ForEach(state => ((IPseudoClasses)thumb!.Classes).Remove(state));
        }
    }

    private const string BoundClass = "__AvaloniaReserved::Classes::";

    /// <summary>
    /// The classes a part binds to its templated control's keyboard focus (<c>Classes.focused="{Binding IsKeyboardFocusWithin, …}"</c>,
    /// as a thumb, which takes no focus of its own, shows its slider's): set when the slider cannot be focused where the page
    /// is written, as the binding would set them.
    /// </summary>
    private static IEnumerable<string> FocusClasses(Control part) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(part)
            .Where(property => property.Name.StartsWith(BoundClass, StringComparison.Ordinal))
            .Where(property => Plans.LocalBindings.Of(part, property) is IDescription { Description: { } path } && (path.Contains("IsKeyboardFocusWithin", StringComparison.Ordinal) || path.Contains("IsFocused", StringComparison.Ordinal)))
            .Select(property => property.Name[BoundClass.Length..]);

    private static Dictionary<string, string> Face(Thumb? thumb, RepeatButton? decrease, RepeatButton? increase)
    {
        var face = thumb?.GetVisualDescendants().OfType<Border>().FirstOrDefault();
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["--f"] = RangeParts.Color(decrease?.Background),
            ["--r"] = RangeParts.Color(increase?.Background),
            ["--tb"] = RangeParts.Color(face?.Background ?? thumb?.Background),
            ["--tc"] = RangeParts.Color(face?.BorderBrush ?? thumb?.BorderBrush),
            ["--tw"] = CssValues.Px(face?.BorderThickness.Top ?? thumb?.BorderThickness.Top ?? 0),
            ["--ts"] = face is { BoxShadow.Count: > 0 } ? BoxStyles.Shadow(face.BoxShadow) : "none",
        };
    }

    private static double ThumbRadius(Thumb? thumb, double size) =>
        thumb?.GetVisualDescendants().OfType<Border>().FirstOrDefault() is { } face ? Math.Min(face.CornerRadius.TopLeft, size / 2) : size / 2;

    /// <summary>The track's band, filled up to the thumb and clear where the parts' bands stop short of its ends, and the thumb, for each engine.</summary>
    private static string Bands(bool vertical, string bandSize, double radius, double start, double end, double band, double width, double height, string thumbCss)
    {
        var angle = vertical ? "0deg" : "90deg";
        var clearStart = start > 0 ? $"transparent 0 {CssValues.Px(start)}," : string.Empty;
        var fill = end > 0 ? $"var(--r) 0 calc(100% - {CssValues.Px(end)}),transparent 0" : "var(--r) 0";
        var track = end > 0 ? $"linear-gradient({angle},var(--r) 0 calc(100% - {CssValues.Px(end)}),transparent 0)" : "var(--r)";
        var progress = start > 0 ? $"linear-gradient({angle},transparent 0 {CssValues.Px(start)},var(--f) 0)" : "var(--f)";
        return string.Create(CultureInfo.InvariantCulture, $"$::-webkit-slider-runnable-track{{{bandSize}border-radius:{CssValues.Px(radius)};background:linear-gradient({angle},{clearStart}var(--f) 0 var(--p),{fill})}}")
            + string.Create(CultureInfo.InvariantCulture, $"$::-webkit-slider-thumb{{-webkit-appearance:none;appearance:none;margin-block-start:{CssValues.Px((band - (vertical ? width : height)) / 2)};{thumbCss}}}")
            + string.Create(CultureInfo.InvariantCulture, $"$::-moz-range-track{{{bandSize}border-radius:{CssValues.Px(radius)};background:{track}}}$::-moz-range-progress{{{bandSize}border-radius:{CssValues.Px(radius)};background:{progress}}}$::-moz-range-thumb{{{thumbCss}}}");
    }

    /// <summary>
    /// How far a part's band stops short of the track's end it reaches (the start for the decrease part, the far end for the
    /// increase part): the margins of its innermost painted border and of the elements around it inside the part, and the
    /// paddings of those elements, on that side, as its template states them; 0 when the band reaches the end.
    /// </summary>
    private static double Inset(RepeatButton? part, bool vertical, bool atEnd)
    {
        if (part?.GetVisualDescendants().OfType<Border>().LastOrDefault(border => border.Background is global::Avalonia.Media.ISolidColorBrush { Color.A: > 0, Opacity: > 0 } or global::Avalonia.Media.IGradientBrush) is not { } band)
        {
            return 0;
        }

        double Side(Thickness thickness) => (vertical, atEnd) switch
        {
            (false, false) => thickness.Left,
            (false, true) => thickness.Right,
            (true, false) => thickness.Bottom,
            (true, true) => thickness.Top,
        };

        var inset = 0.0;
        for (var element = (Visual)band; !ReferenceEquals(element, part) && element is Layoutable placed; element = element.GetVisualParent()!)
        {
            inset += Side(placed.Margin) + (!ReferenceEquals(element, band) && element is Decorator { Padding: var padding } ? Side(padding) : 0);
        }

        return Math.Max(0, inset);
    }

    /// <summary>The track's radius: the largest corner of the decrease part's own border, as thick as the track.</summary>
    private static double TrackRadius(RepeatButton? decrease, double band)
    {
        var parts = decrease?.GetVisualDescendants().OfType<Border>().Where(border => border.CornerRadius != default).ToList() ?? [];
        var corners = parts.Select(border => Math.Max(Math.Max(border.CornerRadius.TopLeft, border.CornerRadius.TopRight), Math.Max(border.CornerRadius.BottomLeft, border.CornerRadius.BottomRight))).ToList();
        var corner = corners.Count > 0 ? corners.Max() : decrease is null ? 0 : Math.Max(decrease.CornerRadius.TopLeft, decrease.CornerRadius.TopRight);
        return Math.Min(corner, band / 2);
    }

    /// <summary>The ticks as gradients across the input where the theme's visible tick bars lie.</summary>
    private static string SliderTicks(Slider slider, bool vertical, double thumb)
    {
        var count = slider.TickFrequency > 0 ? (slider.Maximum - slider.Minimum) / slider.TickFrequency : 0;
        if (slider.TickPlacement == TickPlacement.None || count < 1 || count > 500)
        {
            return "background:none;";
        }

        var bars = slider.GetVisualDescendants().OfType<TickBar>().Where(bar => bar.IsVisible && bar.Bounds.Width > 0 && bar.Bounds.Height > 0).ToList();
        if (bars.Count == 0)
        {
            return "background:none;";
        }

        var color = RangeParts.Color(bars[0].Fill);
        var gradient = $"repeating-linear-gradient({(vertical ? "180deg" : "90deg")},{color} 0 1px,transparent 1px calc((100% - 1px) / {CssValues.Num(count)}))";
        var parts = bars.Select(bar =>
        {
            var at = bar.TranslatePoint(default, slider) ?? default;
            return vertical
                ? $"{gradient} {CssValues.Px(at.X)} {CssValues.Px(thumb / 2)}/{CssValues.Px(bar.Bounds.Width)} calc(100% - {CssValues.Px(thumb - 1)}) no-repeat"
                : $"{gradient} {CssValues.Px(thumb / 2)} {CssValues.Px(at.Y)}/calc(100% - {CssValues.Px(thumb - 1)}) {CssValues.Px(bar.Bounds.Height)} no-repeat";
        });
        return $"background:{string.Join(',', parts)};";
    }
}
