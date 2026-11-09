using Aioros.Avalonia.Controls;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// Writes what <see cref="Drag"/> declares: a target's element carries its field token in <c>data-drop</c>, an item's
/// element its key in <c>data-drag</c> and the focus a keyboard needs to pick it up, and, for a browser without
/// JavaScript, move buttons that post its target's token, a step and its key.
/// </summary>
internal static class DragWriters
{
    private const string TargetKey = "drop";

    public static void Open(HtmlContext context, Control control, Control? owner)
    {
        if ((owner is not null && Drag.IsTarget(owner) ? owner : Drag.IsTarget(control) ? control : null) is { } target)
        {
            if (target == owner)
            {
                context.Attributes(target);
            }

            context.Plan.Literal(" data-drop=\"");
            context.Button(target, TargetKey);
            context.Plan.Literal("\"");
        }

        if (control.IsSet(Drag.ItemProperty))
        {
            context.Plan.Literal(" data-drag=\"");
            context.Plan.Value(control, Drag.ItemProperty);
            context.Plan.Literal("\" tabindex=\"0\" aria-roledescription=\"draggable\"");
        }
    }

    /// <summary>Whether a control's own element carries drag marks, so it is written even when it only wraps a child.</summary>
    public static bool Marks(Control control) => control.IsSet(Drag.ItemProperty) || (Drag.IsTarget(control) && control is not ItemsControl);

    public static void Close(HtmlContext context, Control control)
    {
        if (!control.IsSet(Drag.ItemProperty))
        {
            return;
        }

        context.Plan.Literal("<noscript>");
        Step(context, control, "up", "Move up");
        Step(context, control, "down", "Move down");
        context.Plan.Literal("</noscript>");
    }

    private static void Step(HtmlContext context, Control control, string step, string label)
    {
        context.Plan.Literal("<button type=\"submit\" name=\"_btn\" value=\"");
        context.Plan.Add(new SameIdNode(TargetKey));
        context.Plan.Literal($"|{step}|");
        context.Plan.Value(control, Drag.ItemProperty);
        context.Plan.Literal($"\">{label}</button>");
    }
}
