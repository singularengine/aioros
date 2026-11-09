using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The filter module of the framework's script, loaded only by pages that hold a list filtered as the visitor types
/// (<c>data-aioros-filter</c>, a command menu): cmdk 1.1.1's behavior in the browser, without a request. Each input
/// event scores every option of the list (<c>[role=option]</c>, by its <c>data-value</c> or its text) with cmdk's
/// <c>command-score</c>, hides those that do not match, the groups left empty and the separators while a search is
/// typed (an option's shortcuts, its <c>kbd</c> elements, read after its text, as cmdk reads shadcn's <c>CommandShortcut</c>), orders the options and the groups by their best score (their own order back once the search is cleared),
/// shows the list's empty part when nothing matches and selects the first enabled option (<c>aria-selected</c>, the
/// input's <c>aria-activedescendant</c>), scrolling only the list to keep a selection moved by a key or a search in view,
/// never the page, and nothing on the first pass. Down and Up (Ctrl+N or Ctrl+J, Ctrl+P or Ctrl+K), with Alt to the next or
/// previous group and Meta to the ends, Home and End move the selection, the pointer selects the option it moves over,
/// and Enter clicks the selected option, which posts its command as a click does.
/// </summary>
public static class FilterScript
{
    public const string Prefix = "/_aioros/filter/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("filter"));

    public static string Path => Module.Path;

    public static ReadOnlyMemory<byte> Bytes => Module.Bytes;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
