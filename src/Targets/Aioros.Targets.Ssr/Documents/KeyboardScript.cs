using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The keyboard module of the framework's script, loaded only by pages that declare tabs (<c>data-aioros-tabs</c>), key
/// shortcuts (<c>data-aioros-key</c>, <c>data-aioros-keys</c>) or calendars (<c>data-aioros-calendar</c>): the WAI-ARIA tabs pattern with
/// automatic activation (ids, <c>aria-controls</c> and <c>aria-labelledby</c>, a click or the arrows, Home and End
/// selecting a tab and showing its panel, wrapping and skipping disabled tabs, the selected index posted through its
/// hidden input), and shortcuts that click the button they name (or open and close the dialog of a library's overlay
/// they name, <c>a:Web.ModalKey</c>): a scoped one (<c>data-aioros-scope</c>, written right before its scope's element) while the focus is
/// inside that element, the innermost first, then a document-level one; a disabled one is skipped; Meta stands for
/// Ctrl, and a key without Ctrl, Meta or Alt is ignored in a text field; and a calendar's day grid as the WAI-ARIA date
/// grid (the arrows, Home and End moving the focus over the shown days and the roving tab stop, Ctrl or Meta held on a
/// click written into the calendar's hidden field for the post), and Enter in a date picker's field posting it as a change
/// rather than also letting the browser's implicit submission press the form's first button; and the keys of a region
/// (<c>data-aioros-keyscope</c>, <c>a:Web.KeyScope</c>): while the focus is inside it, a key clicks the first shown, enabled
/// button of that region (not of a region inside it) listing it in <c>data-aioros-keys</c> (<c>a:Web.Keys</c>), a key without
/// Ctrl, Meta or Alt ignored in a text field and an arrow on a radio button, and Enter taken from a choice or a text field
/// (the browser's implicit submission prevented) but answering only from a checked choice or a filled text field, as
/// shadcn/ui's questionnaire answers its keys.
/// </summary>
public static class KeyboardScript
{
    public const string Prefix = "/_aioros/keys/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("keyboard"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
