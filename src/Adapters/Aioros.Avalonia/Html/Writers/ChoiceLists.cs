using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Panels;
using Aioros.Avalonia.Plans;
using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A library's list that chooses in the browser (<see cref="Web.ChoiceProperty"/>, Aioros.Cosmo's combobox), as a native
/// <c>&lt;select&gt;</c> does and as cmdk's <c>onSelect</c> closes shadcn/ui's popover: its element is marked
/// <c>data-aioros-choice</c> and holds, after its content, the hidden field of the chosen index (posted with every post,
/// or, when the choice is bound two way, disabled until the visitor chooses and then posted as a change) and one
/// <c>&lt;template&gt;</c> per index of the part showing the choice (<see cref="Web.ShowsChoiceProperty"/>), written as
/// Avalonia renders it for that index; each option (<see cref="Web.ChoosesProperty"/>) carries the index it chooses and
/// <c>aria-checked</c>. The choice module of the framework's script does the rest (ChoiceScript).
/// </summary>
internal static class ChoiceLists
{
    private const int MostItems = 100;

    /// <summary>The list's property holding the chosen index, when it names an <c>int</c> property and holds few enough items to write each.</summary>
    public static AvaloniaProperty? Property(Control control) =>
        control is ItemsControl { ItemCount: <= MostItems } && Web.GetChoice(control) is { Length: > 0 } name
        && AvaloniaPropertyRegistry.Instance.FindRegistered(control, name) is { } property && property.PropertyType == typeof(int)
            ? property
            : null;

    /// <summary>The list an option chooses in: the nearest one around it, or around the control whose flyout holds it.</summary>
    public static Control? Owner(HtmlContext context, Control option) =>
        Web.GetChooses(option) < 0
            ? null
            : Around(option).Concat(context.Opener is { } opener ? Around(opener) : []).FirstOrDefault(control => Property(control) is not null);

    /// <summary>Whether a control has a role in a list choosing in the browser, so a wrapper keeps its own element.</summary>
    public static bool Marks(Control control) => Property(control) is not null || Shows(control) is not null;

    /// <summary>The marks of the list, of the part showing its choice and of its options, written at their elements.</summary>
    public static void Attributes(HtmlContext context, Control control)
    {
        if (Property(control) is not null)
        {
            context.Plan.Literal(Toggles(control) ? " data-aioros-choice data-aioros-toggle" : " data-aioros-choice");
        }
        else if (Shows(control) is not null)
        {
            context.Plan.Literal(" data-aioros-shows");
        }
        else if (Owner(context, control) is { } owner)
        {
            var index = Web.GetChooses(control);
            var chosen = Equals(owner.GetValue(Property(owner)!), index);
            context.Plan.Literal(string.Create(CultureInfo.InvariantCulture, $" data-aioros-chooses=\"{index}\" aria-checked=\"{(chosen ? "true" : "false")}\""));
        }
    }

    /// <summary>Writes, last in the list's element, its hidden field and the templates of the part showing its choice.</summary>
    public static void Write(HtmlContext context, Control control)
    {
        if (Property(control) is not { } property)
        {
            return;
        }

        context.Plan.Literal("<input type=\"hidden\" name=\"f");
        context.Field(control);
        context.Plan.Literal(string.Create(CultureInfo.InvariantCulture, $"\" value=\"{control.GetValue(property)}\""));
        context.Plan.Literal(Posted(control, property) ? " disabled data-change>" : ">");
        if (control.GetVisualDescendants().OfType<Control>().FirstOrDefault(part => ReferenceEquals(Shows(part), control)) is { } view)
        {
            Templates(context, control, property, view);
        }
    }

    /// <summary>Gives the list the index a post carries, when it is one of its items' or -1.</summary>
    public static void Bind(Control control, string? value)
    {
        if (Property(control) is { } property && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var index)
            && index >= -1 && index < ((ItemsControl)control).ItemCount)
        {
            control.SetCurrentValue(property, index);
        }
    }

    private static IEnumerable<Control> Around(Control control) =>
        control.GetLogicalAncestors().OfType<Control>().Concat(control.GetVisualAncestors().OfType<Control>());

    /// <summary>The list whose choice a template part shows.</summary>
    private static Control? Shows(Control control) =>
        Web.GetShowsChoice(control) && control.TemplatedParent is Control owner && Property(owner) is not null ? owner : null;

    /// <summary>Whether choosing the chosen item again chooses none: a selecting list whose <c>SelectionMode</c> has <c>Toggle</c>.</summary>
    private static bool Toggles(Control control) =>
        control is SelectingItemsControl && AvaloniaPropertyRegistry.Instance.FindRegistered(control, "SelectionMode") is { } mode
        && control.GetValue(mode) is SelectionMode selection && selection.HasFlag(SelectionMode.Toggle);

    /// <summary>Whether the choice is the model's: its property, or a selecting list's selected index or item, bound two way.</summary>
    private static bool Posted(Control control, AvaloniaProperty property) =>
        (LocalBindings.Of(control, property) is { } binding && BindingModes.WritesToSource(binding))
        || (control is SelectingItemsControl list && ItemParts.PostsChange(list).Length > 0);

    /// <summary>
    /// The part showing the choice written for each index the list can hold, from none, each as Avalonia renders it for
    /// that index and as a constant of the page; the list gets its own index back, and the form its fields' numbers.
    /// </summary>
    private static void Templates(HtmlContext context, Control control, AvaloniaProperty property, Control view)
    {
        var current = control.GetValue(property);
        var kind = view.GetVisualParent() is Panel panel ? PanelLayouts.Of(panel, new StringBuilder(), context.Sheet).Kind : ParentKind.Single;
        var fields = context.Fields.Mark();
        var popovers = context.Popovers;
        var reserved = context.Plan.Popovers();
        try
        {
            for (var index = -1; index < ((ItemsControl)control).ItemCount; index++)
            {
                var value = index;
                Settled.Change(control, () => control.SetCurrentValue(property, value));
                Settled.Rebuilt(control);
                context.Plan.Literal(string.Create(CultureInfo.InvariantCulture, $"<template data-aioros-value=\"{index}\">"));
                if (view.IsVisible)
                {
                    context.Plan.WriteConstant(view, () => context.Write(view, kind));
                }

                context.Plan.Literal("</template>");
            }
        }
        finally
        {
            Settled.Change(control, () => control.SetCurrentValue(property, current));
            Settled.Rebuilt(control);
            context.Fields.Rewind(fields);
            context.Popovers = popovers;
            context.Plan.ForgetPopovers(reserved);
        }
    }
}
