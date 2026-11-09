using System.Globalization;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Aioros.Avalonia.Html.Writers;

internal static class PagerWriters
{
    private const string Previous = "PART_PreviousButton";

    private const string Next = "PART_NextButton";

    private const string PreviousIcon = "M 8.12,2.29 L 3.41,7.00 C 3.14,7.27 3.14,7.71 3.41,7.98 L 8.12,12.69 C 8.57,13.14 9.33,12.82 9.33,12.19 L 9.33,2.79 C 9.33,2.16 8.57,1.84 8.12,2.29 Z";

    private const string NextIcon = "M 3.88,2.29 L 8.59,7.00 C 8.86,7.27 8.86,7.71 8.59,7.98 L 3.88,12.69 C 3.43,13.14 2.67,12.82 2.67,12.19 L 2.67,2.79 C 2.67,2.16 3.43,1.84 3.88,2.29 Z";

    private const string PreviousVerticalIcon = "M 2.29,8.12 L 7.00,3.41 C 7.27,3.14 7.71,3.14 7.98,3.41 L 12.69,8.12 C 13.14,8.57 12.82,9.33 12.19,9.33 L 2.79,9.33 C 2.16,9.33 1.84,8.57 2.29,8.12 Z";

    private const string NextVerticalIcon = "M 2.29,3.88 L 7.00,8.59 C 7.27,8.86 7.71,8.86 7.98,8.59 L 12.69,3.88 C 13.14,3.43 12.82,2.67 12.19,2.67 L 2.79,2.67 C 2.16,2.67 1.84,3.43 2.29,3.88 Z";

    public static void WritePipsPager(HtmlContext context, PipsPager pager, ParentKind parent)
    {
        var vertical = pager.Orientation == Orientation.Vertical;
        var style = context.Box(pager, parent);
        style.Append(vertical ? "flex-direction:column;" : "flex-direction:row;");
        style.Append("align-items:center;");
        style.Append(CssValues.Brush(pager.Background) is { } background ? $"background:{background};" : string.Empty);
        var index = Getter(context, pager, PipsPager.SelectedPageIndexProperty, pager.SelectedPageIndex);
        var count = Getter(context, pager, PipsPager.NumberOfPagesProperty, pager.NumberOfPages);
        context.Plan.Literal($"<div class=\"{context.Classes(pager, "c", style)}\">");
        context.Plan.Literal($"<div role=\"radiogroup\" class=\"c\" style=\"flex-direction:{(vertical ? "column" : "row")}\">");
        context.Fields.Add(pager);
        var pip = $"<label class=\"{PipClass(context, pager, vertical)}\">";
        var pages = Slots.Range(count);
        var selection = new SlotSpec("page", index);
        context.Plan.Add(new ChoiceNode(
            pages.Getter,
            Slots.Predicate(selection),
            new ChoiceMarkup(pip, pip, ItemParts.Input(context, multiple: false), "</label>"),
            [[]]) { ItemsSource = pages.Spec, Selection = selection, Field = Forms.FieldSpecs.Of(context.Plan, pager) });
        context.Plan.Literal("</div>");
        if (pager.IsPreviousButtonVisible)
        {
            WriteStep(context, pager, Previous, vertical ? PreviousVerticalIcon : PreviousIcon, Slots.FirstPage(index));
        }

        if (pager.IsNextButtonVisible)
        {
            WriteStep(context, pager, Next, vertical ? NextVerticalIcon : NextIcon, Slots.LastPage(index, count));
        }

        context.Plan.Literal("</div>");
    }

    public static void Step(Button button, string? value)
    {
        if (value is null || button.TemplatedParent is not PipsPager pager)
        {
            return;
        }

        var step = button.Name == Previous ? -1 : 1;
        pager.SetCurrentValue(PipsPager.SelectedPageIndexProperty, Math.Clamp(pager.SelectedPageIndex + step, 0, Math.Max(0, pager.NumberOfPages - 1)));
    }

    public static bool IsStep(Control control) => control is Button { TemplatedParent: PipsPager, Name: Previous or Next };

