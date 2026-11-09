using System.Collections.Immutable;
using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>
/// A bound property with a few possible values that only change how Avalonia lays out or styles the control (an
/// alignment or any other enum, a grid cell, a library's own flag that sets a pseudo-class, as Aioros.Cosmo's
/// <c>Field.IsInvalid</c> sets <c>:invalid</c>, on a library panel, whose flag only styles it, or a library's attached flag on a control that opens a flyout, Aioros.Cosmo's <c>c:Popover.IsOpen</c>, whose open flyout is written marked open: a templated control's own properties are learned by value, and a control that builds something from its own flag, a dialog's <c>IsOpen</c>, is not toggled while the page is written): the control is written once per value, each as Avalonia renders it, with
/// what its descendants bind of it read as constants of that value (<see cref="ConstantBindings.LearnedConstant"/>), and
/// each render picks the one of the model's value. A grid cell is clamped to the grid's rows and columns, as Avalonia
/// places it; a value Avalonia cannot convert leaves the property's default.
/// </summary>
internal static class ValueVariants
{
    private const int MostValues = 16;

    public static AvaloniaProperty? Next(Control control, HashSet<(Control, AvaloniaProperty)> expanding) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(control).Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(control.GetType()))
            .FirstOrDefault(property => !expanding.Contains((control, property)) && LocalBindings.IsBound(control, property) && Domain(control, property) is not null);

    public static void WriteVariants(PlanRecorder plan, Control control, AvaloniaProperty property, HashSet<(Control, AvaloniaProperty)> expanding, Action write)
    {
        expanding.Add((control, property));
        if (plan.BoundPath(control, property) is not { } path || Domain(control, property) is not { } domain)
        {
            write();
            expanding.Remove((control, property));
            return;
        }

        var held = ConstantBindings.LearnedConstant(control, property);
        try
        {
            foreach (var value in domain.Values)
            {
                ImmutableArray<IPlanNode> body;
                using (Apply(control, property, value))
                {
                    Settled.Change(control, static () => { });
                    plan.Begin(plan.Scope);
                    write();
                    body = plan.End();
                }

                var fallback = Equals(value, (property.GetMetadata(control.GetType()) as IStyledPropertyMetadata)?.DefaultValue) ? "default" : string.Empty;
                var spec = new SlotSpec("is", path, Format(value)) { Values = [property.PropertyType.AssemblyQualifiedName!, domain.Low, domain.High, fallback] };
                plan.Add(new ConditionNode(Getter(spec, Slots.Resolver.Paths), body) { Source = spec });
            }
        }
        finally
        {
            held.Dispose();
            Settled.Change(control, static () => { });
            expanding.Remove((control, property));
        }
    }

    /// <summary>
    /// Gives the property the variant's value; a library's attached open flag of a flyout is not toggled (its code opens
    /// and closes the flyout's popup, and a flyout open from the start keeps its content in its presenter): the flyout is
    /// written open or closed by the value instead.
    /// </summary>
    private static IDisposable? Apply(Control control, AvaloniaProperty property, object value) =>
        property.IsAttached && value is bool open && ((control as Button)?.Flyout ?? global::Avalonia.Controls.Primitives.FlyoutBase.GetAttachedFlyout(control)) is global::Avalonia.Controls.Primitives.PopupFlyoutBase flyout
            ? Writers.FlyoutWriters.Shown(flyout, open)
            : LocalBindings.Override(control, property, value);

    /// <summary>Whether the scope's value, clamped to the domain when it has bounds, is the spec's value.</summary>
    public static ValueGetter Getter(SlotSpec spec, Runtime.Slots.MemberPaths paths)
    {
        var get = paths.Getter(spec.Path!);
        var type = Type.GetType(spec.Values[0], throwOnError: true)!;
        var expected = Parse(type, spec.Argument!);
        if (spec.Values[1].Length == 0)
        {
            var fallback = spec.Values[3].Length > 0;
            return scope => Coerce(type, get(scope)) is { } value ? Equals(value, expected) : fallback;
        }

        var low = int.Parse(spec.Values[1], CultureInfo.InvariantCulture);
        var high = int.Parse(spec.Values[2], CultureInfo.InvariantCulture);
        return scope => get(scope) is int value && Math.Clamp(value, low, high) == (int)expected;
    }

    private static (IReadOnlyList<object> Values, string Low, string High)? Domain(Control control, AvaloniaProperty property)
    {
        var type = property.PropertyType;
        if (type.IsEnum && !type.IsDefined(typeof(FlagsAttribute), inherit: false) && Enum.GetValues(type) is { Length: <= MostValues } values)
        {
            return ([.. values.Cast<object>().Distinct()], string.Empty, string.Empty);
        }

        if (type == typeof(bool) && LibraryFlag(control, property))
        {
            return ([false, true], string.Empty, string.Empty);
        }

        if (BoxStyles.GridOf(control) is not { } grid)
        {
            return null;
        }

        var count = property == Grid.ColumnProperty || property == Grid.ColumnSpanProperty ? Math.Max(1, grid.ColumnDefinitions.Count)
            : property == Grid.RowProperty || property == Grid.RowSpanProperty ? Math.Max(1, grid.RowDefinitions.Count)
            : 0;
        if (count == 0 || count > MostValues)
        {
            return null;
        }

        var span = property == Grid.ColumnSpanProperty || property == Grid.RowSpanProperty;
        var low = span ? 1 : 0;
        var high = span ? count : count - 1;
        return (Enumerable.Range(low, high - low + 1).Cast<object>().ToList(), low.ToString(CultureInfo.InvariantCulture), high.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Whether a flag is a library's own that only styles a library panel or a library's button (Aioros.Cosmo's
    /// <c>SidebarMenuButton.IsActive</c>, its <c>:active</c> look), or a library's attached flag on a control that opens a
    /// flyout, which holds its open state (Aioros.Cosmo's <c>c:Popover.IsOpen</c>).
    /// </summary>
    private static bool LibraryFlag(Control control, AvaloniaProperty property) =>
        LibraryValues.FromLibrary(property.OwnerType) && BrowserFlags.Property(control) != property
        && (control is Panel || (!property.IsAttached && control is Button && LibraryValues.FromLibrary(control.GetType()))
            || (property.IsAttached && ((control as Button)?.Flyout is not null || global::Avalonia.Controls.Primitives.FlyoutBase.GetAttachedFlyout(control) is not null)));

    /// <summary>The value as Avalonia converts a bound value to an enum property, or nothing when it cannot.</summary>
    private static object? Coerce(Type type, object? value) => value switch
    {
        null => null,
        _ when type.IsInstanceOfType(value) => value,
        string text when type.IsEnum => Enum.TryParse(type, text, ignoreCase: true, out var parsed) ? parsed : null,
        int or long or short or byte when type.IsEnum => Enum.ToObject(type, value),
        _ => null,
    };

    private static string Format(object value) => value is Enum ? value.ToString()! : Convert.ToString(value, CultureInfo.InvariantCulture)!;

    private static object Parse(Type type, string text) => type.IsEnum ? Enum.Parse(type, text) : type == typeof(bool) ? bool.Parse(text) : int.Parse(text, CultureInfo.InvariantCulture);
}
