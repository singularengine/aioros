using Aioros.Avalonia.Plans;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// A content control written through its template whose content is bound to a value that is not a control and that no
/// data template types: Avalonia's presenter shows it as text (the text itself, or a text block bound to the value with
/// Avalonia's default data template), so the plan writes that text block's text from the content's binding.
/// </summary>
internal static class ContentTexts
{
    /// <summary>Whether a bound content is one the plan follows: a button's text, typed content, or text a template shows.</summary>
    public static bool Followed(Control control, AvaloniaProperty property) =>
        property == ContentControl.ContentProperty && ((control is Label { Content: not Control, Target: null }) || (control.GetType() == typeof(Button) && ((Button)control).Content is not Control) || TypedContent.Applies(control)
            || ChosenContent.Applies(control) || (control is ContentControl owner && Shown(owner)));

    /// <summary>Whether the control's bound content is shown as text by its template.</summary>
    public static bool Shown(ContentControl owner) =>
        LocalBindings.Of(owner, ContentControl.ContentProperty) is not null && owner.Content is not Control
        && (ControlWriters.For(owner) is null || (owner is Button button && Writers.ButtonWriters.WritesParts(button))) && !TypedContent.Applies(owner) && owner.ContentTemplate is null;

    /// <summary>
    /// Writes the content's text as the text block shows it: the binding's <c>TargetNullValue</c> for a null value, the
    /// value through its <c>StringFormat</c> or as text otherwise, with the access key marker taken out by an
    /// <c>AccessText</c>; or nothing when the content is not bound to a path the plan follows.
    /// </summary>
    public static bool Write(PlanRecorder plan, ContentControl owner, bool access) => Write(plan, owner, ContentControl.ContentProperty, access);

    /// <inheritdoc cref="Write(PlanRecorder, ContentControl, bool)"/>
    public static bool Write(PlanRecorder plan, StyledElement owner, AvaloniaProperty property, bool access)
    {
        if (LocalBindings.Of(owner, property) is not { } binding || plan.BoundPath(owner, property) is not { } path)
        {
            return false;
        }

        if (BindingFormats.TargetNullValue(binding) is { } fallback and not string)
        {
            plan.Fail($"{owner.GetType().Name}.{property.Name} is bound with a TargetNullValue that is not text ({fallback.GetType().Name})");
            return false;
        }

        var slot = Slots.Path(path);
        var format = Slots.FormatOf("content-text", BindingFormats.TargetNullValue(binding) as string, BindingFormats.StringFormat(binding) ?? string.Empty, access ? "access" : string.Empty);
        plan.Add(new Runtime.Rendering.ValueNode(slot.Getter, format.Formatter) { Source = slot.Spec, FormatSpec = format.Spec });
        return true;
    }

    /// <summary>
    /// The content control whose content a text block shows as Avalonia's presenter writes it, with the text block's own
    /// binding to that content followed, or nothing when the text block shows something else.
    /// </summary>
    public static (StyledElement Owner, AvaloniaProperty Property)? Owner(PlanRecorder plan, AvaloniaObject target, AvaloniaProperty property)
    {
        if (property != TextBlock.TextProperty || target is not TextBlock text
            || text.GetVisualParent() is not ContentPresenter { ContentTemplate: null, TemplatedParent: StyledElement parent } presenter
            || Presents(presenter, parent) is not { } shown)
        {
            return null;
        }

        var value = shown.Owner.GetValue(shown.Property);
        if (LocalBindings.Of(text, TextBlock.TextProperty) is { } binding)
        {
            if (binding is not IDescription { Description: "" } || !Equals(text.DataContext, value))
            {
                return null;
            }

            plan.Follow(text, TextBlock.TextProperty);
            return Presented(plan, presenter, shown);
        }

        return string.Equals(text.Text, value as string, StringComparison.Ordinal)
            || (value is not null && Equals(text.DataContext, value) && string.Equals(text.Text, Runtime.Rendering.PlanValues.Format(value), StringComparison.Ordinal))
            ? Presented(plan, presenter, shown)
            : null;
    }

    /// <summary>
    /// The bound property of its templated control a presenter shows as text: a content control's content in its
    /// <c>PART_ContentPresenter</c>, or any property of a library's control written through its template that the
    /// presenter's content reads unchanged (<c>Content="{TemplateBinding Title}"</c>, Aioros.Cosmo's <c>Alert.Title</c>),
    /// which Avalonia shows as a text block as it shows a content.
    /// </summary>
    private static (StyledElement Owner, AvaloniaProperty Property)? Presents(ContentPresenter presenter, StyledElement parent)
    {
        if (parent is ContentControl owner && presenter.Name == "PART_ContentPresenter" && Equals(presenter.Content, owner.Content))
        {
            return Shown(owner) ? (owner, ContentControl.ContentProperty) : null;
        }

        return parent is Control control && ControlWriters.For(control) is null && TemplateBindings.Bound(presenter, ContentPresenter.ContentProperty) is { } shown
            && ReferenceEquals(shown.Parent, parent) && LocalBindings.Of(parent, shown.Property) is not null && parent.GetValue(shown.Property) is not Control
            ? shown
            : null;
    }

    private static (StyledElement Owner, AvaloniaProperty Property) Presented(PlanRecorder plan, ContentPresenter presenter, (StyledElement Owner, AvaloniaProperty Property) shown)
    {
        plan.Presented(presenter, ContentPresenter.ContentProperty);
        return shown;
    }
}
