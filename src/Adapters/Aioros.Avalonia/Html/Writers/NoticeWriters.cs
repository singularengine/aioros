using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// The notices a post showed (a window notification manager's cards), written after the page in a manual popover at the
/// corner where the manager's panel stands, its margins (the panel's and its ancestors', as a toaster's offset) kept from
/// the window's edges: each card through its own control theme, stacked as the panel stacks them (a stack panel as a
/// column, reversed when it is, any other panel's arrangement as Avalonia laid it out, held at the panel's side so cards
/// of another height keep their places), each fading after its notification's expiration (Avalonia's 5 s when it gives
/// none, never when it is zero) on the browser's clock, and with the step that showed them for its buttons to post. A panel
/// that stacks its cards as sonner's toaster does is written as a deck (<see cref="NoticeDecks"/>). The framework's script
/// opens them; a browser without scripting shows them by style alone.
/// </summary>
internal static class NoticeWriters
{
    private const string Css =
        ".notice[popover]{position:fixed;inset:auto;margin:0;border:0;padding:0;background:none;color:inherit;overflow:visible}" +
        ".notice:popover-open{display:flex;flex-direction:column}.notice.nr:popover-open{flex-direction:column-reverse}.notice.na:popover-open{display:block}" +
        ".notice>.nt{animation:notice-out .3s ease forwards;animation-delay:var(--expire)}.notice>.nt.nk{animation:none}" +
        "@keyframes notice-out{to{opacity:0;visibility:hidden}}" +
        "@media (scripting:none){.notice[popover]{display:flex;flex-direction:column;z-index:2147483647}.notice.nr[popover]{flex-direction:column-reverse}.notice.na[popover]{display:block}}\n";

    private static readonly TimeSpan Default = TimeSpan.FromSeconds(5);

    public static void Write(HtmlContext context, Overlays overlays, Action<Control, ParentKind> write)
    {
        var start = context.Fields.Count;
        foreach (var layer in overlays.Notices)
        {
            context.Sheet.AddRule("notice", Css);
            WriteLayer(context, layer, overlays.NoticeReplay, write);
        }

        var buttons = Enumerable.Range(start, context.Fields.Count - start).Where(index => context.Fields.IsButton(index) && !context.Fields.IsInert(index)).Select(context.Fields.Token).ToList();
        if (overlays.NoticeReplay is { } replay && buttons.Count > 0)
        {
            context.Plan.Literal($"<input type=\"hidden\" name=\"{PlanForms.NoticeField}\" value=\"{CssValues.Encode(string.Join(' ', buttons) + "\n" + replay)}\">");
        }
    }

    private static void WriteLayer(HtmlContext context, NoticeLayer layer, string? replay, Action<Control, ParentKind> write)
    {
        var panel = layer.Panel;
        var deck = NoticeDecks.Stacks(panel);
        var arranged = deck || (panel is not StackPanel and not ReversibleStackPanel && panel.Bounds.Width > 0);
        var steps = replay is null ? string.Empty : $" data-replay=\"{CssValues.Encode(replay)}\"";
        var marks = deck ? NoticeDecks.Open(context, panel) : string.Empty;
        context.Plan.Literal($"<div popover=\"manual\" class=\"notice{Kind(panel, arranged)}{(deck ? " nd" : string.Empty)}\"{marks}{steps} style=\"{Corner(panel)}{Box(layer, deck, arranged)}\">");
        foreach (var notice in layer.Controls)
        {
            WriteCard(context, layer, notice, deck, arranged, write);
        }

        context.Plan.Literal("</div>");
    }

    private static string Box(NoticeLayer layer, bool deck, bool arranged) =>
        deck ? NoticeDecks.Box(layer.Panel, layer.Controls)
        : arranged ? string.Create(CultureInfo.InvariantCulture, $"width:{CssValues.Px(layer.Panel.Bounds.Width)};height:{CssValues.Px(layer.Panel.Bounds.Height)};")
        : string.Empty;

