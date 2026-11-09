using Avalonia;
using Avalonia.Controls;

namespace Aioros.Markup;

/// <summary>The hints of a library's overlays opened and closed in the browser.</summary>
public sealed partial class Web
{
    /// <summary>
    /// Set on a library's overlay owner (Aioros.Cosmo's <c>c:Dialog</c>): the name of its own <c>bool</c> property that
    /// opens its overlay. On the web the overlay is written into the page while closed, in a <c>&lt;dialog&gt;</c> that
    /// buttons marked <see cref="OpensProperty"/> open and buttons marked <see cref="ClosesProperty"/> close in the
    /// browser with no request (HTML's invoker commands), its open state travelling with the page's posts and posted as
    /// a change when the property is bound two way. The controls the library adds over the page for it carry
    /// <see cref="ModalOfProperty"/>. The native targets ignore it.
    /// </summary>
    public static readonly AttachedProperty<string?> ModalProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Modal");

    /// <summary>
    /// Set by a library on each control it adds over the page while an overlay owner marked <see cref="ModalProperty"/>
    /// is open (its mask, its surface): that owner.
    /// </summary>
    public static readonly AttachedProperty<Control?> ModalOfProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, Control?>("ModalOf");

    /// <summary>
    /// Set by Aioros on an overlay owner marked <see cref="ModalProperty"/> while it writes the owner's closed overlay for
    /// the web: the library shows the overlay as it shows it open, without changing its open property, so a model bound to
    /// that property is never written. A library that ignores it has its property set above its binding instead.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowsProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("Shows");

    /// <summary>Set on a button: the overlay owner marked <see cref="ModalProperty"/> that it opens in the browser (Radix's <c>DialogTrigger</c>).</summary>
    public static readonly AttachedProperty<Control?> OpensProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, Control?>("Opens");

    /// <summary>Set on a button inside an overlay marked <see cref="ModalProperty"/>: it closes the overlay in the browser (Radix's <c>DialogClose</c>).</summary>
    public static readonly AttachedProperty<bool> ClosesProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("Closes");

    /// <summary>Set on an overlay owner marked <see cref="ModalProperty"/>: a click on its mask closes it in the browser, as the library's light dismiss does.</summary>
    public static readonly AttachedProperty<bool> LightDismissProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("LightDismiss");

    /// <summary>
    /// Set on an overlay owner marked <see cref="ModalProperty"/> that no button opens (Aioros.Cosmo's
    /// <c>c:CommandDialog</c> without a trigger): the key that opens and closes it anywhere in the page, as cmdk's dialog
    /// listens for ⌘K. On the web its <c>&lt;dialog&gt;</c> carries the key and the framework's keyboard module opens it
    /// modal or closes it with no request, Meta standing for Ctrl. The native targets ignore it.
    /// </summary>
    public static readonly AttachedProperty<Avalonia.Input.KeyGesture?> ModalKeyProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, Avalonia.Input.KeyGesture?>("ModalKey");

    /// <summary>Gets <see cref="ModalKeyProperty"/>.</summary>
    public static Avalonia.Input.KeyGesture? GetModalKey(Control control) => control.GetValue(ModalKeyProperty);

    /// <summary>Sets <see cref="ModalKeyProperty"/>.</summary>
    public static void SetModalKey(Control control, Avalonia.Input.KeyGesture? value) => control.SetValue(ModalKeyProperty, value);

    /// <summary>Gets <see cref="ModalProperty"/>.</summary>
    public static string? GetModal(Control control) => control.GetValue(ModalProperty);

    /// <summary>Sets <see cref="ModalProperty"/>.</summary>
    public static void SetModal(Control control, string? value) => control.SetValue(ModalProperty, value);

    /// <summary>Gets <see cref="ModalOfProperty"/>.</summary>
    public static Control? GetModalOf(Control control) => control.GetValue(ModalOfProperty);

    /// <summary>Sets <see cref="ModalOfProperty"/>.</summary>
    public static void SetModalOf(Control control, Control? value) => control.SetValue(ModalOfProperty, value);

    /// <summary>Gets <see cref="ShowsProperty"/>.</summary>
    public static bool GetShows(Control control) => control.GetValue(ShowsProperty);

    /// <summary>Sets <see cref="ShowsProperty"/>.</summary>
    public static void SetShows(Control control, bool value) => control.SetValue(ShowsProperty, value);

    /// <summary>Gets <see cref="OpensProperty"/>.</summary>
    public static Control? GetOpens(Control control) => control.GetValue(OpensProperty);

    /// <summary>Sets <see cref="OpensProperty"/>.</summary>
    public static void SetOpens(Control control, Control? value) => control.SetValue(OpensProperty, value);

    /// <summary>Gets <see cref="ClosesProperty"/>.</summary>
    public static bool GetCloses(Control control) => control.GetValue(ClosesProperty);

    /// <summary>Sets <see cref="ClosesProperty"/>.</summary>
    public static void SetCloses(Control control, bool value) => control.SetValue(ClosesProperty, value);

    /// <summary>Gets <see cref="LightDismissProperty"/>.</summary>
    public static bool GetLightDismiss(Control control) => control.GetValue(LightDismissProperty);

    /// <summary>Sets <see cref="LightDismissProperty"/>.</summary>
    public static void SetLightDismiss(Control control, bool value) => control.SetValue(LightDismissProperty, value);
}
