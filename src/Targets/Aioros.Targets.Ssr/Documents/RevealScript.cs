using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The reveal module of the framework's script, loaded only by pages holding a control that plays its animations when it
/// first scrolls into view (<c>data-aioros-reveal</c>, <c>a:Web.Reveal</c>) or a number that counts up to its value then
/// (<c>data-aioros-count</c>, <c>a:Web.CountUp</c>, written in the text's own format, MOTION-28), a card whose glow
/// follows the pointer (<c>data-aioros-spotlight</c>, its <c>--x</c> and <c>--y</c> set as the pointer moves, MOTION-32),
/// or a layer that moves against the scroll (<c>data-aioros-parallax</c>, shifted with CSS <c>translate</c>, MOTION-34): an element outside the viewport when the page
/// opens has its animations paused at their start, its first key frame held by their fill, and played once when a tenth of
/// the viewport above its bottom edge reaches it (Framer Motion's <c>whileInView</c> with <c>once</c>); an element in view
/// plays as the page opens. Without the script every animation plays as the page opens, so nothing stays hidden.
/// </summary>
public static class RevealScript
{
    public const string Prefix = "/_aioros/reveal/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("reveal"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
