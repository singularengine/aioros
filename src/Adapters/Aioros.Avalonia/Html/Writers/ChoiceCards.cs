using System.Text;
using Aioros.Avalonia.Html.Looks;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A radio button or a check box whose theme (not Fluent's) is a card, its template's root a named border around its
/// parts (Aioros.Cosmo's questionnaire choice, shadcn/ui's choice card): a <c>&lt;label&gt;</c> taking the card's look
/// and its states (<c>:hover</c>, and <c>:has(input:checked)</c> for <c>:checked</c>), holding the template's parts as
/// they are laid out, the indicator (the first sized border outside the content presenters) written as the native input
/// drawn as the theme draws it, so the form and the keyboard keep the native control.
/// </summary>
internal static class ChoiceCards
{
    private static readonly (string Pseudo, string Selector)[] States =
        [(":pointerover", "hover"), (":pressed", "active"), (":checked", "has(input:checked)"), (":focus-visible", "has(input:focus-visible)")];

    /// <summary>
    /// The card a choice's template is: a named root border that draws a frame or holds parts beside its mark and its
    /// content (a description, a shortcut); null for Fluent's, or for a template a label of its mark and content writes
    /// whole (BUTTONS-86, BUTTONS-87).
    /// </summary>
    public static Border? Card(ToggleButton choice)
    {
        ThemeLooks.ExpandTemplates(choice);
        if (ThemeLooks.FromFluent(choice, choice is RadioButton ? typeof(RadioButton) : typeof(CheckBox))
            || choice.GetVisualChildren().FirstOrDefault() is not Border { Name.Length: > 0, Child: not null } card)
        {
            return null;
        }

        return card.BorderThickness != default || Beside(card, Mark(card)) ? card : null;
    }

    /// <summary>Whether the card shows a part besides its mark and its content: a text or a sized border of its own.</summary>
    private static bool Beside(Border card, Border? mark) =>
        card.GetVisualDescendants().OfType<Control>().Any(part => part.IsVisible && part is TextBlock or Border { Width: > 0, Height: > 0 } && !ReferenceEquals(part, mark)
            && !part.GetVisualAncestors().TakeWhile(ancestor => !ReferenceEquals(ancestor, card)).Any(ancestor => ancestor is ContentPresenter || ReferenceEquals(ancestor, mark)));

    /// <summary>Writes the choice as its card, <paramref name="input"/> writing its native input with the given attributes.</summary>
    public static void Write(HtmlContext context, ToggleButton choice, ParentKind parent, Border card, Action<string> input)
    {
        var style = context.Box(choice, parent);
        BoxStyles.Font(style, choice.FontSize, choice.FontWeight, choice.Foreground);
        var look = Resting(choice, () => ThemeLooks.Of(choice, card.Name!, States));
        style.Append(look.Css);
        style.Append("display:grid;justify-items:stretch;align-items:stretch;text-align:start;cursor:pointer;user-select:none;");
        var mark = Mark(card);
        var drawn = mark is { Name.Length: > 0 }
            ? MarkLooks.Class(context, choice, new StringBuilder(), " input", "input", MarkLooks.States(choice, "input", direct: false), mark.Name)
            : null;
        style.Append(mark is null ? "position:relative;" : string.Empty);
        context.Plan.Literal("<label");
        context.Root(choice, CssValues.Join(context.Sheet.States(look), drawn), style, attributes: false);
        context.Plan.Literal(">");
        if (mark is null)
        {
            input(" style=\"position:absolute;inset:0;opacity:0;margin:0;cursor:inherit\"");
        }

        context.WriteWith(card.Child!, ParentKind.Row, mark, kind => input($" style=\"{CssValues.Encode(Placement(context, mark!, kind))}\""));
        context.Plan.Literal("</label>");
    }

    /// <summary>
    /// The card's look taken unchecked, so a checked choice writes the same rest look and its checked look under its
    /// state; a disabled choice, whose state cannot change, keeps the look it has.
    /// </summary>
    private static Look Resting(ToggleButton choice, Func<Look> look)
    {
        if (!choice.Classes.Contains(":checked") || !choice.IsEffectivelyEnabled)
        {
            return look();
        }

        var classes = (IPseudoClasses)choice.Classes;
        Settled.Change(choice, () => classes.Remove(":checked"));
        try
        {
            return look();
        }
        finally
        {
            Settled.Change(choice, () => classes.Add(":checked"));
        }
    }

    /// <summary>The indicator: the first border of a fixed size outside the content presenters.</summary>
    private static Border? Mark(Border card) =>
        card.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => !double.IsNaN(border.Width) && !double.IsNaN(border.Height)
            && !border.GetVisualAncestors().TakeWhile(ancestor => !ReferenceEquals(ancestor, card)).Any(ancestor => ancestor is ContentPresenter));

    /// <summary>Where the indicator sits among the card's parts: its box (size, margin, place in its panel) and its render transform.</summary>
    private static string Placement(HtmlContext context, Control mark, ParentKind parent)
    {
        var box = context.Box(mark, parent);
        box.Append(mark.RenderTransform is { Value.IsIdentity: false } transform ? $"transform:{CssValues.Transform(transform)};" : string.Empty);
        return box.ToString();
    }
}
