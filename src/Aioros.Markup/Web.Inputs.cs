using Avalonia;
using Avalonia.Controls;

namespace Aioros.Markup;

public sealed partial class Web
{
    /// <summary>
    /// The member path that identifies an item of the list it is set on (<c>a:Web.ItemKey="Code"</c>): the fields and
    /// buttons of each item carry its key, so a post from a page whose list has since changed is refused rather than
    /// applied to another item. Without it an item is identified by its <c>Id</c>, or by itself when it is a text, a number or an enum.
    /// </summary>
    public static readonly AttachedProperty<string?> ItemKeyProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("ItemKey");

    /// <summary>Gets <see cref="ItemKeyProperty"/>.</summary>
    /// <param name="control">The list.</param>
    /// <returns>The member path, or null.</returns>
    public static string? GetItemKey(Control control) => control.GetValue(ItemKeyProperty);

    /// <summary>Sets <see cref="ItemKeyProperty"/>.</summary>
    /// <param name="control">The list.</param>
    /// <param name="value">The member path.</param>
    public static void SetItemKey(Control control, string? value) => control.SetValue(ItemKeyProperty, value);

    /// <summary>
    /// Set on a panel holding two sliders, the lower and the upper value of one range (a library's two-thumb slider drawn
    /// as two sliders sharing a track, as Aioros.Cosmo's <c>c:RangeSlider</c>): on the web the panel takes the pointer and
    /// the keys of both as Radix Slider does (a press moves the closest thumb, the values stay ordered, Shift and the page
    /// keys step by ten, Home and End move the first and the last thumb), each thumb can be dragged without the script, and
    /// a post that carries the two values crossed binds them in order.
    /// </summary>
    public static readonly AttachedProperty<bool> RangeProperty =
        AvaloniaProperty.RegisterAttached<Web, Panel, bool>("Range");

    /// <summary>
    /// Set on a <c>Popup</c>, the distance its border keeps from the viewport's edges on the web, as Radix Popper's
    /// <c>collisionPadding</c> (Radix Select's 10 px; Avalonia's positioner keeps none, so a library keeps it on the desktop
    /// itself, as Aioros.Cosmo's select does): the popover shifts along its anchor no nearer to the edges across its side.
    /// </summary>
    public static readonly AttachedProperty<double> CollisionPaddingProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("CollisionPadding");

    /// <summary>
    /// Set in a <c>ComboBox</c>'s popup template on a control above or below its list's scroll viewer, <c>Up</c> or
    /// <c>Down</c> (Radix Select's scroll buttons, Aioros.Cosmo's <c>c:SelectScrollUpButton</c>): on the web the list shows
    /// it while it can scroll that way and scrolls an option at a time every 50 ms while the pointer rests on it. Any
    /// other value is ignored.
    /// </summary>
    public static readonly AttachedProperty<string?> ScrollButtonProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("ScrollButton");

    /// <summary>
    /// Set on a <c>ComboBox</c> (or in its theme) whose list is the browser's own on the web, as an HTML select's native
    /// picker (shadcn/ui's <c>NativeSelect</c>, Aioros.Cosmo's <c>native</c> class): its field keeps the theme's look and
    /// the native select's width (its widest option), and its popup is not written as a customizable select's picker.
    /// </summary>
    public static readonly AttachedProperty<bool> NativeListProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("NativeList");

    /// <summary>Gets <see cref="CollisionPaddingProperty"/>.</summary>
    public static double GetCollisionPadding(Control control) => control.GetValue(CollisionPaddingProperty);

    /// <summary>Sets <see cref="CollisionPaddingProperty"/>.</summary>
    public static void SetCollisionPadding(Control control, double value) => control.SetValue(CollisionPaddingProperty, value);

    /// <summary>Gets <see cref="NativeListProperty"/>.</summary>
    public static bool GetNativeList(Control control) => control.GetValue(NativeListProperty);

    /// <summary>Sets <see cref="NativeListProperty"/>.</summary>
    public static void SetNativeList(Control control, bool value) => control.SetValue(NativeListProperty, value);

    /// <summary>Gets <see cref="RangeProperty"/>.</summary>
    public static bool GetRange(Panel panel) => panel.GetValue(RangeProperty);

    /// <summary>Sets <see cref="RangeProperty"/>.</summary>
    public static void SetRange(Panel panel, bool value) => panel.SetValue(RangeProperty, value);

    /// <summary>Gets <see cref="ScrollButtonProperty"/>.</summary>
    public static string? GetScrollButton(Control control) => control.GetValue(ScrollButtonProperty);

    /// <summary>Sets <see cref="ScrollButtonProperty"/>.</summary>
    public static void SetScrollButton(Control control, string? value) => control.SetValue(ScrollButtonProperty, value);
}
