using System.Collections.Immutable;
using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// Settles the transitions a page is in the middle of when it is written: each running transition lands on its value at
/// once (its transitions are held and given back, which ends the running ones and keeps them for later changes), so the
/// page is written as it ends up and not mid-way (an overlay entering from a <c>starting</c> look is not written at
/// opacity 0). The look a control had before its transitions landed is its entrance, which the page writes as the
/// control's <c>@starting-style</c>, so the browser plays the same entrance.
/// </summary>
internal static class Entrances
{
    public static ImmutableDictionary<Control, string> Settle(Visual root)
    {
        Hosting.PageClock.Begin(root);
        var moving = root.GetSelfAndVisualDescendants().OfType<Control>().Where(control => control.Transitions is { Count: > 0 }).ToList();
        if (moving.Count == 0)
        {
            return ImmutableDictionary<Control, string>.Empty;
        }

        var before = moving.ConvertAll(Look);
        moving.ConvertAll(control => control.SetValue(Animatable.TransitionsProperty, null, BindingPriority.Animation)).ForEach(lease => lease?.Dispose());
        var entrances = ImmutableDictionary.CreateBuilder<Control, string>();
        for (var index = 0; index < moving.Count; index++)
        {
            var (opacity, transform) = before[index];
            var (settledOpacity, settledTransform) = Look(moving[index]);
            var css = (opacity == settledOpacity ? string.Empty : $"opacity:{opacity};") + (transform == settledTransform ? string.Empty : $"transform:{transform};");
            if (css.Length > 0)
            {
                entrances[moving[index]] = css;
            }
        }

        return entrances.ToImmutable();
    }

    /// <summary>
    /// How a surface leaves: the look its theme gives the moving part while the surface is <c>:closing</c> (as Radix's
    /// <c>data-state="closed"</c> while tw-animate-css's <c>animate-out</c> plays), as CSS, with that part's transitions
    /// there, the longest of them and the first one's easing; nothing when the theme gives no closing look, so it leaves as it came.
    /// </summary>
    public static (string Css, string? Transitions, TimeSpan Longest, string Easing)? Leaving(Control surface, Control moving)
    {
        var classes = (IPseudoClasses)surface.Classes;
        var (restOpacity, restTransform) = Look(moving);
        try
        {
            Looks.Settled.Change(surface, () => classes.Add(":closing"));
            var (opacity, transform) = Look(moving);
            var css = (opacity == restOpacity ? string.Empty : $"opacity:{opacity};") + (transform == restTransform ? string.Empty : $"transform:{transform};");
            if (css.Length == 0)
            {
                return null;
            }

            var moves = moving.Transitions?.OfType<TransitionBase>().ToList() ?? [];
            var transitions = moving.Transitions is { Count: > 0 } list ? MotionStyles.TransitionList(list) : null;
            return (css, transitions, moves.Select(move => move.Duration + move.Delay).DefaultIfEmpty().Max(), CssValues.Ease(moves.FirstOrDefault()?.Easing));
        }
        finally
        {
            Looks.Settled.Change(surface, () => classes.Remove(":closing"));
        }
    }

    /// <summary>The value a property ends at once the animations running on it end: its value below their priority, or its default.</summary>
    public static double Opacity(Visual visual) =>
        visual.GetBaseValue(Visual.OpacityProperty) is { HasValue: true } settled ? settled.Value : Visual.OpacityProperty.GetDefaultValue(visual.GetType());

    /// <summary>
    /// The matrix a render transform ends at once the animations running on its parts end (Avalonia animates a scale's or
    /// a translation's properties, not the control's transform): each part's values below the animations' priority.
    /// </summary>
    public static Matrix? Matrix(ITransform? transform) => transform switch
    {
        null => null,
        TransformGroup group => group.Children.Aggregate(global::Avalonia.Matrix.Identity, (sum, child) => sum * (Matrix(child) ?? global::Avalonia.Matrix.Identity)),
        ScaleTransform scale => global::Avalonia.Matrix.CreateScale(Settled(scale, ScaleTransform.ScaleXProperty), Settled(scale, ScaleTransform.ScaleYProperty)),
        TranslateTransform translate => global::Avalonia.Matrix.CreateTranslation(Settled(translate, TranslateTransform.XProperty), Settled(translate, TranslateTransform.YProperty)),
        _ => transform.Value,
    };

    private static double Settled(AvaloniaObject owner, StyledProperty<double> property) =>
        owner.GetBaseValue(property) is { HasValue: true } settled ? settled.Value : property.GetDefaultValue(owner.GetType());

    private static (string Opacity, string Transform) Look(Control control) =>
        (CssValues.Num(control.Opacity), control.RenderTransform is { Value.IsIdentity: false } transform ? CssValues.Matrix(transform.Value) : "none");

    /// <summary>
    /// The rules of an entrance: the look a control is written from, more specific than its own rules and state rules, and,
    /// for a visitor who asks for less motion, no transition at all, so it shows (and a popover leaves) at once and at rest,
    /// as Tailwind's <c>motion-safe:</c> keeps tw-animate-css's entrances.
    /// </summary>
    public static string Rule(string name, string css) =>
        string.Create(CultureInfo.InvariantCulture, $"@starting-style{{.{name}.{name}.{name}.{name}{{{css}}}}}\n@media (prefers-reduced-motion:reduce){{.{name}.{name}.{name}.{name}{{transition:none!important}}}}\n");
}
