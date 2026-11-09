using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The flag module of the framework's script, loaded only by pages that hold a button switching a library panel's flag
/// (<c>data-aioros-flips</c>, Aioros.Cosmo's sidebar trigger and rail): a click on it switches the <c>data-aioros-on</c>
/// mark of the panel around it whose <c>data-aioros-flag</c> it names, with no request, so the panel's children take the
/// looks the plan wrote for the other value; the panel's flipping buttons follow it with <c>aria-expanded</c>, and the
/// hidden check box of a flag bound two way follows it too and is posted as a change, and a panel naming a cookie
/// (<c>data-aioros-cookie</c>) keeps its state there for the whole site and seven days, as shadcn/ui's sidebar does; an
/// expander that follows the flag (<c>data-aioros-follows</c>) opens and closes with it, and switches it as it is toggled.
/// Below a panel's breakpoint (<c>data-aioros-breakpoint</c>) the same buttons open and close its sheet instead, the
/// <c>&lt;dialog data-aioros-sheet&gt;</c> shown as a modal (Escape closing it), and a sheet left open when the viewport
/// widens past the breakpoint is closed, as shadcn/ui's sidebar turns its sheet back into the column.
/// </summary>
public static class FlagScript
{
    public const string Prefix = "/_aioros/flags/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("flag"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
