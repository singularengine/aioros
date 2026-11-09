using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// The overlays a control carries besides its tool tip, written around it: its context menu (<c>ContextMenu</c> or
/// <c>ContextFlyout</c>) as a menu popover the overlay module opens at the pointer on <c>contextmenu</c> (a right click,
/// a long press, Shift+F10 or the menu key), and its attached flyout (<c>FlyoutBase.AttachedFlyout</c>): one shown
/// proactively (<c>Transient</c> or <c>TransientWithDismissOnPointerMoveAway</c>, WinUI's show modes of a flyout the
/// user did not ask for, a hover card) opens while the pointer rests
/// on the control or it has the keyboard focus, after the control's <c>OpenDelay</c> and until its <c>CloseDelay</c>
/// once the pointer left both (attached properties of those names, as a library declares them; Radix's 700 and 300 ms
/// without them), and any other opens on a click of the control, where Avalonia's app opens it from a tap.
/// </summary>
internal static class AttachedWriters
{
    private const string Css =
        ".ctx,.hv{display:contents}.hv{anchor-scope:--hv}.hv>:first-child:not(.tip),.hv>.tip>:first-child{anchor-name:--hv}" +
        "[popover].cm{position:fixed;inset:auto;left:var(--aioros-x,0);top:var(--aioros-y,0);margin:0}\n";

    public static bool Attaches(Control control) =>
        Menu(control) || FlyoutBase.GetAttachedFlyout(control) is PopupFlyoutBase;

    /// <summary>
    /// Whether the control has a context menu of its app's: not one its control theme gives every such control (Fluent's
    /// text box has cut, copy and paste, its scroll bar its scroll commands), which the browser's own menu replaces.
    /// </summary>
    private static bool Menu(Control control) =>
        (control.ContextMenu is not null && Own(control, Control.ContextMenuProperty)) || (control.ContextFlyout is PopupFlyoutBase && Own(control, Control.ContextFlyoutProperty));

    private static bool Own(Control control, AvaloniaProperty property) =>
        control.GetDiagnostic(property).Priority == BindingPriority.LocalValue || !Sets(control.Theme ?? control.FindResource(control.GetType()) as ControlTheme, property);

    private static bool Sets(ControlTheme? theme, AvaloniaProperty property) =>
        theme is not null && (Sets(theme.Setters, theme.Children, property) || Sets(theme.BasedOn, property));

    private static bool Sets(IEnumerable<SetterBase> setters, IEnumerable<IStyle> children, AvaloniaProperty property) =>
        setters.OfType<Setter>().Any(setter => setter.Property == property) || children.OfType<Style>().Any(style => Sets(style.Setters, style.Children, property));

    public static void Write(HtmlContext context, Control control, Action write)
    {
        if (!Attaches(control))
        {
            write();
            return;
        }

        context.Sheet.AddRule("attached", Css);
        var menu = Menu(control) ? context.NextPopover() : null;
        var attached = FlyoutBase.GetAttachedFlyout(control) as PopupFlyoutBase;
        var card = attached is null ? null : context.NextPopover();
        if (menu is not null)
        {
            context.Plan.Literal($"<span class=\"ctx\" data-aioros-overlay data-aioros-context=\"{menu}\">");
        }

        var hover = attached is not null && attached.ShowMode != FlyoutShowMode.Standard;
        if (attached is not null)
        {
            var trigger = hover
                ? string.Create(CultureInfo.InvariantCulture, $"data-aioros-hover=\"{card}\" data-open=\"{Delay(control, "OpenDelay", 700)}\" data-close=\"{Delay(control, "CloseDelay", 300)}\"")
                : $"data-aioros-click=\"{card}\"";
            context.Plan.Literal($"<span class=\"hv\" data-aioros-overlay {trigger}>");
        }

        write();
        if (attached is not null)
        {
            WriteAttached(context, card!, attached, control, hover);
            context.Plan.Literal("</span>");
        }

        if (menu is not null)
        {
            WriteContext(context, menu, control);
            context.Plan.Literal("</span>");
        }
    }

    private static void WriteAttached(HtmlContext context, string id, PopupFlyoutBase flyout, Control control, bool hover)
    {
        var kind = hover ? "=\"hint\"" : string.Empty;
        switch (flyout)
        {
            case Flyout panel:
                FlyoutWriters.Opened(panel, control, presenter => FlyoutWriters.WriteFlyout(context, id, panel, control, presenter, kind, "--hv"));
                break;
            case MenuFlyout menu:
                FlyoutWriters.Opened(menu, control, presenter => MenuWriters.WritePopover(context, id, menu.Items, control, $"position-anchor:--hv;{Placements.Of(menu)}", presenter));
                break;
        }
    }

    private static void WriteContext(HtmlContext context, string id, Control control)
    {
        if (control.ContextMenu is { } menu)
        {
            Probes.With(control, menu, surface => MenuWriters.WritePopover(context, id, menu.Items, control, string.Empty, surface, "cm"));
            return;
        }

        switch (control.ContextFlyout)
        {
            case MenuFlyout flyout:
                FlyoutWriters.Opened(flyout, control, presenter => MenuWriters.WritePopover(context, id, flyout.Items, control, string.Empty, presenter, "cm"));
                break;
            case Flyout panel:
                FlyoutWriters.Opened(panel, control, presenter => FlyoutWriters.WriteFlyout(context, id, panel, control, presenter, string.Empty, null, "cm"));
                break;
        }
    }

    /// <summary>A delay in milliseconds a library declares on the control as an attached property of this name, or the default.</summary>
    private static int Delay(Control control, string name, int fallback) =>
        AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(control.GetType()).FirstOrDefault(property => property.Name == name) is { } property
            ? control.GetValue(property) switch
            {
                int milliseconds => milliseconds,
                double milliseconds => (int)milliseconds,
                TimeSpan time => (int)time.TotalMilliseconds,
                _ => fallback,
            }
            : fallback;
}
