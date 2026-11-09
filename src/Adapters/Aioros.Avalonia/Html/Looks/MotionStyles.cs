using Aioros.Markup;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Aioros.Avalonia.Html.Looks;

internal static class MotionStyles
{
    private static readonly FrozenDictionary<string, string> CssProperties = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Height"] = "height",
        ["Width"] = "width",
        ["Opacity"] = "opacity",
        ["Background"] = "background-color",
        ["Foreground"] = "color",
        ["BorderBrush"] = "border-color",
        ["RenderTransform"] = "transform",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static HashSet<string> Append(Control control, StringBuilder style, StyleSheet sheet)
    {
        Transitions(control, style);
        var animated = new HashSet<string>(StringComparer.Ordinal);
        var animations = StylesFor(control)
            .SelectMany(item => item.Animations.OfType<Animation>())
            .Select(animation => Animation(animation, sheet, animated, control))
            .ToList();
        Spotlight(control, style, sheet);
        if (Marquee(control, sheet) is { } marquee)
        {
            animations.Add(marquee);
        }

        if (Sweep(control, sheet) is { } sweep)
        {
            style.Append("background-size:200% 100%;background-repeat:repeat-x;");
            animations.Add(sweep);
        }
        if (animations.Count > 0)
        {
            style.Append(CultureInfo.InvariantCulture, $"animation:{string.Join(',', animations)};");
        }

        if (animated.Contains("transform") && Entrances.Matrix(control.RenderTransform) is null or { IsIdentity: true } && control.RenderTransformOrigin != RelativePoint.Center)
        {
            style.Append(CultureInfo.InvariantCulture, $"transform-origin:{CssValues.Origin(control.RenderTransformOrigin)};");
        }

        return animated;
    }

