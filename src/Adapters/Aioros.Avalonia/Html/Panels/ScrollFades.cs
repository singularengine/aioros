using Aioros.Avalonia.Html.Looks;
using Aioros.Markup;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Panels;

/// <summary>
/// A scroll viewer marked <c>a:Web.ScrollFade</c>, written as shadcn/ui's <c>scroll-fade</c> utilities (the shadcn package
/// 4.21.5's <c>dist/tailwind.css</c>): a mask fading the content out at its edges, each fade growing with a scroll-driven
/// animation over the first and last 96 px of the scroll, or shown whole where the browser has no scroll timelines. A
/// viewer that follows its end (<c>a:Web.FollowsEnd</c>, written from its end) reveals its top and bottom fades from the
/// other end of its scroll timeline, as its scroll starts at its bottom.
/// </summary>
internal static class ScrollFades
{
    private const string Size = "min(12%,40px)";

    private const string Reveal = "96px";

    private static readonly Dictionary<string, (string Edges, string Axis, string Gradient, string? RightToLeft)> Kinds = new(StringComparer.Ordinal)
    {
        ["y"] = ("tb", "y", "to bottom,transparent 0,#000 var(--sf-t,0px),#000 calc(100% - var(--sf-b,0px)),transparent 100%", null),
        ["x"] = ("se", "inline", "to right,transparent 0,#000 var(--sf-s,0px),#000 calc(100% - var(--sf-e,0px)),transparent 100%", "to left,transparent 0,#000 var(--sf-s,0px),#000 calc(100% - var(--sf-e,0px)),transparent 100%"),
        ["t"] = ("t", "y", "to bottom,transparent 0,#000 var(--sf-t,0px),#000 100%", null),
        ["b"] = ("b", "y", "to bottom,#000 0,#000 calc(100% - var(--sf-b,0px)),transparent 100%", null),
        ["s"] = ("s", "inline", "to right,transparent 0,#000 var(--sf-s,0px),#000 100%", "to left,transparent 0,#000 var(--sf-s,0px),#000 100%"),
        ["e"] = ("e", "inline", "to right,#000 0,#000 calc(100% - var(--sf-e,0px)),transparent 100%", "to left,#000 0,#000 calc(100% - var(--sf-e,0px)),transparent 100%"),
    };

    public static string? Mark(ScrollViewer scroll, StyleSheet sheet)
    {
        if (Web.GetScrollFade(scroll) is not { } kind || !Kinds.TryGetValue(kind, out var fade))
        {
            return null;
        }

        sheet.AddRule("sf", string.Concat("tbse".Select(edge => $"@property --sf-{edge}{{syntax:\"<length-percentage>\";inherits:false;initial-value:0px}}\n"))
            + $"@keyframes asft{{from{{--sf-t:0px}}to{{--sf-t:{Size}}}}}\n@keyframes asfb{{from{{--sf-b:{Size}}}to{{--sf-b:0px}}}}\n"
            + $"@keyframes asfs{{from{{--sf-s:0px}}to{{--sf-s:{Size}}}}}\n@keyframes asfe{{from{{--sf-e:{Size}}}to{{--sf-e:0px}}}}\n"
            + $"@keyframes asftr{{from{{--sf-t:{Size}}}to{{--sf-t:0px}}}}\n@keyframes asfbr{{from{{--sf-b:0px}}to{{--sf-b:{Size}}}}}\n");
        var reversed = fade.Axis == "y" && Web.GetFollowsEnd(scroll);
        var name = "sf" + kind + (reversed ? "r" : string.Empty);
        var animations = string.Join(',', fade.Edges.Select(edge => $"asf{edge}{(reversed ? "r" : string.Empty)} 1ms ease-in-out"));
        var timelines = string.Join(',', fade.Edges.Select(_ => $"scroll(self {fade.Axis})"));
        var ranges = string.Join(',', fade.Edges.Select(edge => edge is 't' or 's' != reversed ? $"0 {Reveal}" : $"calc(100% - {Reveal}) 100%"));
        var whole = string.Concat(fade.Edges.Select(edge => $"--sf-{edge}:{Size};"));
        sheet.AddRule(name, $".{name}{{mask-image:linear-gradient({fade.Gradient});mask-repeat:no-repeat;mask-composite:intersect}}\n"
            + (fade.RightToLeft is { } back ? $".{name}:where([dir=rtl],[dir=rtl] *){{mask-image:linear-gradient({back})}}\n" : string.Empty)
            + $"@supports (animation-timeline:scroll()){{.{name}{{animation:{animations};animation-timeline:{timelines};animation-range:{ranges};animation-fill-mode:both}}}}\n"
            + $"@supports not (animation-timeline:scroll()){{.{name}{{{whole}}}}}\n");
        return name;
    }
}
