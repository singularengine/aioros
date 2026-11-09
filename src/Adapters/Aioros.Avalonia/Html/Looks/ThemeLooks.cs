using System.Globalization;
using System.Collections.Immutable;
using System.Numerics;
using System.Text;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

internal static class ThemeLooks
{
    /// <summary>The states of a pressable control: pointed at, pressed and focused from the keyboard, as Avalonia's pseudo-classes and their CSS ones.</summary>
    public static readonly (string Pseudo, string Selector)[] PressStates = [(":pointerover", "hover"), (":pressed", "active"), (":focus-visible", "focus-visible")];

    private const string Enabled = "not(:disabled)";

    private static ImmutableDictionary<string, Look> _looks = ImmutableDictionary<string, Look>.Empty.WithComparers(StringComparer.Ordinal);

    public static void Clear()
    {
        _looks = _looks.Clear();
    }

    public static Look Of(TemplatedControl control, string partName, IReadOnlyList<(string Pseudo, string Selector)> states)
    {
        var key = $"{control.GetType().FullName}.{string.Join('.', control.Classes)}.{control.IsEffectivelyEnabled}.{control.ActualThemeVariant}.{control.Tag?.GetType().FullName}={control.Tag}.{Own(control)}.{partName}.{string.Join(',', states.Select(state => state.Pseudo))}|{StyleContext.Own(control)}|{StyleContext.Of(control)}";
        return ImmutableInterlocked.GetOrAdd(ref _looks, key, _ => Resolve(control, partName, states));
    }

    private static string Own(TemplatedControl control) =>
        $"{BoxStyles.Edges(control.Padding)}|{BoxStyles.Edges(control.BorderThickness)}|{BoxStyles.Corners(control.CornerRadius)}|{Paint(control.Background)}|{Paint(control.BorderBrush)}|{Paint(control.Foreground)}";

    private static string Paint(IBrush? brush) => StyleContext.Text(brush);

    /// <summary>Whether the control wears Fluent's own theme for <paramref name="type"/>, which the writers follow as Avalonia's default.</summary>
    public static bool FromFluent(TemplatedControl control, Type type)
    {
        if (Application.Current?.Styles.OfType<global::Avalonia.Themes.Fluent.FluentTheme>().FirstOrDefault() is not { } fluent
            || !fluent.TryGetResource(type, control.ActualThemeVariant, out var standard))
        {
            return false;
        }

        var own = control.Theme ?? (control.TryFindResource(type, control.ActualThemeVariant, out var found) ? found as ControlTheme : null);
        return ReferenceEquals(own, standard);
    }

    public static void ExpandTemplates(Visual visual)
    {
        if (visual is Control control)
        {
            control.ApplyStyling();
            control.ApplyTemplate();
        }

        foreach (var child in visual.GetVisualChildren().ToList())
        {
            ExpandTemplates(child);
        }
    }

    private static Look Resolve(TemplatedControl control, string partName, IReadOnlyList<(string Pseudo, string Selector)> states)
    {
        ExpandTemplates(control);
        var part = control.GetVisualDescendants().OfType<Control>().FirstOrDefault(descendant => descendant.Name == partName);
        if (part is null)
        {
            return Look.None;
        }

        var transitions = TransitionCss(control, part);
        var held = control.GetVisualDescendants().Prepend(control).OfType<Animatable>()
            .Where(visual => visual.Transitions is not null)
            .Select(visual => visual.SetValue(Animatable.TransitionsProperty, null, BindingPriority.Animation))
            .ToList();
        try
        {
            var css = new StringBuilder();
            switch (part)
            {
                case ContentPresenter presenter:
                    BoxStyles.Fill(css, presenter.Background, presenter.BorderBrush, presenter.BorderThickness, presenter.CornerRadius, presenter.Padding);
                    css.Append(Unpadded(presenter.Padding));
                    if (CssValues.Brush(presenter.Foreground) is { } foreground)
                    {
                        css.Append(CultureInfo.InvariantCulture, $"color:{foreground};");
                    }

                    if (presenter.GetValue(TextBlock.LineHeightProperty) is var line and > 0)
                    {
                        css.Append(CultureInfo.InvariantCulture, $"line-height:{CssValues.Px(line)};");
                    }

                    break;
                case Border border:
                    BoxStyles.Fill(css, border.Background, border.BorderBrush, border.BorderThickness, border.CornerRadius, border.Padding);
                    css.Append(Unpadded(border.Padding));
                    break;
            }

            var rest = Snapshot(control, part);
            css.Append(rest["box-shadow"] == "none" ? string.Empty : $"box-shadow:{rest["box-shadow"]};");
            css.Append(Decorated(control, part, states) ? $"text-decoration:{rest["text-decoration"]};" : string.Empty);
            css.Append(rest["text-underline-offset"] == "auto" ? string.Empty : $"text-underline-offset:{rest["text-underline-offset"]};");
            css.Append(transitions is null ? string.Empty : $"transition:{transitions};");
            css.Append(control.IsSet(Control.FocusAdornerProperty) && control.FocusAdorner is null && states.Any(state => state.Pseudo == ":focus-visible") ? "outline:none;" : string.Empty);
            return new Look(css.ToString(), control.IsEffectivelyEnabled ? States(control, part, states, rest) : []);
        }
        finally
        {
            held.ForEach(lease => lease?.Dispose());
        }
    }

