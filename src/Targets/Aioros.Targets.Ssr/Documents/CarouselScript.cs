using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The carousel module of the framework's script, loaded only by pages that declare carousels
/// (<c>data-aioros-carousel</c>): its step buttons (<c>data-aioros-slide</c>), its indicators
/// (<c>data-aioros-slide-to</c>), ArrowLeft and ArrowRight with the focus inside it, and a scroll of its strip by hand
/// select a slide; the strip scrolls to it (smoothly unless motion is reduced), the slide becomes <c>sel</c>, the hidden
/// field of a bound selection moves into it with its index and announces the change, the indicators swap their looks
/// and <c>aria-current</c>, and the step buttons are disabled at the ends unless the carousel wraps. A carousel marked
/// <c>data-aioros-drag</c> follows the mouse dragged along its strip as Embla's carousel does: a flick (a drag that moved
/// faster than 0.1 px/ms over its last 170 ms) pushes it 300 times its speed; a push past a fifth of the strip (between 50
/// and 225 px) goes to the next slide that way, a lesser one to the slide nearest where it would land, and a drag past
/// 10 px does not click.
/// </summary>
public static class CarouselScript
{
    public const string Prefix = "/_aioros/carousel/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("carousel"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
