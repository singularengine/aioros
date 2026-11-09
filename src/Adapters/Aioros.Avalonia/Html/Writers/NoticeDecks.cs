using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Markup;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A notice layer whose panel stacks its cards as sonner's toaster does (<c>a:Web.Deck</c>, Aioros.Cosmo's
/// <c>c:ToastDeck</c>): the cards, oldest first, stand at the panel's anchored edge, each carrying where it stands in the
/// stack (collapsed: the gap times its place behind the newest, shrunk by the deck's step each, as tall as the newest,
/// marked <c>data-behind</c>, hidden past the visible count; expanded: the gap apart at their own heights) in its
/// <c>translate</c>, <c>scale</c>, <c>height</c> and <c>opacity</c>, moving there over the cards' own transform transition,
/// and its height as Avalonia measured it in <c>data-h</c>. The opacity the deck's behind class gives each part of a card is
/// learned and given under <c>data-behind</c>. The framework's swipe module keeps the stack in the browser; a browser
/// without scripting shows it as written.
/// </summary>
internal static class NoticeDecks
{
    private const string Css =
        ".notice.nd>.nt{position:absolute;left:0;right:0;bottom:0}.notice.nd[data-aioros-anchor=top]>.nt{bottom:auto;top:0}" +
        ".notice.nd>.nt>*{height:100%;box-sizing:border-box}.notice.nd>.nt[data-aioros-swiping]{transition:none!important;user-select:none}" +
        "@keyframes deck-out-up{from{transform:translate(var(--sx,0px),var(--sy,0px))}to{transform:translate(var(--sx,0px),calc(var(--sy,0px) - 100%));opacity:0}}" +
        "@keyframes deck-out-down{from{transform:translate(var(--sx,0px),var(--sy,0px))}to{transform:translate(var(--sx,0px),calc(var(--sy,0px) + 100%));opacity:0}}" +
        "@keyframes deck-out-left{from{transform:translate(var(--sx,0px),var(--sy,0px))}to{transform:translate(calc(var(--sx,0px) - 100%),var(--sy,0px));opacity:0}}" +
        "@keyframes deck-out-right{from{transform:translate(var(--sx,0px),var(--sy,0px))}to{transform:translate(calc(var(--sx,0px) + 100%),var(--sy,0px));opacity:0}}" +
        ".notice>.nt[data-out]{animation-duration:.2s!important;animation-timing-function:ease-out!important;animation-fill-mode:forwards!important;animation-delay:0s!important;animation-play-state:running!important}" +
        ".notice>.nt[data-out=up]{animation-name:deck-out-up!important}.notice>.nt[data-out=down]{animation-name:deck-out-down!important}" +
        ".notice>.nt[data-out=left]{animation-name:deck-out-left!important}.notice>.nt[data-out=right]{animation-name:deck-out-right!important}" +
        "@media (prefers-reduced-motion:reduce){.notice.nd>.nt{transition:none!important}.notice>.nt[data-out]{animation-duration:0s!important}}\n";

    public static bool Stacks(Panel panel) => !double.IsNaN(Web.GetDeck(panel));

    /// <summary>The deck's attributes after the layer's tag: its gap, visible count and step, its anchor, and whether it stands expanded.</summary>
    public static string Open(HtmlContext context, Panel panel)
    {
        context.Sheet.AddRule("notice-deck", Css);
        var (gap, visible, step) = Shape(panel);
        var top = panel.VerticalAlignment == VerticalAlignment.Top;
        return string.Create(CultureInfo.InvariantCulture,
            $" data-aioros-deck=\"{CssValues.Num(gap)} {visible} {CssValues.Num(step)}\" data-aioros-anchor=\"{(top ? "top" : "bottom")}\"{(Web.GetDeckExpanded(panel) ? " data-aioros-expanded" : string.Empty)}");
    }

    /// <summary>The box of the deck: as wide as its panel, as tall as its newest card, or as the cards it shows expanded.</summary>
    public static string Box(Panel panel, IReadOnlyList<Control> cards)
    {
        var (gap, visible, _) = Shape(panel);
        var heights = Newest(cards).Select(Height).ToList();
        var height = Web.GetDeckExpanded(panel) ? heights.Take(visible).Sum() + (Math.Max(Math.Min(visible, heights.Count), 1) - 1) * gap : heights.FirstOrDefault();
        return string.Create(CultureInfo.InvariantCulture, $"width:{CssValues.Px(panel.Bounds.Width)};height:{CssValues.Px(height)};");
    }

