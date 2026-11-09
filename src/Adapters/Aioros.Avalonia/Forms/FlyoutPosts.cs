using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;

namespace Aioros.Avalonia.Forms;

/// <summary>
/// A flyout a post came from: its content's control was pressed or changed, which the visitor could do only while the
/// browser showed it, so the answer writes it open again (an answer hides every popover and reopens those written open),
/// as Radix's popover stays open while its content is used and as Aioros's date picker keeps its popup after a month.
/// </summary>
internal static class FlyoutPosts
{
    private static readonly AttachedProperty<FlyoutBase?> OwnerProperty = AvaloniaProperty.RegisterAttached<Control, FlyoutBase?>("Owner", typeof(FlyoutPosts));

    private static readonly AttachedProperty<bool> KeptProperty = AvaloniaProperty.RegisterAttached<FlyoutBase, bool>("Kept", typeof(FlyoutPosts));

    /// <summary>Names the flyout whose content is being written, so a post from it is known.</summary>
    public static void Owns(Control content, FlyoutBase flyout) => content.SetValue(OwnerProperty, flyout);

    public static bool Kept(FlyoutBase flyout) => flyout.GetValue(KeptProperty);

    /// <summary>
    /// Keeps open the flyout whose content holds the control a post pressed or changed, unless the control is a button
    /// running a command, which closes it as a menu item closes Avalonia's <c>MenuFlyout</c> and a link closes Radix's
    /// navigation menu (Aioros.Cosmo's closes on a chosen link).
    /// </summary>
    public static void From(Control control)
    {
        if (control is Button { Command: not null })
        {
            return;
        }

        for (ILogical? node = control; node is not null; node = node.LogicalParent)
        {
            if (node is Control part && part.GetValue(OwnerProperty) is { } flyout)
            {
                flyout.SetValue(KeptProperty, true);
                return;
            }
        }
    }
}
