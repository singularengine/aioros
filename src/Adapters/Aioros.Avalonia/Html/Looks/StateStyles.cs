using System.Text;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Aioros.Avalonia.Controls;

namespace Aioros.Avalonia.Html.Looks;

internal static class StateStyles
{
    private static readonly (string Pseudo, string Selector)[] States = [(":pointerover", ":hover"), (":pressed", ":active"), (":current", "[aria-current=\"page\"]"), (":focus-within", ":focus-within"), (":focus-visible", ":focus-visible")];

    public static void Register(HtmlContext context, Control container)
    {
        context.ForgetStateClasses(container);
        if (container.Classes.All(name => name.StartsWith(':')) || !HasStateStyles(container))
        {
            return;
        }

        var elements = container.GetLogicalDescendants().OfType<Control>().Prepend(container).ToList();
        var still = elements.Select(element => element.SetValue(Animatable.TransitionsProperty, null, global::Avalonia.Data.BindingPriority.Animation)).ToList();
        try
        {
            Register(context, container, elements, States);
        }
        finally
        {
            still.ForEach(release => release?.Dispose());
        }
    }

    /// <summary>
    /// The looks a templated control's own theme gives its template's parts in its states (a non-Fluent date picker's
    /// field and icon while it is pointed at, Semi.Avalonia's), as rules on its element for the parts written inside it;
    /// <paramref name="states"/> names other states and the selectors they are written under (a copying button's
    /// <c>:copied</c> under <c>[data-copied]</c>).
    /// </summary>
    public static void RegisterTemplate(HtmlContext context, TemplatedControl control, (string Pseudo, string Selector)[]? states = null)
    {
        context.ForgetStateClasses(control);
        var elements = control.GetVisualDescendants().OfType<Control>().Prepend(control).ToList();
        var still = elements.Select(element => element.SetValue(Animatable.TransitionsProperty, null, global::Avalonia.Data.BindingPriority.Animation)).ToList();
        try
        {
            Register(context, control, elements, states ?? States);
        }
        finally
        {
            still.ForEach(release => release?.Dispose());
        }
    }

    /// <summary>
    /// The look a control's own class gives it and its parts (a code slot's <c>active</c>), as a rule under
    /// <paramref name="selector"/>, which the framework's script sets on its element.
    /// </summary>
    public static void RegisterClass(HtmlContext context, Control control, string name, string selector)
    {
        if (control.Classes.Contains(name))
        {
            return;
        }

        var elements = control.GetVisualDescendants().OfType<Control>().Prepend(control).ToList();
        var still = elements.Select(element => element.SetValue(Animatable.TransitionsProperty, null, global::Avalonia.Data.BindingPriority.Animation)).ToList();
        try
        {
            Register(context, control, elements, [(name, selector)], (on, state) =>
            {
                if (on)
                {
                    control.Classes.Add(state);
                }
                else
                {
                    control.Classes.Remove(state);
                }
            });
        }
        finally
        {
            still.ForEach(release => release?.Dispose());
        }
    }

    private static void Register(HtmlContext context, Control container, List<Control> elements, (string Pseudo, string Selector)[] states) =>
        Register(context, container, elements, states, (on, pseudo) =>
        {
            var classes = (IPseudoClasses)container.Classes;
            if (on)
            {
                classes.Add(pseudo);
            }
            else
            {
                classes.Remove(pseudo);
            }
        });

    private static void Register(HtmlContext context, Control container, List<Control> elements, (string Pseudo, string Selector)[] states, Action<bool, string> toggle)
    {
        var normal = elements.Select(Snapshot).ToList();
        var rules = new StringBuilder();
        var parts = new Dictionary<Control, List<string>>();
        foreach (var (pseudo, selector) in states)
        {
            toggle(true, pseudo);
            var changed = elements.Select(Snapshot).ToList();
            toggle(false, pseudo);
            for (var index = 0; index < elements.Count; index++)
            {
                var difference = Difference(normal[index], changed[index]);
                if (difference.Length == 0)
                {
                    continue;
                }

                if (index == 0)
                {
                    rules.Append(Rule(selector, "{0}", difference));
                }
                else
                {
                    var descendant = "d" + CssValues.Hash(selector + difference);
                    if (!parts.TryGetValue(elements[index], out var names))
                    {
                        names = [];
                        parts[elements[index]] = names;
                    }

                    names.Add(descendant);
                    rules.Append(Rule(selector, "{0} ." + descendant, difference));
                }
            }
        }

        foreach (var (part, names) in parts)
        {
            context.AddStateClass(part, string.Join(' ', names), container);
        }

        if (rules.Length == 0)
        {
            return;
        }

        var name = "h" + CssValues.Hash(rules.ToString());
        context.Sheet.AddRule(name, rules.ToString().Replace("{0}", name, StringComparison.Ordinal) + "\n");
        context.AddStateClass(container, name, container);
    }

