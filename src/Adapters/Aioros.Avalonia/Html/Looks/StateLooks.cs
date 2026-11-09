using System.Text;
using System.Numerics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>An Avalonia pseudo-class and the CSS selector fragment that matches the same state on the web.</summary>
internal readonly record struct WebState(string Pseudo, string Selector);

/// <summary>The look of a control at rest and the rules of each combination of its states, as property values.</summary>
internal sealed record StateLook(IReadOnlyDictionary<string, string> Rest, IReadOnlyList<(string Selector, IReadOnlyDictionary<string, string> Rule)> Rules)
{
    /// <summary>The rules as CSS rules for the elements <paramref name="target"/> names after each selector (empty for the element itself).</summary>
    public string Css(string name, string target, Func<string, string>? map = null) =>
        string.Concat(Rules.Select(rule => $".{name}{rule.Selector}{target}{{{string.Concat(rule.Rule.Select(declaration => $"{map?.Invoke(declaration.Key) ?? declaration.Key}:{declaration.Value};"))}}}"));
}

/// <summary>
/// Resolves what a control's theme shows in each combination of its states, as Avalonia's style resolution gives it:
/// the state pseudo-classes are cleared for the rest look (a checked toggle's rest is its unchecked look, with
/// <c>:unchecked</c>), then each combination is added in the order of its specificity, with the transitions held, and a
/// combination gets its own rule only where the rules of its parts would not give its look.
/// </summary>
internal static class StateLooks
{
    private static readonly string[] Checks = [":checked", ":indeterminate"];

    public static StateLook Resolve(TemplatedControl control, IReadOnlyList<WebState> states, Func<Dictionary<string, string>> snapshot)
    {
        ThemeLooks.ExpandTemplates(control);
        var transitions = control.GetVisualDescendants().Prepend(control).OfType<Animatable>()
            .Where(visual => visual.Transitions is not null)
            .Select(visual => visual.SetValue(Animatable.TransitionsProperty, null, BindingPriority.Animation))
            .ToList();
        var classes = (IPseudoClasses)control.Classes;
        var toggled = control is ToggleButton || states.Any(state => Checks.Contains(state.Pseudo));
        var before = Held(control, states, toggled);
        var held = new CheckedValue(control);
        try
        {
            before.ForEach(pseudo => classes.Remove(pseudo));
            held.Set(false);
            if (toggled)
            {
                classes.Add(":unchecked");
            }

            var rest = snapshot();
            var written = new List<(int Mask, Dictionary<string, string> Rule)>();
            foreach (var mask in Enumerable.Range(1, (1 << states.Count) - 1).OrderBy(mask => BitOperations.PopCount((uint)mask)).ThenBy(mask => mask))
            {
                var members = Members(states, mask);
                var checks = members.Any(state => Checks.Contains(state.Pseudo));
                if (members.Count(state => Checks.Contains(state.Pseudo)) > 1)
                {
                    continue;
                }

                var changed = Combination(classes, members, toggled && checks, held, snapshot);
                if (toggled)
                {
                    classes.Add(":unchecked");
                }

                var cascade = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (_, rule) in written.Where(entry => (entry.Mask & ~mask) == 0))
                {
                    foreach (var (property, value) in rule)
                    {
                        cascade[property] = value;
                    }
                }

                var needed = changed.Where(entry => rest.GetValueOrDefault(entry.Key) != entry.Value || cascade.ContainsKey(entry.Key))
                    .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                if (needed.Count > 0 && !(needed.Count == cascade.Count && needed.All(entry => cascade.GetValueOrDefault(entry.Key) == entry.Value)))
                {
                    written.Add((mask, needed));
                }
            }

            return new StateLook(rest, [.. written.Select(entry => (string.Concat(Members(states, entry.Mask).Select(state => state.Selector)), (IReadOnlyDictionary<string, string>)entry.Rule))]);
        }
        finally
        {
            held.Dispose();
            classes.Remove(":unchecked");
            before.ForEach(pseudo => classes.Add(pseudo));
            transitions.ForEach(lease => lease?.Dispose());
        }
    }

    /// <summary>The snapshot of one combination of the states: their pseudo-classes added and the toggle's value held for them, then both taken back.</summary>
    private static Dictionary<string, string> Combination(IPseudoClasses classes, List<WebState> members, bool checks, CheckedValue held, Func<Dictionary<string, string>> snapshot)
    {
        held.Set(members.Exists(static state => state.Pseudo == ":checked") ? true : members.Exists(static state => state.Pseudo == ":indeterminate") ? null : false);
        members.ForEach(state => classes.Add(state.Pseudo));
        if (checks)
        {
            classes.Remove(":unchecked");
        }

        var changed = snapshot();
        members.ForEach(state => classes.Remove(state.Pseudo));
        held.Set(false);
        return changed;
    }

    /// <summary>
    /// A control's own style (<paramref name="style"/>: its box and font) and, where a style changes it with the control's
    /// states (<c>CheckBox:checked</c> setting its <c>FontWeight</c> or <c>Foreground</c>), that style at rest and the rules
    /// of each state for its writer to write; a control in a state is then written as it is at rest. A declaration the rest
    /// writes and a state does not (an opacity back at its default) is that state's <c>unset</c>.
    /// </summary>
    public static (StringBuilder Style, StateLook? Own) OwnStyle(TemplatedControl control, IReadOnlyList<WebState> states, Func<StringBuilder> style)
    {
        HashSet<string>? rest = null;
        var own = Resolve(control, states, () =>
        {
            var declarations = Declarations(style());
            if (rest is null)
            {
                rest = [.. declarations.Keys];
            }
            else
            {
                rest.Where(name => !declarations.ContainsKey(name)).ToList().ForEach(name => declarations[name] = "unset");
            }

            return declarations;
        });
        return own.Rules.Count == 0 ? (style(), null) : (StateLooks.AtRest(control, states, style), own);
    }

    private static Dictionary<string, string> Declarations(StringBuilder style)
    {
        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var declaration in style.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = declaration.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                declarations[declaration[..colon]] = declaration[(colon + 1)..];
            }
        }

        return declarations;
    }

    /// <summary>What <paramref name="read"/> gives with the control at rest: its states' pseudo-classes cleared as <see cref="Resolve"/> clears them for the rest look, then put back.</summary>
    public static T AtRest<T>(TemplatedControl control, IReadOnlyList<WebState> states, Func<T> read)
    {
        var classes = (IPseudoClasses)control.Classes;
        var toggled = control is ToggleButton || states.Any(state => Checks.Contains(state.Pseudo));
        var before = Held(control, states, toggled);
        var held = new CheckedValue(control);
        try
        {
            before.ForEach(pseudo => classes.Remove(pseudo));
            held.Set(false);
            if (toggled)
            {
                classes.Add(":unchecked");
            }

            return read();
        }
        finally
        {
            held.Dispose();
            classes.Remove(":unchecked");
            before.ForEach(pseudo => classes.Add(pseudo));
        }
    }

    /// <summary>
    /// The pseudo-classes the control holds that its rest must not: its states', and for a control that checks, every check
    /// (an indeterminate box among them, whose glyph Avalonia draws for a null value even when it is not a state written).
    /// </summary>
    private static List<string> Held(TemplatedControl control, IReadOnlyList<WebState> states, bool toggled) =>
        [.. states.Select(state => state.Pseudo).Concat(toggled ? Checks : []).Append(":unchecked").Distinct(StringComparer.Ordinal).Where(control.Classes.Contains)];

    private static List<WebState> Members(IReadOnlyList<WebState> states, int mask) =>
        [.. Enumerable.Range(0, states.Count).Where(index => (mask & (1 << index)) != 0).Select(index => states[index])];

    /// <summary>Lays the control out on its own at its desired size, so its template's parts have the bounds Avalonia gives them.</summary>
    public static void LayOut(Control control)
    {
        foreach (var part in control.GetVisualDescendants().OfType<Layoutable>().Prepend(control))
        {
            part.InvalidateMeasure();
        }

        control.Measure(Size.Infinity);
        var size = control.DesiredSize;
        control.Arrange(new Rect(size));
    }
}

