using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The scroll module of the framework's script, loaded only by pages holding a button that scrolls a viewer to its end
/// (<c>data-aioros-scrolls</c>, Aioros.Cosmo's message scroller): a click scrolls the viewer the button names (the
/// nearest <c>data-aioros-scroll-name</c> around it) to its end, smoothly and with no request, and the button carries
/// <c>data-aioros-at-end</c>, which hides it, while that viewer is at its end, kept up to date as the viewer scrolls and
/// as it or its content changes size, as shadcn/ui's scroll-to-end button is active only away from the end. A following
/// viewer (<c>data-aioros-follow</c>) that was at its end when its content grows is brought back to it, as
/// <c>use-stick-to-bottom</c> re-pins the end, where the browser's scroll anchoring would keep the old content in view.
/// </summary>
public static class ScrollScript
{
    public const string Prefix = "/_aioros/scroll/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("scroll"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
