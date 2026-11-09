using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>
/// The runs and spans of a text that carry an <c>a:Web.Href</c>, which Avalonia draws as text and never hit tests: the
/// one under a point is found as <c>SelectableTextBlock</c> finds a character (<c>TextLayout.HitTestPoint</c> inside the
/// padding), each inline holding its text's length, a line break and an inline container one.
/// </summary>
internal static class InlineLinks
{
    private static readonly Cursor Hand = new(StandardCursorType.Hand);

    public static string? At(TextBlock text, Point point)
    {
        if (text.Inlines is not { Count: > 0 } inlines || !inlines.Any(Linked))
        {
            return null;
        }

        var hit = text.TextLayout.HitTestPoint(point - new Point(text.Padding.Left, text.Padding.Top));
        if (!hit.IsInside)
        {
            return null;
        }

        var start = 0;
        return Find(inlines, hit.TextPosition, ref start, null);
    }

    /// <summary>Shows the hand cursor while the pointer is over a linked inline of a text that has one.</summary>
    public static void Hover(TextBlock text, Point point)
    {
        if (text.Inlines is not { Count: > 0 } inlines || !inlines.Any(Linked))
        {
            return;
        }

        if (At(text, point) is not null)
        {
            text.Cursor = Hand;
        }
        else
        {
            text.ClearValue(InputElement.CursorProperty);
        }
    }

    private static bool Linked(Inline inline) =>
        Web.GetHref(inline) is not null || (inline is Span span && span.Inlines.Any(Linked));

    private static string? Find(InlineCollection inlines, int position, ref int start, string? href)
    {
        foreach (var inline in inlines)
        {
            var address = Web.GetHref(inline) ?? href;
            if (inline is Span span)
            {
                if (Find(span.Inlines, position, ref start, address) is { } found)
                {
                    return found;
                }

                continue;
            }

            var length = inline is Run run ? run.Text?.Length ?? 0 : 1;
            if (position >= start && position < start + length)
            {
                return address;
            }

            start += length;
        }

        return null;
    }
}