    private static string Rule(string selector, string target, string difference) =>
        "." + target.Replace("{0}", "{0}" + selector, StringComparison.Ordinal) + "{" + difference + "}";

    /// <summary>
    /// Whether the control's own theme styles its template's parts in a state written on the web (a library card whose
    /// floating actions show while it is pointed at, Aioros.Cosmo's code block), so its parts' looks are registered as
    /// <see cref="RegisterTemplate"/> registers a themed picker's.
    /// </summary>
    public static bool StylesTemplate(TemplatedControl control) =>
        (control.Theme ?? (control.TryFindResource(control.GetType(), control.ActualThemeVariant, out var found) ? found as ControlTheme : null)) is { } theme
        && Flatten(theme.Children).OfType<Style>().Select(style => style.Selector?.ToString() ?? string.Empty)
            .Any(selector => selector.Contains("/template/", StringComparison.Ordinal) && States.Any(state => selector.Contains(state.Pseudo, StringComparison.Ordinal)));

    /// <summary>Whether a style targets this control in the given state (for example <c>:current</c>).</summary>
    public static bool Styles(Control control, string pseudo) => Selectors(control).Any(selector => selector.Contains(pseudo, StringComparison.Ordinal));

    private static bool HasStateStyles(Control control) => Selectors(control).Any(selector => States.Any(state => selector.Contains(state.Pseudo, StringComparison.Ordinal)));

    private static IEnumerable<string> Selectors(Control control)
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

        var names = control.Classes.Where(name => !name.StartsWith(':')).Select(name => "." + name).ToList();
        return Flatten(sources).OfType<Style>().Select(style => style.Selector?.ToString() ?? string.Empty)
            .Where(selector => names.Any(name => selector.Contains(name + ":", StringComparison.Ordinal)));
    }

    private static IEnumerable<IStyle> Flatten(IEnumerable<IStyle> styles) =>
        styles.SelectMany(style => style is Styles nested ? Flatten(nested) : [style]);

    private static Dictionary<string, string> Snapshot(Control control)
    {
        var transform = Base(control, Visual.RenderTransformProperty);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["opacity"] = CssValues.Num(Base(control, Visual.OpacityProperty)),
            ["transform"] = transform is null ? "none" : CssValues.Transform(transform),
        };
        var background = control switch
        {
            Border => Base(control, Border.BackgroundProperty),
            Panel => Base(control, Panel.BackgroundProperty),
            TemplatedControl => Base(control, TemplatedControl.BackgroundProperty),
            TextBlock => Base(control, TextBlock.BackgroundProperty),
            _ => (IBrush?)null,
        };
        values["background"] = CssValues.Paint(background) ?? "transparent";
        if (control is Border)
        {
            values["border-color"] = CssValues.Brush(Base(control, Border.BorderBrushProperty)) ?? "transparent";
            var shadow = Base(control, Border.BoxShadowProperty);
            values["box-shadow"] = shadow.Count == 0 ? "none" : BoxStyles.Shadow(shadow);
        }

        if (control is TextBlock or Icon or TemplatedControl)
        {
            values["color"] = CssValues.Brush(Base(control, TextElement.ForegroundProperty)) ?? "inherit";
        }

        return values;
    }

    private static T Base<T>(AvaloniaObject owner, StyledProperty<T> property) =>
        owner.GetBaseValue(property) is T value ? value : owner.GetValue(property);

    private static string Difference(Dictionary<string, string> normal, Dictionary<string, string> changed)
    {
        var css = new StringBuilder();
        foreach (var property in (string[])["background", "border-color", "color", "opacity", "transform", "box-shadow"])
        {
            if (changed.TryGetValue(property, out var value) && normal.GetValueOrDefault(property) != value)
            {
                css.Append(property).Append(':').Append(value).Append("!important;");
            }
        }

        return css.ToString();
    }
}
