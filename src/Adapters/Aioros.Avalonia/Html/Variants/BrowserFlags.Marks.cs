using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Markup;
using Aioros.Runtime.Rendering;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>The marks a flagged panel gives what it holds in one value only: its tips, and its expanders that follow it.</summary>
internal sealed partial class BrowserFlags
{
    /// <summary>
    /// The marks of an expander open in one value of a panel's flag and closed in the other (a step bound to the panel's
    /// state, Aioros.Cosmo's trace that a button outside it opens): the flag it follows and the value it is open in, so the
    /// framework's script opens and closes its <c>&lt;details&gt;</c> as the flag switches and switches the flag as it is toggled.
    /// </summary>
    public string Follows(Expander expander)
    {
        if (!Final || !_expanded.TryGetValue(expander, out var values))
        {
            return string.Empty;
        }

        for (var depth = 0; depth < _frames.Count; depth++)
        {
            if (_frames[depth].Flag is not { } flag || !values.TryGetValue(Swap(Key, depth, Other), out var other) || other == expander.IsExpanded)
            {
                continue;
            }

            var openOn = _frames[depth].On ? expander.IsExpanded : other;
            return $" data-aioros-follows=\"{CssValues.Encode(flag.Name)}\" data-aioros-open-on=\"{(openOn ? "true" : "false")}\"";
        }

        return string.Empty;
    }

    /// <summary>The tool tip the library sets on a control of the panel being written only in another value of a flag (the icon sidebar's buttons' tips), or nothing.</summary>
    public object? OtherTip(Control control) =>
        Final && ToolTip.GetTip(control) is null && _tips.TryGetValue(control, out var tips) && tips.Count > 0 ? tips.Values.First() : null;

    /// <summary>
    /// The class of a tool tip set in one value of a panel's flag only: its rule hides the tip while the panel's mark is
    /// in the value where the library sets none, as shadcn/ui's sidebar renders its buttons' tips hidden unless collapsed.
    /// </summary>
    public string? TipMarker(Control control)
    {
        if (!Final || !_looks.ContainsKey(control))
        {
            return null;
        }

        var here = ToolTip.GetTip(control) is not null;
        var tips = _tips.GetValueOrDefault(control);
        for (var depth = 0; depth < _frames.Count; depth++)
        {
            if (_frames[depth].Flag is null || here == (tips?.ContainsKey(Swap(Key, depth, Other)) == true))
            {
                continue;
            }

            var frame = _frames[depth];
            var name = frame.Flag!.Name;
            var shownOn = here ? frame.On : !frame.On;
            var marker = "f" + CssValues.Hash($"{name}|tip|{shownOn}");
            var state = shownOn ? ":not([data-aioros-on])" : "[data-aioros-on]";
            context.Sheet.AddRule(marker, $":where([data-aioros-flag=\"{name}\"]{state}) .{marker}>.tt{{display:none}}\n");
            return marker;
        }

        return null;
    }
}
