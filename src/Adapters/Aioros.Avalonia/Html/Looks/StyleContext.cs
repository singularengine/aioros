using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// What a control's look reads from its place: the state of its ancestors that the selectors of styles can match (types,
/// names, classes and pseudo-classes, the properties the styles in scope match, their position where a style counts
/// children) and the styles and resources they hold, which reach the control as Avalonia matches selectors and resolves
/// dynamic resources up its ancestors. Styles and resources are described by their content, so a page rendered again
/// gives the same description and a cache keyed on it does not grow with every render. <see cref="Own"/> describes what
/// the control fixes itself outside styles, which wins over them in Avalonia's value priorities.
/// </summary>
internal static partial class StyleContext
{
    private sealed record Scope(ImmutableHashSet<string> Properties, bool Counts);

    private sealed record Reach(ImmutableHashSet<string> All, ImmutableHashSet<string> Triggered);

    private static readonly ConditionalWeakTable<ControlTheme, string> Themes = [];

    private static readonly FrozenSet<AvaloniaProperty> Unread = new AvaloniaProperty[]
    {
        ContentControl.ContentProperty, ContentControl.ContentTemplateProperty, StyledElement.DataContextProperty, StyledElement.NameProperty,
        StyledElement.ThemeProperty, Control.TagProperty, TemplatedControl.TemplateProperty, Button.CommandProperty, Button.CommandParameterProperty,
    }.ToFrozenSet();

    private static readonly ConditionalWeakTable<ControlTheme, Reach> ThemeSetters = [];

    private static Scope? _application;

    private static Reach? _applicationSetters;

    /// <summary>
    /// What a templated control fixes itself outside styles: its explicit theme described by content, its template's
    /// realized parts, its own local values (bindings included) for a property some style in scope sets (its theme's, its
    /// ancestors' or the application's), since a local value wins over every style, and its parts' values for a property
    /// some style sets but none sets under a pseudo-class, which keep the template's value in every state. A value no style
    /// sets (a bound <c>IsChecked</c>, which the look reads through pseudo-classes) is left out, as is a state's value.
    /// </summary>
    public static string Own(StyledElement control)
    {
        var reach = Styled(control);
        var text = new StringBuilder();
        text.Append("theme{").Append(Theme(control.Theme)).Append('}');
        Values(text, control, reach.All, static priority => priority == BindingPriority.LocalValue);
        if (control is Visual visual)
        {
            var steady = reach.All.Except(reach.Triggered);
            foreach (var part in visual.GetVisualDescendants().OfType<StyledElement>().Where(part => ReferenceEquals(part.TemplatedParent, control)))
            {
                text.Append('<').Append(part.GetType().FullName).Append('#').Append(part.Name).Append('.').AppendJoin('.', part.Classes.Where(static name => !name.StartsWith(':')));
                Values(text, part, steady, static priority => priority is BindingPriority.LocalValue or BindingPriority.Template);
                text.Append('>');
            }
        }

        return text.ToString();
    }

    /// <summary>The properties the styles that can reach a control set, and those set under a pseudo-class: its theme's (explicit, or found by its type up its base types), its own and its ancestors' styles, and the application's.</summary>
    private static Reach Styled(StyledElement control)
    {
        var theme = control.Theme;
        for (var type = control.GetType(); theme is null && type is not null; type = type.BaseType)
        {
            theme = control.TryFindResource(type, out var found) ? found as ControlTheme : null;
        }

        var all = new HashSet<string>(StringComparer.Ordinal);
        var triggered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reach in (Reach[])[Set(theme), Volatile.Read(ref _applicationSetters) ?? ApplicationSetters()])
        {
            all.UnionWith(reach.All);
            triggered.UnionWith(reach.Triggered);
        }

        for (var element = (StyledElement?)control; element is not null; element = Next(element))
        {
            Names(element.Styles, all, triggered, false);
        }

