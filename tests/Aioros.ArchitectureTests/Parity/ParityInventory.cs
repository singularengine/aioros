using System.Reflection;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace Aioros.ArchitectureTests.Parity;

public sealed record Construct(string Kind, string Name);

public static class ParityInventory
{
    public static readonly string[] Kinds = ["control", "state", "layout", "text", "selector", "styling", "binding", "template", "animation", "markup", "visual"];

    private static readonly Type[] LayoutOwners =
    [
        typeof(Visual), typeof(Layoutable), typeof(Panel), typeof(Grid), typeof(DefinitionBase), typeof(ColumnDefinition), typeof(RowDefinition),
        typeof(DockPanel), typeof(StackPanel), typeof(WrapPanel), typeof(UniformGrid), typeof(RelativePanel), typeof(Canvas),
        typeof(Border), typeof(Decorator), typeof(Viewbox), typeof(ScrollViewer), typeof(LayoutTransformControl),
    ];

    private static readonly Type[] TextOwners = [typeof(TextBlock), typeof(TextElement)];

    private static IEnumerable<Assembly> Assemblies =>
        [typeof(AvaloniaObject).Assembly, typeof(Control).Assembly, typeof(Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension).Assembly, typeof(Avalonia.Data.Binding).Assembly];

    public static IEnumerable<Construct> All() =>
        Controls().Concat(States()).Concat(LayoutProperties()).Concat(Text()).Concat(Selectors()).Concat(Styling())
            .Concat(Bindings()).Concat(Templates()).Concat(Animations()).Concat(MarkupExtensions()).Concat(Visuals())
            .Distinct()
            .OrderBy(construct => Array.IndexOf(Kinds, construct.Kind))
            .ThenBy(construct => construct.Name, StringComparer.Ordinal);

    public static IEnumerable<Construct> Controls() =>
        Aioros.ArchitectureTests.Controls.ControlCoverage.AvaloniaControls().Select(name => new Construct("control", name));

    public static IEnumerable<Construct> States() =>
        Exported()
            .SelectMany(type => type.GetCustomAttributes<PseudoClassesAttribute>(inherit: false))
            .SelectMany(attribute => attribute.PseudoClasses)
            .Distinct(StringComparer.Ordinal)
            .Select(pseudo => new Construct("state", pseudo));

    public static IEnumerable<Construct> LayoutProperties() =>
        LayoutOwners.SelectMany(Properties).Select(name => new Construct("layout", name));

    public static IEnumerable<Construct> Text() =>
        TextOwners.SelectMany(Properties).Select(name => new Construct("text", name))
            .Concat(Concrete<Inline>().Select(type => new Construct("text", type.FullName!)));

    public static IEnumerable<Construct> Selectors() =>
        typeof(Avalonia.Styling.Selectors).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => new Construct("selector", "Selectors." + method.Name));

    public static IEnumerable<Construct> Styling() =>
        Exported().Where(type => !type.IsAbstract && !type.IsInterface && !type.IsGenericTypeDefinition
                && (typeof(IStyle).IsAssignableFrom(type) || typeof(IResourceProvider).IsAssignableFrom(type)) && !typeof(Control).IsAssignableFrom(type) && type != typeof(Application))
            .Select(type => new Construct("styling", type.FullName!))
            .Concat(StaticValues(typeof(ThemeVariant)).Select(name => new Construct("styling", name)));

    public static IEnumerable<Construct> Bindings() =>
        Concrete<BindingBase>().Where(type => type.Namespace?.StartsWith("Avalonia.Markup", StringComparison.Ordinal) != true).Select(type => new Construct("binding", type.FullName!))
            .Concat(EnumValues<BindingMode>().Concat(EnumValues<RelativeSourceMode>()).Select(name => new Construct("binding", name)));

    public static IEnumerable<Construct> Templates() =>
        Exported().Where(type => !type.IsAbstract && !type.IsInterface && !type.IsGenericTypeDefinition
                && type.GetInterfaces().Any(contract => contract.Name.StartsWith("ITemplate", StringComparison.Ordinal) || contract == typeof(IDataTemplate)))
            .Select(type => new Construct("template", type.FullName!));

    public static IEnumerable<Construct> Animations() =>
        Concrete<ITransition>().Concat(Concrete<Easing>()).Concat(Concrete<IPageTransition>()).Append(typeof(Animation)).Append(typeof(KeyFrame))
            .Select(type => new Construct("animation", type.FullName!))
            .Concat(EnumValues<FillMode>().Concat(EnumValues<PlaybackDirection>()).Select(name => new Construct("animation", name)));

    public static IEnumerable<Construct> MarkupExtensions() =>
        Exported().Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition && type.Namespace?.StartsWith("Avalonia.Markup", StringComparison.Ordinal) == true
                && (type.Name.EndsWith("Extension", StringComparison.Ordinal) || type.GetMethod("ProvideValue", [typeof(IServiceProvider)]) is not null))
            .Select(type => new Construct("markup", type.FullName!));

    public static IEnumerable<Construct> Visuals() =>
        Concrete<IBrush>().Concat(Concrete<IEffect>()).Concat(Concrete<Transform>())
            .Where(type => type.IsPublic)
            .Select(type => new Construct("visual", type.FullName!));

    private static IEnumerable<Type> Exported() =>
        Assemblies.Distinct().SelectMany(assembly => assembly.GetExportedTypes());

    private static IEnumerable<Type> Concrete<T>() =>
        Exported().Where(type => typeof(T).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface && !type.IsGenericTypeDefinition);

    private static IEnumerable<string> Properties(Type owner) =>
        owner.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => typeof(AvaloniaProperty).IsAssignableFrom(field.FieldType) && field.Name.EndsWith("Property", StringComparison.Ordinal)
                && field.GetValue(null) is AvaloniaProperty { IsReadOnly: false })
            .Select(field => owner.Name + "." + field.Name[..^"Property".Length]);

    private static IEnumerable<string> StaticValues(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(property => property.PropertyType == type).Select(property => type.Name + "." + property.Name);

    private static IEnumerable<string> EnumValues<T>()
        where T : struct, Enum =>
        Enum.GetNames<T>().Select(name => typeof(T).Name + "." + name);
}