    /// <summary>How far a pager's step button moves the selected page: back one for its previous button, on one for its next.</summary>
    public static int StepOf(Control button) => button.Name == Previous ? -1 : 1;

    private static string Getter(HtmlContext context, PipsPager pager, global::Avalonia.AvaloniaProperty property, int value) =>
        Slots.Operand(context.Plan.Planning ? context.Plan.BoundPath(pager, property) : null, value);

    private static string PipClass(HtmlContext context, PipsPager pager, bool vertical)
    {
        var normal = ItemLooks.Resource(pager, "PipsPagerSelectionIndicatorForeground") ?? "rgba(0,0,0,.45)";
        var hover = ItemLooks.Resource(pager, "PipsPagerSelectionIndicatorForegroundPointerOver") ?? normal;
        var selected = ItemLooks.Resource(pager, "PipsPagerSelectionIndicatorForegroundSelected") ?? normal;
        var size = vertical ? "width:24px;height:12px;" : "width:12px;height:24px;";
        var rules = $"{{position:relative;display:flex;align-items:center;justify-content:center;cursor:pointer;{size}}}" +
                    $"|::before{{content:\"\";width:4px;height:4px;border-radius:50%;background:{normal};transition:width .167s,height .167s,background .167s}}" +
                    $"|:hover::before{{width:6px;height:6px;background:{hover}}}" +
                    $"|{ItemLooks.Checked}::before{{width:6px;height:6px;background:{selected}}}" +
                    $"|{ItemLooks.Checked}:hover::before{{background:{hover}}}";
        var name = "pp" + CssValues.Hash(rules);
        context.Sheet.AddRule(name, string.Concat(rules.Split('|').Select(rule => "." + name + rule)) + "\n");
        return name;
    }

    private static void WriteStep(HtmlContext context, PipsPager pager, string part, string icon, Slot hidden)
    {
        if (ItemLooks.Part<Button>(pager, part) is not { } button)
        {
            return;
        }

        var style = new System.Text.StringBuilder("display:inline-flex;align-items:center;justify-content:center;flex-shrink:0;");
        var look = string.Empty;
        ThemeLooks.ExpandTemplates(button);
        if (ItemLooks.Part<global::Avalonia.Controls.Presenters.ContentPresenter>(button, "PART_ContentPresenter") is { } presenter)
        {
            BoxStyles.Fill(style, null, presenter.BorderBrush, presenter.BorderThickness, presenter.CornerRadius, presenter.Padding);
            look = Enabled(button, () => ItemLooks.Class(context, button, presenter, presenter, ItemLooks.Marked));
        }

        style.Append(CultureInfo.InvariantCulture, $"width:{CssValues.Px(button.Width)};height:{CssValues.Px(button.Height)};");
        style.Append(part == Previous ? "order:-1;" : string.Empty);
        context.Plan.Literal("<button type=\"submit\" name=\"f");
        context.Field(button);
        context.Plan.Literal($"\" value=\"on\" aria-label=\"{(part == Previous ? "Previous page" : "Next page")}\" class=\"{context.Sheet.Class(look, style)}\"");
        context.Plan.Add(new ConditionNode(hidden.Getter, [new LiteralNode(" style=\"visibility:hidden\"")]) { Source = hidden.Spec });
        context.Plan.Literal($"><svg viewBox=\"{(icon == PreviousIcon || icon == NextIcon ? "2.67 1.84 6.66 11.3" : "1.84 2.16 11.3 7.17")}\" width=\"10\" height=\"10\" fill=\"currentColor\"><path d=\"{icon}\"/></svg></button>");
    }

    /// <summary>
    /// What <paramref name="read"/> reads from a step button as it looks enabled: Avalonia disables the step at the pager's
    /// end, where the plan hides it, so the button's class is the same whatever page the trace saw.
    /// </summary>
    private static string Enabled(Button button, Func<string> read)
    {
        var classes = (IPseudoClasses)button.Classes;
        var disabled = button.Classes.Contains(":disabled");
        if (disabled)
        {
            classes.Remove(":disabled");
        }

        try
        {
            return read();
        }
        finally
        {
            if (disabled)
            {
                classes.Add(":disabled");
            }
        }
    }
}
