using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The overlay module of the framework's script, loaded only by pages holding a library's overlay marked for the browser
/// (<c>dialog[data-aioros-modal]</c>, Aioros.Cosmo's dialogs and sheets): the browser opens and closes the dialog with its
/// invoker commands (<c>commandfor</c>, <c>command="show-modal"</c> and <c>"close"</c>), which the module carries out
/// where the browser lacks them, and closes it on Escape; a click on its mask (<c>.pm</c>) closes it when it is marked
/// <c>data-aioros-dismiss</c>. As it opens or closes, its state input (the control state its <c>data-aioros-modal</c>
/// names) follows, its content's <c>fieldset</c> is enabled only while it is open (so a closed dialog's fields are not
/// posted), enabled already as it opens, and the focus moved to its first field when the browser left it on the box of
/// its content (a sliding surface makes that box scroll for a moment), and the hidden check box of a dialog marked <c>data-change</c> follows and is posted as a change.
/// </summary>
public static class ModalScript
{
    public const string Prefix = "/_aioros/modal/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("modal"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
