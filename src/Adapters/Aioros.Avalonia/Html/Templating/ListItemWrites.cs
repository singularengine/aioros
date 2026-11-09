using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>What a writer of a bound list does around its items: their keys in the names written in them and the mark of the region's lists.</summary>
internal static class ListItemWrites
{
    /// <summary>Makes the fields written from here on carry this list item's key, and none once given none (<see cref="ItemKeyNode"/>).</summary>
    public static void EnterItem(this HtmlContext context, UInt128? key)
    {
        context.Fields.ItemKey = key;
        context.Plan.Add(new ItemKeyNode(key));
    }

    /// <summary>Starts an item of a bound list on the engine: its key for the names written in it and its place in the region's shape.</summary>
    public static void EnterListItem(this HtmlContext context, UInt128? outer, ValueGetter? key, object? item)
    {
        context.EnterItem(key is null ? outer : ItemKeys.Combine(outer, key(item)));
        context.Fields.ListItem(key is null ? null : context.Fields.ItemKey);
    }

    /// <summary>Writes the mark of the lists of the region just written: found by the render on a plan, as it stands on the engine.</summary>
    public static void WriteShape(this HtmlContext context) =>
        context.Plan.Add(context.Plan.Planning ? new ShapeNode() : new ShapeNode(context.Fields.Shape()));
}
