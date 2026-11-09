using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// Bindings in a control template to a property of its templated control (<c>{TemplateBinding Name}</c>,
/// <c>$templatedParent.Name</c>): which property they read, and whether a part shows it unchanged.
/// </summary>
internal static class TemplateBindings
{
    public const string TemplatedParentPrefix = "$templatedParent.";

    private const string TemplateBindingPrefix = "{TemplateBinding ";

    /// <summary>
    /// The property of the templated control a binding reads: a <c>TemplateBinding</c> by its description
    /// (<c>{TemplateBinding Name}</c>), or a binding to the templated parent (<c>$templatedParent.Name</c>, through a
    /// converter or not); nothing for any other binding.
    /// </summary>
    public static AvaloniaProperty? Property(BindingExpressionBase expression, StyledElement parent)
    {
        var name = (expression as IDescription)?.Description switch
        {
            { } description when expression.GetType().Name == "TemplateBindingExpression" && description.StartsWith(TemplateBindingPrefix, StringComparison.Ordinal) && description.EndsWith('}') =>
                description[TemplateBindingPrefix.Length..^1].Trim(),
            { } description when description.StartsWith(TemplatedParentPrefix, StringComparison.Ordinal) && description[TemplatedParentPrefix.Length..].All(char.IsLetterOrDigit) =>
                description[TemplatedParentPrefix.Length..],
            _ => null,
        };
        return name is { Length: > 0 } ? AvaloniaPropertyRegistry.Instance.GetRegistered(parent).FirstOrDefault(property => property.Name == name) : null;
    }

    /// <summary>
    /// The property of its templated control a template part shows unchanged, when the control binds it (see
    /// <see cref="PlanRecorder.TemplateShown"/>), in any scope; not while a value is set above that binding, which the
    /// part then shows as a constant.
    /// </summary>
    public static (StyledElement Parent, AvaloniaProperty Property)? Bound(AvaloniaObject target, AvaloniaProperty property)
    {
        if (target is not Control { TemplatedParent: StyledElement parent } || BindingOperations.GetBindingExpressionBase(target, property) is not { } expression
            || BindingFormats.Converter(expression) is not null || Property(expression, parent) is not { } shown
            || !property.PropertyType.IsAssignableFrom(shown.PropertyType) || LocalBindings.Own(parent, shown) is null)
        {
            return null;
        }

        return (parent, shown);
    }

    /// <summary>
    /// Whether a part of the control's template shows the property unchanged, so the part's slot follows the control's
    /// binding; for an inherited property (a text color), only when no text below the control inherits it instead, which
    /// would keep the compiled model's value.
    /// </summary>
    public static bool ShownByTemplate(Control control, AvaloniaProperty property) =>
        control.GetVisualDescendants().OfType<Control>().Any(part => ReferenceEquals(part.TemplatedParent, control)
            && AvaloniaPropertyRegistry.Instance.GetRegistered(part).Any(read => Bound(part, read) is { } shown && shown.Property == property))
        && (!property.Inherits || !control.GetVisualDescendants().OfType<TextBlock>().Any(text => text.GetDiagnostic(property).Priority == BindingPriority.Inherited));

    /// <summary>Whether a binding reads the property of its templated control, alone or in a <c>MultiBinding</c>.</summary>
    public static bool Reads(BindingExpressionBase expression, StyledElement control, AvaloniaProperty property) =>
        Property(expression, control) == property || BindingSources.Children(expression)?.Any(child => Property(child, control) == property) == true;

    /// <summary>
    /// The templated control of a template part, when the page itself places it: not as the container of another item,
    /// though it may be an item declared in the XAML that is its own container (a <c>TabItem</c>).
    /// </summary>
    public static Control? ConstantParent(AvaloniaObject target) =>
        target is Control { TemplatedParent: Control { TemplatedParent: null } parent }
        && (ItemsControl.ItemsControlFromItemContainer(parent) is not { } owner
            || (!LocalBindings.HasBinding(owner, ItemsControl.ItemsSourceProperty) && ReferenceEquals(owner.ItemFromContainer(parent), parent)))
            ? parent
            : null;

    /// <summary>
    /// Whether a binding in a control template reads only <c>TemplateBinding</c>s of properties its templated control
    /// holds as constants of the page: a control placed by the page itself (not by another template, nor as an items
    /// control's container, whose content is its item) whose properties are set in the XAML or bound to elements that
    /// hold constants. Avalonia gives such a binding the same value on every render.
    /// </summary>
    public static bool ReadsConstantTemplate(AvaloniaObject target, BindingExpressionBase expression) =>
        ConstantParent(target) is { } parent && BindingSources.Children(expression) is { Length: > 0 } children
        && children.All(child => Property(child, parent) is { } property && ConstantBindings.ConstantProperty(parent, property));
}
