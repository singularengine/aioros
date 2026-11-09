using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The choice module of the framework's script, loaded only by pages holding an option of a list that chooses in the
/// browser (<c>data-aioros-chooses</c>, Aioros.Cosmo's combobox): a click on an option, or Enter on it in the list's
/// filter, chooses its index with no request, as cmdk's <c>onSelect</c> closes shadcn/ui's popover and as a native
/// <c>&lt;select&gt;</c> chooses. The list's shown part (<c>data-aioros-shows</c>) becomes its template for that index,
/// the option alone is <c>aria-checked</c>, the popover holding it closes, and the list's hidden field takes the index,
/// enabled so the next post carries it, and is posted at once as a change when it is marked <c>data-change</c> (a choice
/// bound two way). Choosing the chosen option again chooses none (-1) in a list marked <c>data-aioros-toggle</c>.
/// </summary>
public static class ChoiceScript
{
    public const string Prefix = "/_aioros/choice/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("choice"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
