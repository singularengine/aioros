using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Panels;

/// <summary>
/// A scroll viewer that follows its end (<c>a:Web.FollowsEnd</c>) and the buttons that scroll a viewer to its end
/// (<c>a:Web.ScrollsToEnd</c>): the viewer is a scroll container whose scroll origin is its end
/// (<c>flex-direction:column-reverse</c>, its content kept at its top while it fits), so it opens there and stays there while its
/// content grows until the visitor scrolls away, with no script; a viewer a button names carries that name, and the
/// button its viewer's, for the scroll module that scrolls it and hides the button while the viewer is at its end.
/// </summary>
internal static class ScrollEnds
{
    private const string Css = "[data-aioros-scrolls][data-aioros-at-end]{display:none!important}\n";

    /// <summary>
    /// The class of a viewer that follows its end: its scroll origin at its end (<c>column-reverse</c>), a spacer before
    /// its content keeping a content shorter than the viewer at its top, where Avalonia lays it.
    /// </summary>
    public static string? Layout(ScrollViewer scroll, StyleSheet sheet)
    {
        if (!Web.GetFollowsEnd(scroll))
        {
            return null;
        }

        sheet.AddRule("ff", ".ff{flex-direction:column-reverse}.ff::before{content:\"\";flex:1 0 0}.ff.ff>*{flex-grow:0}\n");
        return "ff";
    }

    /// <summary>The marks of a viewer that follows its end or a button names, and of a button that scrolls one.</summary>
    public static string Attributes(Control control, StyleSheet sheet)
    {
        if (Web.GetScrollsToEnd(control) is { Length: > 0 } target)
        {
            sheet.AddRule("sae", Css);
            return $" data-aioros-scrolls=\"{CssValues.Encode(target)}\"";
        }

        if (control is not ScrollViewer scroll)
        {
            return string.Empty;
        }

        var follow = Web.GetFollowsEnd(scroll) ? " data-aioros-follow" : string.Empty;
        return scroll.Name is { Length: > 0 } name && Named(scroll, name) ? $"{follow} data-aioros-scroll-name=\"{CssValues.Encode(name)}\"" : follow;
    }

    /// <summary>Whether a button of the viewer's template, or of its page, scrolls it by its name.</summary>
    private static bool Named(ScrollViewer scroll, string name) =>
        (scroll.TemplatedParent as Visual ?? TopLevel.GetTopLevel(scroll) as Visual ?? scroll.GetVisualAncestors().LastOrDefault()) is { } scope
        && scope.GetVisualDescendants().OfType<Control>().Any(control => Web.GetScrollsToEnd(control) == name);
}
