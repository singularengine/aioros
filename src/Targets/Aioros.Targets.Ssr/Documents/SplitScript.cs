using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The split module of the framework's script, loaded only by pages that declare grid splitters
/// (<c>data-aioros-split</c>): the WAI-ARIA window splitter. A separator resizes the two tracks of its grid it names,
/// dragged with the pointer (captured) or moved with the arrows along its axis by its step (pixels, or a percentage of
/// the grid's star tracks), Home and End to its limits (its <c>aria-valuemin</c> and <c>aria-valuemax</c> and the
/// definitions' pixels), Enter collapsing the track before it and restoring it, F6 and Shift+F6 to the next and previous
/// separator of its grid; the grid's tracks are written as its inline style, kept by the morph, and
/// <c>aria-valuenow</c> and the separator's hidden field follow, the field firing <c>input</c> once a drag ends or a key
/// moves it, so a separator marked <c>data-delay</c> posts its position as a change.
/// </summary>
public static class SplitScript
{
    public const string Prefix = "/_aioros/split/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("split"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