/// <summary>
/// A toggle's value held for a state while its looks are read, so a style that selects it by its value
/// (<c>CheckBox[IsChecked=True]</c>) applies in its checked state as one by <c>:checked</c> does; put back when disposed.
/// </summary>
internal sealed class CheckedValue(TemplatedControl control) : IDisposable
{
    private IDisposable? _lease;

    private readonly bool _selected = control is ToggleButton and not RadioButton && SelectsValue(control);

    private bool? _held = control is ToggleButton { IsChecked: var current } ? !current : null;

    public void Set(bool? value)
    {
        if (!_selected || (_lease is not null && _held == value))
        {
            return;
        }

        _lease?.Dispose();
        _held = value;
        _lease = Plans.LocalBindings.Override(control, ToggleButton.IsCheckedProperty, value);
    }

    public void Dispose()
    {
        _lease?.Dispose();
        _lease = null;
    }

    /// <summary>
    /// Whether a style the control takes selects it by its value: one of its page, of the application or of its control
    /// theme whose selector reads <c>IsChecked</c>; a toggle no such style reads keeps its value (a radio's group, a row's flag).
    /// </summary>
    private static bool SelectsValue(TemplatedControl control)
    {
        var styles = new List<IStyle>();
        for (StyledElement? element = control; element is not null; element = element.Parent as StyledElement)
        {
            styles.AddRange(element.Styles);
        }

        styles.AddRange(Application.Current?.Styles ?? []);
        if (control.Theme is { } theme)
        {
            styles.Add(theme);
        }
        else if (control.TryFindResource(control.StyleKey, control.ActualThemeVariant, out var found) && found is ControlTheme own)
        {
            styles.Add(own);
        }

        return Reads(styles, 0);
    }

    private static bool Reads(IEnumerable<IStyle> styles, int depth) => depth < 8 && styles.Any(style => style switch
    {
        StyleBase { } owned when owned is Style { Selector: { } selector } && selector.ToString().Contains("[IsChecked", StringComparison.Ordinal) => true,
        StyleBase owned => Reads(owned.Children, depth + 1),
        Styles nested => Reads(nested, depth + 1),
        _ => false,
    });
}

