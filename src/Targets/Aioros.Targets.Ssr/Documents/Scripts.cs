using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The framework's script, served as a file named by its hash and cached for a year (<c>immutable</c>), as Next.js,
/// Blazor and Turbo serve their runtimes, and referenced from each document's head with <c>defer</c>: it downloads
/// while the page is parsed and runs once it is, and a visitor's later documents no longer carry it.
/// </summary>
public static class Scripts
{
    public const string Prefix = "/_aioros/app/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("app")
        .Replace("/_aioros/morph.js", MorphScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/pointer.js", PointerScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/overlay.js", OverlayScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/keys.js", KeyboardScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/split.js", SplitScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/carousel.js", CarouselScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/filter.js", FilterScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/flags.js", FlagScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/scroll.js", ScrollScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/copy.js", CopyScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/choice.js", ChoiceScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/modal.js", ModalScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/range.js", RangeScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/scroller.js", ScrollerScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/swipe.js", SwipeScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/answer.js", AnswerScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/draft.js", DraftScript.Path, StringComparison.Ordinal)
        .Replace("/_aioros/reveal.js", RevealScript.Path, StringComparison.Ordinal));

    /// <summary>The script's source, as it is served.</summary>
    public static ReadOnlyMemory<byte> Bytes => Module.Bytes;

    public static string Path => Module.Path;

    /// <summary>The element that loads it, in a document's head.</summary>
    public static string Tag => "<script src=\"" + Path + "\" defer></script>\n";

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);



    public static readonly string OpenInApp = ScriptSources.Read("open");

    public static readonly string Development = "<script>\n" + ScriptSources.Read("development") + "</script>\n\n";

}
