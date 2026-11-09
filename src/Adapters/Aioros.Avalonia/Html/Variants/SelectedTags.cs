using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>
/// A bound <c>Tag</c> that style selectors read (<c>Button[Tag=urgent]</c>): the control is written once for each value
/// the selectors' texts convert to, compared as Avalonia's <c>PropertyEqualsSelector</c> compares them, and once matching
/// none; each render picks the one of the model's value. The texts come from every style that may apply to the control
/// or its descendants, so no selector that reads the value is missed.
/// </summary>
internal static partial class SelectedTags
{
    public static bool Next(Control control, HashSet<(Control, AvaloniaProperty)> expanding) =>
        !expanding.Contains((control, Control.TagProperty)) && LocalBindings.IsBound(control, Control.TagProperty) && Texts(control).Count > 0;

    public static void WriteVariants(PlanRecorder plan, Control control, HashSet<(Control, AvaloniaProperty)> expanding, Action write)
    {
        if (Values(plan, control) is not { } found)
        {
            return;
        }

        var (path, type, values) = found;
        expanding.Add((control, Control.TagProperty));
        try
        {
            foreach (var (value, text) in values)
            {
                Variant(plan, control, value, new SlotSpec("tag", path, "is") { Values = [type.AssemblyQualifiedName!, text] }, write);
            }

            Variant(plan, control, null, new SlotSpec("tag", path, "none") { Values = [type.AssemblyQualifiedName!, .. values.Select(value => value.Text)] }, write);
        }
        finally
        {
            Settled.Change(control, static () => { });
            expanding.Remove((control, Control.TagProperty));
        }
    }

    /// <summary>Whether the scope's value matches the spec's selector text, or, for the variant that matches none, none of its texts.</summary>
    public static ValueGetter Getter(SlotSpec spec, Runtime.Slots.MemberPaths paths)
    {
        var get = paths.Getter(spec.Path!);
        var type = Type.GetType(spec.Values[0], throwOnError: true)!;
        var expected = spec.Values.Skip(1).Select(text => Expected(type, text)).OfType<object>().ToImmutableArray();
        if (spec.Argument == "none")
        {
            return scope => get(scope) is not { } value || !expected.Contains(value);
        }

        return scope => get(scope) is { } value && expected.Contains(value);
    }

    private static (string Path, Type Type, List<(object Value, string Text)> Values)? Values(PlanRecorder plan, Control control)
    {
        var name = control.GetType().Name + ".Tag";
        var binding = LocalBindings.Of(control, Control.TagProperty)!;
        if (BindingFormats.Converter(binding) is not null || BindingFormats.StringFormat(binding) is not null)
        {
            plan.Fail($"{name} is bound with a {(BindingFormats.Converter(binding) is null ? "StringFormat" : "converter")}, which style selectors on it cannot follow");
            return null;
        }

        if (plan.BoundPath(control, Control.TagProperty) is not { } path)
        {
            return null;
        }

        var declared = plan.PathType(path);
        var type = declared is null ? null : Nullable.GetUnderlyingType(declared) ?? declared;
        if (type is null || !type.IsSealed)
        {
            plan.Fail($"{name} is bound to '{path}', whose type ({type?.Name ?? "unknown"}) is not sealed, so its selectors cannot be compared as Avalonia compares them");
            return null;
        }

        var values = new List<(object Value, string Text)>();
        foreach (var text in Texts(control))
        {
            if (Expected(type, text) is { } value && !values.Exists(known => Equals(known.Value, value)))
            {
                values.Add((value, text));
            }
        }

        return (path, type, values);
    }

    private static void Variant(PlanRecorder plan, Control control, object? value, SlotSpec spec, Action write)
    {
        ImmutableArray<IPlanNode> body;
        using (LocalBindings.Override(control, Control.TagProperty, value))
        {
            Settled.Change(control, static () => { });
            plan.Begin(plan.Scope);
            write();
            body = plan.End();
        }

        plan.Add(new ConditionNode(Getter(spec, Slots.Resolver.Paths), body) { Source = spec });
    }

    /// <summary>The value a selector's text is compared with, converted as <c>PropertyEqualsSelector.Compare</c> converts it, or nothing when it never matches or Avalonia's conversion throws on it (and Avalonia's render with it).</summary>
    private static object? Expected(Type type, string text)
    {
        if (type.IsAssignableFrom(typeof(string)))
        {
            return text;
        }

        var converter = TypeDescriptor.GetConverter(type);
        try
        {
            return converter.CanConvertFrom(typeof(string)) ? converter.ConvertFrom(null, CultureInfo.InvariantCulture, text) : null;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The texts of every <c>[Tag=text]</c> selector of the styles that may apply to the control or its descendants.</summary>
    private static List<string> Texts(Control control) =>
        [.. control.GetVisualDescendants().Concat(control.GetLogicalDescendants().OfType<Visual>()).Prepend(control).Distinct()
            .SelectMany(Sources).Distinct().SelectMany(Flatten).OfType<Style>()
            .SelectMany(style => TagSelector().Matches(style.ToString()).Select(match => match.Groups["text"].Value))
            .Distinct(StringComparer.Ordinal)];

    /// <summary>The styles Avalonia applies to an element: its control theme, its templated parent's, and the styles of every styling host above it.</summary>
    private static IEnumerable<IStyle> Sources(StyledElement element)
    {
        foreach (var owner in (StyledElement?[])[element, element.TemplatedParent as StyledElement])
        {
            for (var theme = owner?.Theme ?? (owner?.FindResource(owner.StyleKey) as ControlTheme); theme is not null; theme = theme.BasedOn)
            {
                yield return theme;
            }
        }

        for (IStyleHost? host = element; host is not null; host = host.StylingParent)
        {
            if (host.IsStylesInitialized)
            {
                yield return host.Styles;
            }
        }
    }

    private static IEnumerable<IStyle> Flatten(IStyle style) => style.Children.SelectMany(Flatten).Prepend(style);

    [GeneratedRegex(@"\[Tag=(?<text>[^\]]*)\]")]
    private static partial Regex TagSelector();
}
