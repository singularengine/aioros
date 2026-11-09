using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Html.Variants;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;

namespace Aioros.Avalonia.Html;

/// <summary>
/// What a plan records around a control before it is written: the bindings it cannot follow, which fail the plan; the
/// expansions it learns (<see cref="PlanExpansions"/>); and a bound visibility, as a condition around the control, which
/// a control hidden in the compiled model is written shown and laid out for (a list's first item hiding what later ones
/// show, as a select only for unassigned rows), so its look is the one it has when it shows.
/// </summary>
internal sealed class PlannedVisibility(HtmlContext context, Action<Control, ParentKind> writeVisible)
{
    private static readonly AvaloniaProperty[] Unplannable =
    [
        ContentControl.ContentProperty, Layoutable.MaxWidthProperty,
        TextElement.ForegroundProperty,
        Border.BackgroundProperty, Panel.BackgroundProperty,
    ];

    private readonly HashSet<(Control, AvaloniaProperty)> _expanding = [];

    private PlanRecorder Plan => context.Plan;

    /// <summary>Writes the control as the plan records it, or false when it is written as any control is.</summary>
    public bool Write(Control control, ParentKind parent)
    {
        if (control.IsVisible || LocalBindings.HasBinding(control, Visual.IsVisibleProperty))
        {
            CheckPlannable(control);
        }

        if (PlanExpansions.Write(Plan, control, _expanding, () => context.Write(control, parent)))
        {
            return true;
        }

        if (Plan.BoundFlag(control, Visual.IsVisibleProperty) is not { } visible)
        {
            return false;
        }

        Plan.Begin(Plan.Scope);
        if (control.IsVisible)
        {
            writeVisible(control, parent);
        }
        else
        {
            using (LocalBindings.Override(control, Visual.IsVisibleProperty, true))
            {
                Settled.Change(control, static () => { });
                writeVisible(control, parent);
            }

            Settled.Change(control, static () => { });
        }

        Plan.Add(new ConditionNode(visible.Getter, Plan.End()) { Source = visible.Spec });
        return true;
    }

    private void CheckPlannable(Control control)
    {
        if (control.TemplatedParent is not null)
        {
            return;
        }

        if (BindingLimits.Unreproducible(control) is { } reason)
        {
            Plan.Fail(reason);
        }

        foreach (var property in Unplannable.Where(property => Plan.ReadsModel(control, property) && !BoundStyles.Paints(control, property) && !ContentTexts.Followed(control, property) && !LibraryValues.MayLearn(control, property) && !TemplateBindings.ShownByTemplate(control, property)))
        {
            Plan.Fail($"{control.GetType().Name}.{property.Name} is bound");
        }
    }
}
