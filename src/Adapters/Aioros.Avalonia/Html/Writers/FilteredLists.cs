using Aioros.Avalonia.Plans;
using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A list its template filters as the visitor types (<see cref="Web.FilterProperty"/>, a command menu, cmdk's), written
/// as the WAI-ARIA combobox with a list popup: its text box is a <c>combobox</c> with <c>aria-autocomplete="list"</c>,
/// its items panel the <c>listbox</c>, each item an <c>option</c> (its <c>TextSearch.Text</c> as <c>data-value</c>),
/// each item that is a list of its own a <c>group</c> of options, and the part shown when nothing matches marked and
/// hidden while items show. The framework's filter module filters, ranks and selects in the browser (FilterScript).
/// </summary>
internal static class FilteredLists
{
    public const string Input = "Input";

    public const string Empty = "Empty";

    private const string Css = "[role=listbox] [hidden],[data-aioros-empty][hidden]{display:none!important}\n";

    /// <summary>Whether the control is a filtered list's part the plan writes even while it is hidden: the part shown when nothing matches.</summary>
    public static bool WritesHidden(Control control) => !control.IsVisible && Web.GetFilter(control) == Empty && control.TemplatedParent is ItemsControl;

    /// <summary>Whether a control has a role in a filtered list, so a wrapper keeps its own element.</summary>
    public static bool Marks(Control control) => Role(control) is not null;

    /// <summary>Whether a button is an option of a filtered list, whose selected look follows <c>aria-selected</c>.</summary>
    public static bool IsOption(Control control) => Role(control) == "option";

    /// <summary>The attributes of the control's role in a filtered list, written at its element.</summary>
    public static void Attributes(HtmlContext context, Control control)
    {
        switch (Role(control))
        {
            case "combobox":
                context.Plan.Literal(" data-aioros-filter role=\"combobox\" aria-autocomplete=\"list\" aria-expanded=\"true\"");
                break;
            case "listbox":
                context.Sheet.AddRule("filtered", Css);
                context.Plan.Literal(" role=\"listbox\"");
                break;
            case "group":
                context.Plan.Literal(" role=\"group\"");
                break;
            case "option":
                context.Plan.Literal(" tabindex=\"-1\" role=\"option\"");
                Value(context, control);
                break;
            case "empty":
                context.Sheet.AddRule("filtered", Css);
                context.Plan.Literal(control.IsVisible ? " data-aioros-empty" : " data-aioros-empty hidden");
                break;
        }
    }

    private static void Value(HtmlContext context, Control option)
    {
        if (LocalBindings.IsBound(option, TextSearch.TextProperty))
        {
            context.Plan.Literal(" data-value=\"");
            context.Plan.Value(option, TextSearch.TextProperty);
            context.Plan.Literal("\"");
        }
        else if (TextSearch.GetText(option) is { Length: > 0 } value)
        {
            context.Plan.Literal($" data-value=\"{Looks.CssValues.Encode(value)}\"");
        }
    }

    private static string? Role(Control control)
    {
        if (Web.GetFilter(control) is { } filter)
        {
            return filter switch
            {
                Input when control is TextBox && Filtered(control.TemplatedParent as ItemsControl) => "combobox",
                Empty when Filtered(control.TemplatedParent as ItemsControl) => "empty",
                _ => null,
            };
        }

        if (control is Panel { IsItemsHost: true } && control.GetVisualParent() is ItemsPresenter { TemplatedParent: ItemsControl list } && Filtered(list))
        {
            return "listbox";
        }

        if (ItemsControl.ItemsControlFromItemContainer(control) is not { } owner)
        {
            return null;
        }

        if (control is Separator)
        {
            return null;
        }

        if (Filtered(owner))
        {
            return control is ItemsControl ? "group" : "option";
        }

        return control is not ItemsControl && ItemsControl.ItemsControlFromItemContainer(owner) is { } outer && Filtered(outer) ? "option" : null;
    }

    /// <summary>Whether the list's template holds its filtering text box, looked for in the template's parts only, not in its items.</summary>
    private static bool Filtered(ItemsControl? list)
    {
        if (list is null)
        {
            return false;
        }

        var parts = new Stack<Visual>(list.GetVisualChildren());
        while (parts.TryPop(out var part))
        {
            if (part is TextBox box && ReferenceEquals(box.TemplatedParent, list) && Web.GetFilter(box) == Input)
            {
                return true;
            }

            if (part is not ItemsPresenter)
            {
                foreach (var child in part.GetVisualChildren())
                {
                    parts.Push(child);
                }
            }
        }

        return false;
    }
}
