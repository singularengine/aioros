using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The overlay module of the framework's script, loaded only by pages that declare an overlay behavior
/// (<c>data-aioros-overlay</c>): the WAI-ARIA menu pattern for menu popovers and menu bars (the focus on the first item
/// when a menu opens, the arrows without wrapping, Home, End, typeahead, sub-menus with the arrows and with the pointer
/// after a short rest, the focus following the pointer), context menus opened at the pointer (<c>contextmenu</c>,
/// Shift+F10, the menu key) and kept in the view, hover cards opened after their open delay and closed after their close
/// delay, flyouts a click of their control opens, buttons whose flyout also opens on hover (<c>data-hover-open</c>, Radix's
/// NavigationMenu: after their open delay, at once while another of their row is open, closed after their close delay once
/// the pointer leaves the button and its popover, not reopened by the pointer after a click closed it, and, marked
/// <c>data-switch</c>, sliding in from the side of the one they replace while it slides out, Radix's <c>data-motion</c>), and tool tips
/// hidden by Escape with the show delay skipped for a tip shown within the between delay of another. The popover API gives
/// the rest: light dismiss, Escape, the top layer and the focus given back.
/// </summary>
public static class OverlayScript
{
    public const string Prefix = "/_aioros/overlay/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("overlay"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
