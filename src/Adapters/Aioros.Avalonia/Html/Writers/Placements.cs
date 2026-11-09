using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// Where an anchored popover sits, as Avalonia's popup positioner places a popup: the placement as a CSS
/// <c>position-area</c> (centered on the anchor's side, or aligned with one of its edges), the offset toward the anchor as
/// the margin on the anchor's side (Avalonia moves the popup by its offsets whatever the side, so an offset of 4 below is a
/// 4 px gap and above a 4 px overlap), the offset across as a <c>translate</c>, and fallbacks that flip the popover to the
/// other side when it would leave the view, as Avalonia's constraint adjustment flips it. A popup's collision padding
/// (<c>a:Web.CollisionPadding</c>) is the inset of the edges its area shares with the viewport across its side, so it
/// shifts along its anchor no nearer to them, as Radix Popper's <c>shift</c> with <c>padding</c>.
/// </summary>
internal static class Placements
{
    public static string Of(PlacementMode placement, double horizontal, double vertical, PopupAnchor anchor = PopupAnchor.None, PopupGravity gravity = PopupGravity.None, Thickness room = default, double padding = 0)
    {
        var (area, side) = Area(placement, anchor, gravity);
        var (gap, across) = side switch
        {
            "top" => (room.Bottom - vertical, horizontal),
            "bottom" => (room.Top + vertical, horizontal),
            "left" => (room.Right - horizontal, vertical),
            "right" => (room.Left + horizontal, vertical),
            _ => (0d, 0d),
        };
        var css = $"position-area:{area};";
        if (side is null)
        {
            return css + Translate(horizontal, vertical);
        }

        var margin = side switch
        {
            "top" => "bottom",
            "bottom" => "top",
            "left" => "right",
            _ => "left",
        };
        css += gap == 0 ? string.Empty : $"margin-{margin}:{CssValues.Px(gap)};";
        css += side is "top" or "bottom" ? Translate(across, 0) + "position-try-fallbacks:flip-block;" : Translate(0, across) + "position-try-fallbacks:flip-inline;";
        return css + Padded(area, side, padding);
    }

    public static string Of(Popup popup) => Of(popup.Placement, popup.HorizontalOffset, popup.VerticalOffset, popup.PlacementAnchor, popup.PlacementGravity, padding: Web.GetCollisionPadding(popup));

    public static string Of(PopupFlyoutBase flyout) => Of(flyout.Placement, flyout.HorizontalOffset, flyout.VerticalOffset, flyout.PlacementAnchor, flyout.PlacementGravity);

    private static string Padded(string area, string side, double padding)
    {
        if (padding <= 0)
        {
            return string.Empty;
        }

        var edges = side is "top" or "bottom" ? (First: "left", Second: "right") : (First: "top", Second: "bottom");
        var shared = new[] { edges.First, edges.Second }.Where(edge => !area.Contains(" span-", StringComparison.Ordinal) || area.EndsWith(" span-" + edge, StringComparison.Ordinal));
        return string.Concat(shared.Select(edge => $"{edge}:{CssValues.Px(padding)};"));
    }

    private static string Translate(double x, double y) =>
        x == 0 && y == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"translate:{CssValues.Px(x)} {CssValues.Px(y)};");

    private static (string Area, string? Side) Area(PlacementMode placement, PopupAnchor anchor, PopupGravity gravity) => placement switch
    {
        PlacementMode.Top => ("top", "top"),
        PlacementMode.Left => ("left", "left"),
        PlacementMode.Right => ("right", "right"),
        PlacementMode.Center => ("center", null),
        PlacementMode.TopEdgeAlignedLeft => ("top span-right", "top"),
        PlacementMode.TopEdgeAlignedRight => ("top span-left", "top"),
        PlacementMode.BottomEdgeAlignedLeft or PlacementMode.Pointer => ("bottom span-right", "bottom"),
        PlacementMode.BottomEdgeAlignedRight => ("bottom span-left", "bottom"),
        PlacementMode.LeftEdgeAlignedTop => ("left span-bottom", "left"),
        PlacementMode.LeftEdgeAlignedBottom => ("left span-top", "left"),
        PlacementMode.RightEdgeAlignedTop => ("right span-bottom", "right"),
        PlacementMode.RightEdgeAlignedBottom => ("right span-top", "right"),
        PlacementMode.AnchorAndGravity => Gravity(anchor, gravity),
        _ => ("bottom", "bottom"),
    };

    /// <summary>A placement given by an anchor point and a gravity: the side the gravity points to, aligned as the anchor point sits on the anchor's edge.</summary>
    private static (string Area, string? Side) Gravity(PopupAnchor anchor, PopupGravity gravity)
    {
        var vertical = Pick(gravity.HasFlag(PopupGravity.Top), "top", gravity.HasFlag(PopupGravity.Bottom), "bottom");
        var horizontal = Pick(gravity.HasFlag(PopupGravity.Left), "left", gravity.HasFlag(PopupGravity.Right), "right");
        if (vertical is not null)
        {
            var span = Pick(anchor.HasFlag(PopupAnchor.Left), " span-right", anchor.HasFlag(PopupAnchor.Right), " span-left") ?? (horizontal is null ? string.Empty : " span-" + horizontal);
            return (vertical + span, vertical);
        }

        return horizontal is null ? ("center", null) : (horizontal + Pick(anchor.HasFlag(PopupAnchor.Top), " span-bottom", anchor.HasFlag(PopupAnchor.Bottom), " span-top"), horizontal);
    }

    private static string? Pick(bool first, string firstValue, bool second, string secondValue) => first ? firstValue : Fallback(second, secondValue);

    private static string? Fallback(bool second, string secondValue) => second ? secondValue : null;
}