    private static void WriteCard(HtmlContext context, NoticeLayer layer, Control notice, bool deck, bool arranged, Action<Control, ParentKind> write)
    {
        var expiration = Expiration(notice);
        var (moving, attributes, place) = deck ? NoticeDecks.Place(context, layer.Panel, layer.Controls, notice)
            : (string.Empty, string.Empty, arranged ? Arranged(notice, layer.Panel) : string.Empty);
        var kept = expiration == TimeSpan.Zero ? " nk" : Leaving(context, notice);
        context.Plan.Literal(string.Create(CultureInfo.InvariantCulture, $"<div class=\"{CssValues.Join("nt" + kept, moving)}\" role=\"status\"{attributes} style=\"--expire:{CssValues.Ms(expiration)};{place}\">"));
        write(notice, ParentKind.Column);
        context.Plan.Literal("</div>");
    }

    /// <summary>
    /// The class of a card that leaves as its theme closes it (its root's look while the card is <c>:closing</c>, sonner's
    /// fall): an animation of that look over its closing transitions' length after the expiration, in place of the
    /// framework's fade, at once for a visitor who asks for less motion; nothing when the theme gives no closing look.
    /// </summary>
    private static string Leaving(HtmlContext context, Control notice)
    {
        if (notice.GetVisualChildren().OfType<Control>().FirstOrDefault() is not { } moving || Entrances.Leaving(notice, moving) is not { } leaving)
        {
            return string.Empty;
        }

        var name = "n" + CssValues.Hash(leaving.Css + leaving.Easing + CssValues.Ms(leaving.Longest));
        context.Sheet.AddRule(name, string.Create(CultureInfo.InvariantCulture,
            $".notice>.nt.{name}{{animation:{name} {CssValues.Ms(leaving.Longest)} {leaving.Easing} forwards;animation-delay:var(--expire)}}@keyframes {name}{{to{{{leaving.Css}visibility:hidden}}}}" +
            $"@media (prefers-reduced-motion:reduce){{.notice>.nt.{name}{{animation-duration:0s}}}}\n"));
        return " " + name;
    }

    private static string Kind(Panel panel, bool arranged) => arranged ? " na" : panel is ReversibleStackPanel { ReverseOrder: true } ? " nr" : string.Empty;

    /// <summary>How long a card stays: its notification's expiration, or Avalonia's default when it gives none.</summary>
    private static TimeSpan Expiration(Control notice) =>
        notice is ContentControl { Content: INotification notification } ? notification.Expiration : Default;

    /// <summary>A card where the panel arranged it, held at the panel's vertical side.</summary>
    private static string Arranged(Control notice, Panel panel)
    {
        var at = notice.Bounds;
        var vertical = panel.VerticalAlignment == VerticalAlignment.Bottom
            ? $"bottom:{CssValues.Px(panel.Bounds.Height - at.Bottom)};"
            : $"top:{CssValues.Px(at.Y)};";
        return string.Create(CultureInfo.InvariantCulture, $"position:absolute;left:{CssValues.Px(at.X)};width:{CssValues.Px(at.Width)};{vertical}");
    }

    /// <summary>The corner where the panel stands, kept from the window's edges by the margins of the panel and its ancestors.</summary>
    private static string Corner(Panel panel)
    {
        var margin = panel.GetSelfAndVisualAncestors().OfType<Layoutable>().TakeWhile(part => part is not TopLevel and not global::Avalonia.Controls.Primitives.AdornerLayer and not global::Avalonia.Controls.Primitives.OverlayLayer)
            .Aggregate(default(Thickness), (sum, part) => sum + part.Margin);
        var horizontal = panel.HorizontalAlignment switch
        {
            HorizontalAlignment.Left => $"left:{CssValues.Px(margin.Left)};",
            HorizontalAlignment.Center => "left:0;right:0;margin-inline:auto;",
            HorizontalAlignment.Stretch => $"left:{CssValues.Px(margin.Left)};right:{CssValues.Px(margin.Right)};",
            _ => $"right:{CssValues.Px(margin.Right)};",
        };
        var vertical = panel.VerticalAlignment switch
        {
            VerticalAlignment.Bottom => $"bottom:{CssValues.Px(margin.Bottom)};",
            VerticalAlignment.Center => "top:0;bottom:0;margin-block:auto;",
            _ => $"top:{CssValues.Px(margin.Top)};",
        };
        return vertical + horizontal;
    }
}