    /// <summary>A zero padding written as such, so the browser's own padding of a button never shows where the theme has none.</summary>
    private static string Unpadded(Thickness padding) => padding == default ? "padding:0;" : string.Empty;

    /// <summary>
    /// The rules of each combination of states, in the order of their specificity: one state, then two together, each
    /// guarded by <c>:not(:disabled)</c> so every rule weighs one more pseudo-class per state. A combination gets its own
    /// rule only where the rules of its parts would not give the look Avalonia gives it (a <c>:pointerover:pressed</c>
    /// style), so a pressed button keeps the pointed-at look a <c>:pressed</c> style does not change.
    /// </summary>
    private static List<LookState> States(TemplatedControl control, Control part, IReadOnlyList<(string Pseudo, string Selector)> states, Dictionary<string, string> rest)
    {
        var written = new List<(int Mask, Dictionary<string, string> Rule)>();
        var classes = (IPseudoClasses)control.Classes;
        foreach (var mask in Enumerable.Range(1, (1 << states.Count) - 1).OrderBy(mask => BitOperations.PopCount((uint)mask)).ThenBy(mask => mask))
        {
            var members = Enumerable.Range(0, states.Count).Where(index => (mask & (1 << index)) != 0).Select(index => states[index]).ToList();
            var added = members.Where(state => !control.Classes.Contains(state.Pseudo)).ToList();
            added.ForEach(state => classes.Add(state.Pseudo));
            var changed = Snapshot(control, part);
            added.ForEach(state => classes.Remove(state.Pseudo));
            var cascade = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (_, rule) in written.Where(entry => (entry.Mask & ~mask) == 0))
            {
                foreach (var (property, value) in rule)
                {
                    cascade[property] = value;
                }
            }

            var needed = changed.Where(entry => rest[entry.Key] != entry.Value || cascade.ContainsKey(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            if (needed.Count > 0 && !(needed.Count == cascade.Count && needed.All(entry => cascade.GetValueOrDefault(entry.Key) == entry.Value)))
            {
                written.Add((mask, needed));
            }
        }

        return [.. written.Select(entry => new LookState(
            Enabled + string.Concat(Enumerable.Range(0, states.Count).Where(index => (entry.Mask & (1 << index)) != 0).Select(index => ":" + states[index].Selector)),
            string.Concat(entry.Rule.Select(declaration => $"{declaration.Key}:{declaration.Value};"))))];
    }

    public static Dictionary<string, string> Snapshot(TemplatedControl control, Control part)
    {
        var (background, border, thickness, radius, foreground, shadow) = part switch
        {
            ContentPresenter presenter => (presenter.Background, presenter.BorderBrush, presenter.BorderThickness, presenter.CornerRadius, presenter.Foreground, presenter.BoxShadow),
            Border element => (element.Background, element.BorderBrush, element.BorderThickness, element.CornerRadius, control.Foreground, element.BoxShadow),
            _ => (null, null, default(Thickness), default(CornerRadius), null, default(BoxShadows)),
        };
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"] = CssValues.Paint(background) ?? "transparent",
            ["border-color"] = CssValues.Brush(border) ?? "transparent",
            ["border-width"] = BoxStyles.Edges(thickness),
            ["border-radius"] = BoxStyles.Corners(radius),
            ["color"] = CssValues.Brush(foreground) ?? "inherit",
            ["box-shadow"] = shadow.Count == 0 ? "none" : BoxStyles.Shadow(shadow),
            ["text-decoration"] = Decoration(part.GetValue(TextBlock.TextDecorationsProperty) ?? control.GetValue(TextBlock.TextDecorationsProperty)),
            ["text-underline-offset"] = Underlines.Offset(part, part.GetValue(TextBlock.TextDecorationsProperty) ?? control.GetValue(TextBlock.TextDecorationsProperty)),
            ["opacity"] = CssValues.Num(control.Opacity),
            ["transform"] = control.RenderTransform is { Value.IsIdentity: false } transform ? CssValues.Transform(transform) : "none",
        };
    }

    /// <summary>Whether the text's decoration is written at rest: the theme decorates it, or one of its states does.</summary>
    private static bool Decorated(TemplatedControl control, Control part, IReadOnlyList<(string Pseudo, string Selector)> states)
    {
        if (Snapshot(control, part)["text-decoration"] != "none")
        {
            return true;
        }

        var classes = (IPseudoClasses)control.Classes;
        return states.Any(state =>
        {
            if (control.Classes.Contains(state.Pseudo))
            {
                return false;
            }

            classes.Add(state.Pseudo);
            var decorated = Snapshot(control, part)["text-decoration"] != "none";
            classes.Remove(state.Pseudo);
            return decorated;
        });
    }

    private static string Decoration(TextDecorationCollection? decorations)
    {
        var lines = decorations?.Select(decoration => decoration.Location switch
        {
            TextDecorationLocation.Underline => "underline",
            TextDecorationLocation.Strikethrough => "line-through",
            TextDecorationLocation.Overline => "overline",
            _ => null,
        }).OfType<string>().Distinct().ToList();
        return lines is { Count: > 0 } ? string.Join(' ', lines) : "none";
    }

    /// <summary>The CSS transitions of the control and of the part whose look its element takes, or null when the part has none.</summary>
    private static string? TransitionCss(TemplatedControl control, Control part) =>
        part.Transitions is { Count: > 0 } ? MotionStyles.TransitionList((control.Transitions ?? Enumerable.Empty<ITransition>()).Concat(part.Transitions)) : null;
}
