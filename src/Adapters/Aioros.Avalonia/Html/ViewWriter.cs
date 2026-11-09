using Aioros.Avalonia.Html.Templating;
using System.Collections.Immutable;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html;

/// <summary>
/// Writes a view in its region, reading its own model, through a <see cref="ViewNode"/> in Avalonia's render and in the
/// plan alike, so both number the region's field ids under the view: Avalonia's render writes what the view shows (its
/// error template when it failed, nothing when hidden), and the plan records the view's body, which a view that is not
/// loaded for this render cannot give, so that plan is not kept.
/// </summary>
internal sealed class ViewWriter(HtmlContext context)
{
    private readonly HashSet<Control> _viewing = [];

    private PlanRecorder Plan => context.Plan;

    /// <summary>What a view's region writes when its loader failed: the view error template, recorded once per plan.</summary>
    public ImmutableArray<IPlanNode> ViewError { get; private set; } = [];

    /// <summary>Records the view error template, already shown and styled, as the error body of every view of the plan.</summary>
    public void RecordViewError(Control error)
    {
        Plan.Begin(null);
        context.Write(error, ParentKind.Single);
        ViewError = Plan.End();
    }

    /// <summary>Writes the control as a view when it is the view of one of the render's models and not already being written as one.</summary>
    public bool Write(Control control, ParentKind parent)
    {
        var slot = View.GetSlot(control);
        if (slot < 0 || slot >= context.Models.Length || !_viewing.Add(control))
        {
            return false;
        }

        try
        {
            Write(control, parent, slot);
        }
        finally
        {
            _viewing.Remove(control);
        }

        return true;
    }

    private void Write(Control control, ParentKind parent, int slot)
    {
        var item = View.GetItem(control);
        var model = item is not null && context.Models[slot] is ViewItems items ? items.Model(item) : context.Models[slot];
        var key = View.GetKey(control);
        if (Plan.Planning && model is ViewState state)
        {
            Plan.Fail($"the view '{key}' is {state.Name} for this request, so its plan is recorded on a later request");
        }

        if (Plan.Planning && item is not null && !ReferenceEquals(Plan.Scope, control.GetValue(View.ListItemProperty)))
        {
            Plan.Fail($"the view '{key}' repeated in a list is not placed in its item's data context, so the plan cannot find its item");
        }

        if (ReferenceEquals(model, ViewState.Hidden))
        {
            return;
        }

        context.Fields.EnterView(slot, item);
        try
        {
            if (ReferenceEquals(model, ViewState.Failed))
            {
                Plan.Begin(null);
                if ((control as ContentControl)?.Content is Control error)
                {
                    context.Write(error, ParentKind.Single);
                }

                Plan.Add(new ViewNode(slot, key, [], Plan.End()) { Item = item });
                return;
            }

            if (Plan.Planning)
            {
                Plan.BeginView(model);
                context.Write(control, parent);
                context.WriteShape();
                Plan.Add(new ViewNode(slot, key, Plan.EndView(), ViewError));
                return;
            }

            Plan.Begin(model);
            context.Write(control, parent);
            context.WriteShape();
            Plan.Add(new ViewNode(slot, key, Plan.End(), []) { Item = item });
        }
        finally
        {
            context.Fields.LeaveView();
        }
    }
}
