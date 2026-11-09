using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// The parts a control's template draws its field with, inside its <c>DataValidationErrors</c> and beside the messages:
/// the field the web writes inside the element that holds the control's whole box.
/// </summary>
internal static class FieldParts
{
    /// <summary>
    /// The field's own minimum height: the largest minimum height of the shown parts that draw it (Fluent's text box
    /// border, its numeric box's inner text box, Aioros.Cosmo's panel), since any of them holds the field at least that
    /// tall, where the control's own minimum height holds its messages too; nothing without validation messages.
    /// </summary>
    public static double MinHeight(Control control)
    {
        ThemeLooks.ExpandTemplates(control);
        if (control.GetVisualDescendants().OfType<DataValidationErrors>().FirstOrDefault()?.Content is not Control field)
        {
            return 0;
        }

        return field.GetVisualDescendants().OfType<Control>().Prepend(field)
            .Where(part => part.GetVisualAncestors().OfType<Control>().TakeWhile(ancestor => !ReferenceEquals(ancestor, field)).Prepend(part).All(shown => shown.IsVisible))
            .Max(part => part.MinHeight);
    }
}
