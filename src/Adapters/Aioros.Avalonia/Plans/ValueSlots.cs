using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Templating;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Plans;

/// <summary>The slots a plan writes for bound values: a formatted value, or a flag a condition reads.</summary>
internal static class ValueSlots
{
    /// <summary>Writes the slot of a value the plan follows, or reports that it does not follow it.</summary>
    public static bool Write(PlanRecorder plan, AvaloniaObject target, AvaloniaProperty property, ValueFormat format)
    {
        if (plan.TemplateShown(target, property) is { } shown)
        {
            plan.Follow(target, property);
            plan.Value(shown.Parent, shown.Property, format);
            return true;
        }

        if ((MultiBindings.Of(plan, target, property) ?? BoundConverters.Of(plan, target, property)) is { } converted)
        {
            plan.Follow(target, property);
            var output = ValueFormats.Of(format);
            plan.Add(new ValueNode(converted.Getter, output.Formatter, Raw: format == ValueFormat.Icon) { Source = converted.Spec, FormatSpec = output.Spec });
            return true;
        }

        if (plan.BoundPath(target, property) is { } path)
        {
            var slot = Slots.Path(path);
            var formatted = format == ValueFormat.Text && LocalBindings.Of(target, property) is { } binding && BindingFormats.StringFormat(binding) is { } pattern
                ? Slots.FormatOf("string-format", pattern)
                : ValueFormats.Of(format);
            plan.Add(new ValueNode(slot.Getter, formatted.Formatter, Raw: format == ValueFormat.Icon) { Source = slot.Spec, FormatSpec = formatted.Spec });
            return true;
        }

        if (format == ValueFormat.Text && ContentTexts.Owner(plan, target, property) is { } owner && ContentTexts.Write(plan, owner.Owner, owner.Property, access: false))
        {
            return true;
        }

        if (format == ValueFormat.Text && ShowsScope(plan, target, property))
        {
            plan.Add(new ValueNode(Slots.Self.Getter) { Source = Slots.Self.Spec });
            return true;
        }

        return false;
    }

    /// <summary>The flag a bound boolean property reads, negated for <c>{Binding !Path}</c>, or nothing when it is not bound.</summary>
    public static Slot? Flag(PlanRecorder plan, AvaloniaObject target, AvaloniaProperty property)
    {
        if (plan.TemplateShown(target, property) is { } shown)
        {
            plan.Follow(target, property);
            return Flag(plan, shown.Parent, shown.Property);
        }

        if ((MultiBindings.Of(plan, target, property) ?? BoundConverters.Of(plan, target, property)) is { } converted)
        {
            plan.Follow(target, property);
            return new Slot(converted.Getter, converted.Spec!);
        }

        var fallback = (property.GetMetadata(target.GetType()) as IStyledPropertyMetadata)?.DefaultValue is true;
        if (plan.NegatedPath(target, property) is { } negated)
        {
            return Flags.Of(negated, negate: true, fallback);
        }

        return plan.BoundPath(target, property) is { } path ? Flags.Of(path, negate: false, fallback) : null;
    }

    /// <summary>
    /// Whether a text is the one Avalonia's default data template shows for a presenter's content that is the current
    /// scope (an item without a template): it binds the item itself, so each item writes its own text.
    /// </summary>
    private static bool ShowsScope(PlanRecorder plan, AvaloniaObject target, AvaloniaProperty property) =>
        property == TextBlock.TextProperty && target is TextBlock text && text.GetVisualParent() is global::Avalonia.Controls.Presenters.ContentPresenter { ContentTemplate: null } presenter
        && text.DataContext is { } item && ReferenceEquals(item, plan.Scope) && ReferenceEquals(presenter.Content, item)
        && string.Equals(text.Text, PlanValues.Format(item), StringComparison.Ordinal);
}
