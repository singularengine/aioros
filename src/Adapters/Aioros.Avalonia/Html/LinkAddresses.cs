using Aioros.Markup;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;

namespace Aioros.Avalonia.Html;

/// <summary>
/// The address of an <c>a:Web.Href</c> link: its <c>href</c>, literal or a slot of its binding (also in an item
/// template), and the mark of the current page the request decides for each link (<see cref="CurrentNode"/>).
/// </summary>
internal static class LinkAddresses
{
    public static bool Links(AvaloniaObject control) => Web.GetHref(control) is not null || LocalBindings.IsBound(control, Web.HrefProperty);

    public static void Write(PlanRecorder plan, AvaloniaObject control)
    {
        plan.Literal(" href=\"");
        plan.Value(control, Web.HrefProperty, ValueFormat.Href);
        plan.Literal("\"");
        plan.Add(plan.Planning && plan.BoundPath(control, Web.HrefProperty) is { } href
            ? new CurrentNode(null, PropertyPaths.Getter(href)) { Source = SlotSpec.Bound(href), Exact = Web.GetExact(control) }
            : new CurrentNode(Web.GetHref(control)) { Exact = Web.GetExact(control) });
    }
}