    private static readonly FrozenDictionary<string, string> TransitionProperties = new Dictionary<string, string>(CssProperties, StringComparer.Ordinal)
    {
        ["BoxShadow"] = "box-shadow",
        ["BorderThickness"] = "border-width",
        ["CornerRadius"] = "border-radius",
        ["Padding"] = "padding",
        ["Margin"] = "margin",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static void Transitions(Control control, StringBuilder style)
    {
        if (control.Transitions is { Count: > 0 } transitions && TransitionList(transitions) is { } css)
        {
            style.Append(CultureInfo.InvariantCulture, $"transition:{css};");
        }
    }

    /// <summary>Avalonia transitions as the value of a CSS <c>transition</c>, one per property the web animates; null when none is.</summary>
    public static string? TransitionList(IEnumerable<ITransition> transitions)
    {
        var css = string.Join(',', transitions.OfType<TransitionBase>()
            .Where(transition => transition.Property is not null && TransitionProperties.ContainsKey(transition.Property.Name))
            .Select(transition => $"{TransitionProperties[transition.Property!.Name]} {CssValues.Ms(transition.Duration)} {CssValues.Ease(transition.Easing)} {CssValues.Ms(transition.Delay)}")
            .Distinct(StringComparer.Ordinal));
        return css.Length > 0 ? css : null;
    }

    /// <summary>
    /// The sweep of a text's gradient foreground (MOTION-26): one whole gradient, drawn twice as wide as the text, to the
    /// right every <see cref="Web.SweepProperty"/> seconds, without end; none for a solid foreground.
    /// </summary>
    private static string? Sweep(Control control, StyleSheet sheet)
    {
        if (Web.GetSweep(control) is not (> 0 and var seconds) || control is not TextBlock { Foreground: IGradientBrush })
        {
            return null;
        }

        const string Frames = "0%{background-position:0 0;}100%{background-position:-200% 0;}";
        var name = "a" + CssValues.Hash(Frames);
        sheet.AddRule(name, $"@keyframes {name}{{{Frames}}}\n");
        return $"{name} {CssValues.Ms(TimeSpan.FromSeconds(seconds))} linear 0ms infinite normal none";
    }

    /// <summary>A spotlight's color in the control's class and, once per page, the glow drawn from it under the pointer (MOTION-31).</summary>
    private static void Spotlight(Control control, StringBuilder style, StyleSheet sheet)
    {
        if (CssValues.Brush(Web.GetSpotlight(control)) is not { } color)
        {
            return;
        }

        style.Append(CultureInfo.InvariantCulture, $"--aioros-spotlight:{color};");
        sheet.AddRule("spotlight", "[data-aioros-spotlight]{position:relative}\n[data-aioros-spotlight]::after{content:\"\";position:absolute;inset:0;border-radius:inherit;pointer-events:none;background:radial-gradient(320px circle at var(--x,50%) var(--y,50%),var(--aioros-spotlight),transparent 70%);opacity:0;transition:opacity 300ms}\n[data-aioros-spotlight]:hover::after{opacity:1}\n");
    }

    /// <summary>A marquee's loop (MOTION-30): left by half its width every <see cref="Web.MarqueeProperty"/> seconds, without end, paused under the pointer.</summary>
    private static string? Marquee(Control control, StyleSheet sheet)
    {
        if (Web.GetMarquee(control) is not (> 0 and var seconds))
        {
            return null;
        }

        const string Frames = "0%{transform:translateX(0);}100%{transform:translateX(-50%);}";
        var name = "a" + CssValues.Hash(Frames);
        sheet.AddRule(name, $"@keyframes {name}{{{Frames}}}\n[data-aioros-marquee]:hover{{animation-play-state:paused}}\n");
        return $"{name} {CssValues.Ms(TimeSpan.FromSeconds(seconds))} linear 0ms infinite normal none";
    }

    private static string Animation(Animation animation, StyleSheet sheet, HashSet<string> animated, Control control)
    {
        var frames = new StringBuilder();
        foreach (var frame in animation.Children.OrderBy(child => child.Cue.CueValue))
        {
            frames.Append(CultureInfo.InvariantCulture, $"{CssValues.Num(frame.Cue.CueValue * 100)}%{{{Declarations(frame, animated, control)}}}");
        }

        var name = "a" + CssValues.Hash(frames.ToString());
        sheet.AddRule(name, $"@keyframes {name}{{{frames}}}\n");
        var iterations = animation.IterationCount.IsInfinite ? "infinite" : animation.IterationCount.Value.ToString(CultureInfo.InvariantCulture);
        return $"{name} {CssValues.Ms(animation.Duration)} {CssValues.Ease(animation.Easing)} {CssValues.Ms(animation.Delay)} {iterations} {Direction(animation.PlaybackDirection)} {Fill(animation.FillMode)}";
    }

    private static string Declarations(KeyFrame frame, HashSet<string> animated, Control control)
    {
        var declarations = new StringBuilder();
        var transforms = new Dictionary<AvaloniaProperty, object?>();
        foreach (var setter in frame.Setters.OfType<Setter>())
        {
            if (setter.Property is { } transformProperty && typeof(Transform).IsAssignableFrom(transformProperty.OwnerType))
            {
                transforms[transformProperty] = setter.Value;
                continue;
            }

            if (setter.Property is null || !CssProperties.TryGetValue(setter.Property.Name, out var property))
            {
                continue;
            }

            animated.Add(property);
            var value = setter.Value switch
            {
                IBrush brush => CssValues.Brush(brush),
                ITransform transform => CssValues.Transform(transform),
                double length when property is "width" or "height" => CssValues.Px(length),
                _ => Convert.ToString(setter.Value, CultureInfo.InvariantCulture),
            };
            declarations.Append(CultureInfo.InvariantCulture, $"{property}:{value};");
        }

        if (transforms.Count > 0 && control.RenderTransform is Transform rendered)
        {
            animated.Add("transform");
            declarations.Append(CultureInfo.InvariantCulture, $"transform:{FrameTransform(rendered, transforms)};");
        }

        return declarations.ToString();
    }

    private static string FrameTransform(Transform transform, Dictionary<AvaloniaProperty, object?> frame)
    {
        double Value(AvaloniaObject owner, AvaloniaProperty property) =>
            frame.TryGetValue(property, out var value) ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : owner.GetBaseValue(property) is double basis ? basis : (double)owner.GetValue(property)!;
        IEnumerable<Transform> parts = transform is TransformGroup group ? group.Children : [transform];
        return string.Join(' ', parts.Select(part => part switch
        {
            TranslateTransform translate => $"translate({CssValues.Px(Value(translate, TranslateTransform.XProperty))},{CssValues.Px(Value(translate, TranslateTransform.YProperty))})",
            ScaleTransform scale => $"scale({CssValues.Num(Value(scale, ScaleTransform.ScaleXProperty))},{CssValues.Num(Value(scale, ScaleTransform.ScaleYProperty))})",
            RotateTransform rotate => $"rotate({CssValues.Num(Value(rotate, RotateTransform.AngleProperty))}deg)",
            SkewTransform skew => $"skew({CssValues.Num(Value(skew, SkewTransform.AngleXProperty))}deg,{CssValues.Num(Value(skew, SkewTransform.AngleYProperty))}deg)",
            _ => CssValues.Matrix(part.Value),
        }));
    }

    private static string Direction(PlaybackDirection direction) => direction switch
    {
        PlaybackDirection.Reverse => "reverse",
        PlaybackDirection.Alternate => "alternate",
        PlaybackDirection.AlternateReverse => "alternate-reverse",
        _ => "normal",
    };

    private static string Fill(FillMode mode) => mode switch
    {
        FillMode.Forward => "forwards",
        FillMode.Backward => "backwards",
        FillMode.Both => "both",
        _ => "none",
    };

    private static IEnumerable<Style> StylesFor(Control control)
    {
        var sources = new List<IStyle>();
        for (StyledElement? element = control; element is not null; element = element.Parent as StyledElement)
        {
            sources.AddRange(element.Styles);
        }

        if (Application.Current is { } application)
        {
            sources.AddRange(application.Styles);
        }

        return Flatten(sources).OfType<Style>().Where(style => style.Animations.Count > 0 && Matches(style, control));
    }

    private static IEnumerable<IStyle> Flatten(IEnumerable<IStyle> styles) =>
        styles.SelectMany(style => style is Styles nested ? Flatten(nested) : [style]);

    /// <summary>Whether Avalonia applies the style to the control, its selector matched as Avalonia matches it (MOTION-20).</summary>
    private static bool Matches(Style style, Control control) =>
        style.Selector?.ToString() is { } selector && SelectorText.Matches(selector, control);
}