    /// <summary>The class, attributes and inline style of a card's wrapper where it stands in the stack, counted from the newest.</summary>
    public static (string Classes, string Attributes, string Style) Place(HtmlContext context, Panel panel, IReadOnlyList<Control> cards, Control card)
    {
        var (gap, visible, step) = Shape(panel);
        var newest = Newest(cards);
        var index = newest.IndexOf(card);
        var expanded = Web.GetDeckExpanded(panel);
        var sign = panel.VerticalAlignment == VerticalAlignment.Top ? 1 : -1;
        var offset = expanded ? newest.Take(index).Sum(Height) + gap * index : gap * index;
        var behind = !expanded && index > 0;
        var style = string.Create(CultureInfo.InvariantCulture, $"translate:0 {CssValues.Px(sign * offset)};")
            + (behind ? string.Create(CultureInfo.InvariantCulture, $"scale:{CssValues.Num(1 - (step * index))};height:{CssValues.Px(Height(newest[0]))};") : string.Empty)
            + (index >= visible ? "opacity:0;" : string.Empty)
            + string.Create(CultureInfo.InvariantCulture, $"z-index:{newest.Count - index};");
        var attributes = string.Create(CultureInfo.InvariantCulture, $" data-h=\"{CssValues.Num(Height(card))}\"") + (behind ? " data-behind" : string.Empty);
        Behind(context, panel, card);
        return (Moving(context, card), attributes, style);
    }

    private static (double Gap, int Visible, double Step) Shape(Panel panel) =>
        (Web.GetDeck(panel), Math.Max(1, Web.GetDeckVisible(panel)), Web.GetDeckScale(panel));

    private static List<Control> Newest(IReadOnlyList<Control> cards) => [.. cards.Reverse()];

    /// <summary>A card's own height, as Avalonia measured it.</summary>
    private static double Height(Control card) => card.DesiredSize.Height > 0 ? card.DesiredSize.Height : card.Bounds.Height;

    /// <summary>The class that moves a card to its place in the stack over the card's own transform transition (400 ms with <c>ease</c> when it has none).</summary>
    private static string Moving(HtmlContext context, Control card)
    {
        var moves = (card.Transitions ?? []).OfType<TransitionBase>().FirstOrDefault(transition => transition.Property == Visual.RenderTransformProperty);
        var length = moves is null ? "400ms" : CssValues.Ms(moves.Duration);
        var easing = moves is null ? "ease" : CssValues.Ease(moves.Easing);
        var name = "dm" + CssValues.Hash(length + easing);
        context.Sheet.AddRule(name, $".notice.nd>.nt.{name}{{transition:translate {length} {easing},scale {length} {easing},opacity {length} {easing},height {length} {easing}}}\n");
        return name;
    }

    /// <summary>
    /// The opacity the deck's behind class (<c>a:Web.DeckBehind</c>) gives each part of the card, learned with the class on
    /// and off: each part whose opacity changes takes a class whose rules give it either opacity as its card is behind or not.
    /// </summary>
    private static void Behind(HtmlContext context, Panel panel, Control card)
    {
        if (Web.GetDeckBehind(panel) is not { Length: > 0 } name)
        {
            return;
        }

        var had = card.Classes.Contains(name);
        var parts = card.GetVisualDescendants().OfType<Control>().Prepend(card).ToList();
        Settled.Change(card, () => card.Classes.Remove(name));
        var front = parts.ConvertAll(part => part.Opacity);
        Settled.Change(card, () => card.Classes.Add(name));
        var behind = parts.ConvertAll(part => part.Opacity);
        Settled.Change(card, () => card.Classes.Set(name, had));
        for (var index = 1; index < parts.Count; index++)
        {
            if (Math.Abs(front[index] - behind[index]) < 1e-6)
            {
                continue;
            }

            var part = "db" + CssValues.Hash(CssValues.Num(front[index]) + "/" + CssValues.Num(behind[index]));
            context.Sheet.AddRule(part, string.Create(CultureInfo.InvariantCulture,
                $".notice.nd>.nt:not([data-behind]) .{part}.{part}{{opacity:{CssValues.Num(front[index])}}}.notice.nd>.nt[data-behind] .{part}.{part}{{opacity:{CssValues.Num(behind[index])}}}\n"));
            context.PartClasses[parts[index]] = part;
        }
    }
}
