using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A date picker whose control theme is not Fluent's, written through its template as shadcn/ui's date picker is built
/// (a popover whose trigger shows the date, its content a calendar): the template's parts as they are, its drop-down
/// button opening a popover in the browser with no post, and the popover written from the template's popup with the
/// popup's content (its border and calendar, written as <see cref="ThemedCalendarWriters"/> writes calendars), realized
/// for the time it is written as Avalonia would show it (<see cref="Probes"/>) and placed as Avalonia places the popup;
/// the looks its theme gives the parts while the picker is pointed at, pressed or holds the focus follow it in the browser.
/// </summary>
internal static class ThemedDatePickerWriters
{
    public static bool Writes(CalendarDatePicker picker) =>
        !FluentThemes.Owns(picker, typeof(CalendarDatePicker)) && Part<Popup>(picker, "PART_Popup") is { Child: not null } && Part<Button>(picker, "PART_Button") is not null;

    public static void Write(HtmlContext context, CalendarDatePicker picker, ParentKind parent)
    {
        if (context.Plan.Planning && LocalBindings.IsBound(picker, CalendarDatePicker.SelectedDateProperty))
        {
            context.Plan.Fail("a CalendarDatePicker with a bound SelectedDate");
        }

        var (button, popup) = (Part<Button>(picker, "PART_Button")!, Part<Popup>(picker, "PART_Popup")!);
        var id = context.NextPopover();
        StateStyles.RegisterTemplate(context, picker);
        context.Plan.Literal("<div data-aioros-picker");
        context.Root(picker, "c", context.Box(picker, parent), $"anchor-name:--{id}");
        context.Plan.Literal(">");
        var toggles = button.GetValue(Web.TogglesProperty);
        button.SetValue(Web.TogglesProperty, id);
        context.Substitutes[popup] = _ => { };
        try
        {
            if (picker.GetVisualChildren().OfType<Control>().FirstOrDefault() is { } root)
            {
                context.Write(root, ParentKind.Single);
            }
        }
        finally
        {
            context.Substitutes.Remove(popup);
            button.SetValue(Web.TogglesProperty, toggles);
        }

        Popover(context, picker, popup, id);
        context.Plan.Literal("</div>");
    }

    /// <summary>The popup's content lent to a host around the picker for the time it is written, then given back to the popup.</summary>
    private static void Popover(HtmlContext context, CalendarDatePicker picker, Popup popup, string id)
    {
        var content = popup.Child!;
        popup.Child = null;
        try
        {
            Probes.With(picker, content, surface =>
            {
                context.Sheet.AddRule("fly", FlyoutWriters.FlyCss);
                var open = picker.IsDropDownOpen || DatePickerPosts.KeptOpen(picker) ? " data-aioros-open" : string.Empty;
                context.Plan.Literal($"<div popover id=\"{id}\" role=\"dialog\"{open} class=\"fly\" style=\"position-anchor:--{id};{Placements.Of(popup)}\">");
                if (surface is not null)
                {
                    context.Write(surface, ParentKind.Column);
                }

                context.Plan.Literal("</div>");
            });
        }
        finally
        {
            popup.Child = content;
        }
    }

    private static T? Part<T>(CalendarDatePicker picker, string name)
        where T : Control
    {
        ThemeLooks.ExpandTemplates(picker);
        return picker.GetVisualDescendants().OfType<T>().FirstOrDefault(part => part.Name == name && ReferenceEquals(part.TemplatedParent, picker));
    }
}
