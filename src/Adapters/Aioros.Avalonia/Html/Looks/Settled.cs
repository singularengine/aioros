using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>Changes a control with its transitions held, so each value lands at once as it does on a first render.</summary>
internal static class Settled
{
    /// <summary>
    /// Settles a control whose tree a change rebuilt: the jobs Avalonia posted run, what they built is styled and templated,
    /// and the layout runs again, so the new parts are what Avalonia shows.
    /// </summary>
    public static void Rebuilt(Control control)
    {
        Change(control, static () => { });
        Dispatcher.UIThread.RunJobs();
        ThemeLooks.ExpandTemplates(control);
        Change(control, static () => { });
    }

    public static void Change(Control control, Action change)
    {
        var held = control.GetVisualDescendants().Prepend(control).OfType<Animatable>()
            .Where(visual => visual.Transitions is not null)
            .Select(visual => visual.SetValue(Animatable.TransitionsProperty, null, BindingPriority.Animation))
            .ToList();
        change();
        TopLevel.GetTopLevel(control)?.UpdateLayout();
        held.ForEach(lease => lease?.Dispose());
    }
}
