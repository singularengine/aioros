using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The scroller module of the framework's script, loaded only by pages whose select lists carry scroll buttons
/// (<c>data-aioros-scroller</c>): as Radix Select's scroll buttons do, while the pointer rests on a shown button the list
/// scrolls every 50 ms by the selected option's height (the first enabled option's when none is selected or the selected
/// one is a hidden placeholder), the first shown option's scroll margin placing it that much higher or lower, until the
/// pointer leaves the button or the button hides at the list's end. When the list opens, its focused option is scrolled into view the nearest way from the list's start, as
/// Radix Select focuses the selected item, instead of the browser's centering, the scroll-up element's height kept for it
/// as it shows once the list has left its start.
/// </summary>
public static class ScrollerScript
{
    public const string Prefix = "/_aioros/scroller/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("scroller"));

    /// <summary>The module's source, as it is served.</summary>
    public static ReadOnlyMemory<byte> Bytes => Module.Bytes;

    /// <summary>The module's path, named by its hash.</summary>
    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