        return new([.. all], [.. triggered]);
    }

    private static Reach ApplicationSetters()
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        var triggered = new HashSet<string>(StringComparer.Ordinal);
        if (global::Avalonia.Application.Current is { } application)
        {
            Names(application.Styles, all, triggered, false);
        }

        var known = new Reach([.. all], [.. triggered]);
        Volatile.Write(ref _applicationSetters, known);
        return known;
    }

    private static Reach Set(ControlTheme? theme) =>
        theme is null ? new([], []) : ThemeSetters.GetValue(theme, static one =>
        {
            var based = Set(one.BasedOn);
            var all = new HashSet<string>(based.All, StringComparer.Ordinal);
            var triggered = new HashSet<string>(based.Triggered, StringComparer.Ordinal);
            all.UnionWith(one.Setters.OfType<Setter>().Select(setter => setter.Property?.Name).OfType<string>());
            Names(one.Children, all, triggered, false);
            return new([.. all], [.. triggered]);
        });

    private static void Names(IEnumerable<IStyle> styles, HashSet<string> all, HashSet<string> triggered, bool underState)
    {
        foreach (var style in styles)
        {
            switch (style)
            {
                case Styles nested:
                    Names(nested, all, triggered, underState);
                    break;
                case Style one:
                    var state = underState || (one.Selector?.ToString() ?? string.Empty).Replace("/template/", string.Empty, StringComparison.Ordinal).Contains(':', StringComparison.Ordinal);
                    var names = one.Setters.OfType<Setter>().Select(setter => setter.Property?.Name).OfType<string>().ToList();
                    all.UnionWith(names);
                    if (state)
                    {
                        triggered.UnionWith(names);
                    }

                    Names(one.Children, all, triggered, state);
                    break;
                case ContainerQuery query:
                    Names(query.Children, all, triggered, underState);
                    break;
            }
        }
    }

    /// <summary>A control theme described by content (its target, the theme it is based on, its setters and styles), read once per theme.</summary>
    public static string Theme(ControlTheme? theme) =>
        theme is null ? string.Empty : Themes.GetValue(theme, static one =>
        {
            var text = new StringBuilder();
            text.Append(one.TargetType?.FullName).Append(":(").Append(Theme(one.BasedOn)).Append(')');
            Setters(text, one.Setters);
            Describe(text, one.Children);
            return text.ToString();
        });

    private static void Values(StringBuilder text, AvaloniaObject element, ImmutableHashSet<string> styled, Func<BindingPriority, bool> kept)
    {
        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(element).Where(property => styled.Contains(property.Name) && !Unread.Contains(property) && element.IsSet(property) && kept(element.GetDiagnostic(property).Priority)))
        {
            text.Append('[').Append(property.Name).Append('=').Append(Text(element.GetValue(property))).Append(']');
        }
    }

    /// <summary>A value as a look reads it: a brush by its colors, transitions by what they animate and how, a transform by its matrix, a theme by its content.</summary>
    public static string Text(object? value) => value switch
    {
        null => string.Empty,
        ISolidColorBrush brush => $"{brush.Color}/{brush.Opacity.ToString(CultureInfo.InvariantCulture)}",
        IGradientBrush gradient => $"{gradient.GetType().Name}/{gradient.Opacity.ToString(CultureInfo.InvariantCulture)}/{gradient.SpreadMethod}:{string.Join(',', gradient.GradientStops.Select(stop => $"{stop.Color}@{stop.Offset.ToString(CultureInfo.InvariantCulture)}"))}:{Geometry(gradient)}",
        Transitions transitions => string.Join(',', transitions.Select(transition => transition is TransitionBase timed
            ? $"{timed.GetType().Name}:{timed.Property?.Name}:{timed.Duration}:{timed.Delay}:{Text(timed.Easing)}"
            : transition.GetType().FullName)),
        Transform transform => transform.Value.ToString(),
        ControlTheme theme => Theme(theme),
        ITemplate template => "template:" + template.GetType().FullName,
        IEnumerable<IStyle> styles => Styles(styles),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string Geometry(IGradientBrush gradient) => gradient switch
    {
        ILinearGradientBrush linear => $"{linear.StartPoint}>{linear.EndPoint}",
        IRadialGradientBrush radial => $"{radial.Center}/{radial.GradientOrigin}/{radial.RadiusX}/{radial.RadiusY}",
        IConicGradientBrush conic => $"{conic.Center}/{conic.Angle.ToString(CultureInfo.InvariantCulture)}",
        _ => string.Empty,
    };

    private static string Styles(IEnumerable<IStyle> styles)
    {
        var text = new StringBuilder();
        Describe(text, styles);
        return text.ToString();
    }

    private static void Setters(StringBuilder text, IEnumerable<SetterBase> setters)
    {
        foreach (var setter in setters.OfType<Setter>())
        {
            text.Append(setter.Property?.Name).Append(':').Append(Text(setter.Value)).Append(';');
        }
    }

    /// <summary>Forgets what the application's styles match on, when a style changes.</summary>
    public static void Clear()
    {
        Volatile.Write(ref _application, null);
        Volatile.Write(ref _applicationSetters, null);
    }

    public static string Of(Control control)
    {
        var ancestors = new List<StyledElement>();
        for (var element = Next(control); element is not null; element = Next(element))
        {
            ancestors.Add(element);
        }

        var text = new StringBuilder();
        foreach (var element in ancestors)
        {
            Describe(text, element.Styles);
        }

        var local = Read(text.ToString());
        var application = Application();
        var properties = application.Properties.Union(local.Properties);
        var counts = application.Counts || local.Counts;
        foreach (var element in ancestors)
        {
            text.Append('/').Append(element.GetType().FullName).Append('#').Append(element.Name).Append('.').AppendJoin('.', element.Classes);
            foreach (var name in properties)
            {
                if (AvaloniaPropertyRegistry.Instance.FindRegistered(element, name) is { } property)
                {
                    text.Append('[').Append(name).Append('=').Append(Value(element.GetValue(property))).Append(']');
                }
            }

            if (counts && element.Parent is Panel panel && element is Control child)
            {
                text.Append('@').Append(panel.Children.IndexOf(child).ToString(CultureInfo.InvariantCulture));
            }

            if (element is IResourceNode { HasResources: true })
            {
                text.Append('{');
                Describe(text, element.Resources);
                text.Append('}');
            }
        }

        return text.ToString();
    }

    private static StyledElement? Next(StyledElement element) => element.Parent as StyledElement ?? (element as Control)?.TemplatedParent as StyledElement;

    private static string Value(object? value) => value switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>What the application's styles match on, read once.</summary>
    private static Scope Application()
    {
        if (Volatile.Read(ref _application) is { } known)
        {
            return known;
        }

        var text = new StringBuilder();
        if (global::Avalonia.Application.Current is { } application)
        {
            Describe(text, application.Styles);
        }

        var scope = Read(text.ToString());
        Volatile.Write(ref _application, scope);
        return scope;
    }

    private static Scope Read(string styles) =>
        new([.. PropertyMatch().Matches(styles).Select(match => match.Groups[1].Value)], styles.Contains(":nth-", StringComparison.Ordinal));

    private static void Describe(StringBuilder text, IEnumerable<IStyle> styles)
    {
        foreach (var style in styles)
        {
            switch (style)
            {
                case Styles nested:
                    Describe(text, nested);
                    break;
                case Style one:
                    text.Append(one.Selector?.ToString()).Append('{');
                    Setters(text, one.Setters);
                    text.Append('}');
                    Describe(text, one.Children);
                    break;
                case ContainerQuery query:
                    text.Append("@container ").Append(query.Name).Append(' ').Append(query.Query?.ToString()).Append('{');
                    Describe(text, query.Children);
                    text.Append('}');
                    break;
            }
        }
    }

    private static void Describe(StringBuilder text, IResourceDictionary resources)
    {
        foreach (var key in resources.Keys.Select(key => key.ToString() ?? string.Empty).Order(StringComparer.Ordinal))
        {
            var value = resources.TryGetResource(key, null, out var found) ? found : null;
            text.Append(key).Append('=').Append(Text(value)).Append(';');
        }

        foreach (var (variant, dictionary) in resources.ThemeDictionaries.OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal))
        {
            text.Append('(').Append(variant).Append(':');
            if (dictionary is IResourceDictionary inner)
            {
                Describe(text, inner);
            }

            text.Append(')');
        }

        foreach (var merged in resources.MergedDictionaries)
        {
            text.Append('+');
            switch (merged)
            {
                case IResourceDictionary inner:
                    Describe(text, inner);
                    break;
                case global::Avalonia.Markup.Xaml.Styling.ResourceInclude include:
                    text.Append(include.Source);
                    break;
                default:
                    text.Append(merged.GetType().FullName);
                    break;
            }
        }
    }

    [GeneratedRegex(@"\[([A-Za-z_][\w.]*)=")]
    private static partial Regex PropertyMatch();
}
