using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The swipe module of the framework's script, loaded only by pages holding a deck of notices
/// (<c>.notice[data-aioros-deck]</c>) or an element a visitor swipes away (<c>data-aioros-swipe</c>). A deck keeps its
/// stack in the browser as sonner's toaster does: collapsed, its cards behind the newest stand the gap apart each, shrunk
/// by its step each, as tall as the newest and marked <c>data-behind</c>, hidden past its visible count; expanded while
/// the pointer is over it (or always, <c>data-aioros-expanded</c>), they stand the gap apart at their own heights; their
/// expiry stops while it is expanded, pressed or the page hidden; a card that leaves (expired, swiped, its close button or
/// its action pressed) is taken out once its exit has played and the stack closes up; a later answer's cards join the
/// stack in front (<see cref="Scripts"/> hands them over instead of morphing the deck, and keeps the page sheet a card came
/// with, marked <c>data-aioros-kept</c>, until its last card is taken out); heights are the cards' as the browser lays them out. A swiped element follows the
/// pointer, dampened against its directions: a card as sonner's toast does, let go past its distance or faster than its
/// velocity and leaving the way it went; a dialog's surface as vaul's drawer does, its mask fading as it goes, closing the
/// dialog from where it stands or moving back, unless the press was in a scrolled area of it. Text is not selected and not
/// dragged while it is swiped.
/// </summary>
public static class SwipeScript
{
    public const string Prefix = "/_aioros/swipe/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("swipe"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
