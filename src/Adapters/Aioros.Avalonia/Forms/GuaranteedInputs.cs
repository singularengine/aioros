using System.Reflection;
using Aioros.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Forms;

/// <summary>
/// Whether an input's value may be the page's own state (decision "Guaranteed scope, closed per control type"): no code
/// outside Avalonia and the ecosystem could observe it (a code-behind beside it, a library's attached property, a library
/// control reading it by name); and whether the guarantee covers it: its theme is a guaranteed one (Fluent, Aioros.Cosmo)
/// and no style outside them changes with its state, so its type's domain test closes it, where any other input is proven
/// by its page's verification. Found without setting anything on any control.
/// </summary>
internal static class GuaranteedInputs
{
    private static readonly string[] Own = ["Aioros.Avalonia", "Aioros.Markup"];

    private static readonly string[] Families = ["Aioros.Cosmo", "Aioros.Pictor", "Aioros.Graphs"];

    public static bool Unobserved(Control control, IReadOnlySet<string> readByLibraries) =>
        !Observed(control) && (control.Name is not { } name || !readByLibraries.Contains(name));

    public static bool Covers(Control control) => Themed(control) && !StateStyled(control);

    /// <summary>The names of the inputs a control outside the guarantee reads by binding (<c>{Binding #ratio.Value}</c>).</summary>
    public static HashSet<string> ReadByLibraries(Control root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in root.GetVisualDescendants().Concat(root.GetLogicalDescendants()).Prepend(root).OfType<AvaloniaObject>().Where(static element => Foreign(element.GetType())).Distinct())
        {
            foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(element))
            {
                if (BindingOperations.GetBindingExpressionBase(element, property) is IDescription { Description: ['#', .. var path] })
                {
                    names.Add(path.Split('.')[0]);
                }
            }
        }

        return names;
    }

    /// <summary>Whether an assembly's types are outside Avalonia and the ecosystem: an app's own or a library's.</summary>
    public static bool Foreign(Type type) =>
        type.Assembly.GetName().Name is not { } name || !name.StartsWith("Avalonia", StringComparison.Ordinal) && !Own.Contains(name) && !Families.Any(family => name == family || name.StartsWith(family + ".", StringComparison.Ordinal));

    private static bool Themed(Control control)
    {
        var theme = control.Theme ?? (control.TryFindResource(control.StyleKey, control.ActualThemeVariant, out var found) ? found as ControlTheme : null);
        return theme is not null && Application.Current is { } application
            && application.Styles.OfType<Styles>().Where(Guaranteed).Any(styles => styles.TryGetResource(control.StyleKey, control.ActualThemeVariant, out var own) && ReferenceEquals(own, theme) || Holds(styles.Resources, theme));
    }

    private static bool Guaranteed(IStyle style) => style is FluentTheme || style.GetType().Assembly.GetName().Name == "Aioros.Cosmo";

    private static bool Holds(IResourceProvider? provider, ControlTheme theme) => provider switch
    {
        ResourceInclude include => Holds(include.Loaded, theme),
        IResourceDictionary dictionary => dictionary.Values.Any(value => ReferenceEquals(value, theme))
            || dictionary.MergedDictionaries.Any(merged => Holds(merged, theme))
            || dictionary.ThemeDictionaries.Values.Any(variant => Holds(variant, theme)),
        _ => false,
    };

    private static bool StateStyled(Control control)
    {
        var tokens = control.Classes.Where(static name => !name.StartsWith(':')).Select(static name => "." + name)
            .Append(control.StyleKey.Name).Concat(string.IsNullOrEmpty(control.Name) ? [] : ["#" + control.Name]).ToList();
        var sources = new List<IStyle>();
        for (StyledElement? element = control; element is not null; element = element.Parent as StyledElement)
        {
            sources.AddRange(element.Styles);
        }

        if (Application.Current is { } application)
        {
            sources.AddRange(application.Styles.Where(static style => !Guaranteed(style)));
        }

        return Selectors(sources, string.Empty).Any(selector => tokens.Any(token => selector.Contains(token + ":", StringComparison.Ordinal) || selector.Contains(token + "[", StringComparison.Ordinal)));
    }

    private static IEnumerable<string> Selectors(IEnumerable<IStyle> styles, string parent) =>
        styles.SelectMany(style => style switch
        {
            Styles nested when !Guaranteed(nested) => Selectors(nested, parent),
            Style { Selector: var selector } own when selector?.ToString().Replace("^", parent, StringComparison.Ordinal) is { } text => Selectors(own.Children, text).Prepend(text),
            _ => [],
        });

    private static bool Observed(Control control)
    {
        for (var element = (ILogical)control; element is not null and not Outlet; element = element.LogicalParent)
        {
            if (element is AvaloniaObject owner && (Declares(owner.GetType()) || AttachedByLibrary(owner)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Declares(Type type)
    {
        for (var current = type; current is not null && Foreign(current); current = current.BaseType)
        {
            const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            if (current.GetNestedTypes(BindingFlags.NonPublic).Length > 0 || current.GetMethods(Declared).Any(static method => method.Name != "InitializeComponent" && !method.Name.StartsWith('!')))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AttachedByLibrary(AvaloniaObject owner) =>
        AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(owner.GetType()).Any(property => Foreign(property.OwnerType) && owner.IsSet(property));
}
