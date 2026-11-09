using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>
/// The ways a plan writes a control more than once, or in another scope, because a binding changes how Avalonia renders
/// it: content an app's template selector picks for, typed content, a bound data context, bound classes, bound values with a few possibilities, a bound tag that selectors read, enabled states and a library control's own values.
/// Each writes the control through <c>write</c> with the binding's value set, and the plan picks a case on each render.
/// </summary>
internal static class PlanExpansions
{
    public static bool Write(PlanRecorder plan, Control control, HashSet<(Control, AvaloniaProperty)> expanding, Action write)
    {
        if (control is ContentPresenter chosen && !expanding.Contains((control, ContentPresenter.ContentProperty)) && ChosenContent.Owner(chosen) is { } selecting)
        {
            expanding.Add((control, ContentPresenter.ContentProperty));
            ChosenContent.Write(plan, selecting, chosen, write);
            expanding.Remove((control, ContentPresenter.ContentProperty));
            return true;
        }

        if (control is ContentPresenter presenter && !expanding.Contains((control, ContentPresenter.ContentProperty)) && TypedContent.Owner(presenter) is { } owner)
        {
            expanding.Add((control, ContentPresenter.ContentProperty));
            TypedContent.Write(plan, owner, presenter, write);
            expanding.Remove((control, ContentPresenter.ContentProperty));
            return true;
        }

        if (control is ContentPresenter shown && !expanding.Contains((control, ContentPresenter.ContentProperty)) && TemplatedContents.Path(plan, shown) is { } path)
        {
            expanding.Add((control, ContentPresenter.ContentProperty));
            TemplatedContents.Write(plan, shown, path, write);
            expanding.Remove((control, ContentPresenter.ContentProperty));
            return true;
        }

        if (!expanding.Contains((control, StyledElement.DataContextProperty)) && plan.ContextPath(control) is { } context)
        {
            expanding.Add((control, StyledElement.DataContextProperty));
            plan.Begin(control.DataContext, plan.PathType(context));
            write();
            var source = new SlotSpec("one", context);
            plan.Add(new RepeatNode(Slots.Getter(source), plan.End()) { Source = source });
            expanding.Remove((control, StyledElement.DataContextProperty));
            return true;
        }

        if (LearnedValues.Next(control, expanding) is { } learned)
        {
            LearnedValues.WriteVariants(plan, control, learned, expanding, write);
            return true;
        }

        if (BoundClasses.Next(control, expanding) is { } bound)
        {
            BoundClasses.WriteVariants(plan, control, bound, expanding, write);
            return true;
        }

        if (ValueVariants.Next(control, expanding) is { } valued)
        {
            ValueVariants.WriteVariants(plan, control, valued, expanding, write);
            return true;
        }

        if (SelectedTags.Next(control, expanding))
        {
            SelectedTags.WriteVariants(plan, control, expanding, write);
            return true;
        }

        if (EnabledStates.Varies(control, expanding))
        {
            EnabledStates.WriteVariants(plan, control, expanding, write);
            return true;
        }

        if (LibraryValues.Candidates(control, expanding) is { Length: > 0 } library)
        {
            LibraryValues.Write(plan, control, library, expanding, write);
            return true;
        }

        return false;
    }
}
