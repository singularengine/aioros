using System.Collections.Immutable;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Markup.Xaml.Templates;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A <c>TreeView</c> as nested lists: items with children are <c>&lt;details&gt;</c> branches, the others rows, each
/// indented by its level. A tree with a bound <c>ItemsSource</c> is planned as a <see cref="TreeNode"/>: per item
/// template (its <c>TreeDataTemplate</c>'s type), a branch and a leaf learned from items of the compiled tree, or from
/// an item whose children are set above their binding, and the branch writes its children with the same body.
/// </summary>
internal static class TreeWriters
{
    public static void WriteTreeView(HtmlContext context, TreeView tree, ParentKind parent)
    {
        var style = context.Box(tree, parent);
        BoxStyles.Fill(style, tree.Background, tree.BorderBrush, tree.BorderThickness, tree.CornerRadius, tree.Padding);
        context.Sheet.AddRule("tv", "ul.tv{list-style:none;margin:0;padding:0;display:flex;flex-direction:column;min-width:0}ul.tv summary>.chev{opacity:1}\n");
        context.Plan.Literal($"<ul class=\"{context.Classes(tree, "tv", style)}\">");
        if (context.Plan.Planning && context.Plan.BoundPath(tree, ItemsControl.ItemsSourceProperty) is { } path)
        {
            Plan(context, tree, path);
        }
        else if (!context.Plan.Planning && LocalBindings.IsBound(tree, ItemsControl.ItemsSourceProperty))
        {
            WriteBound(context, tree);
        }
        else
        {
            WriteItems(context, ItemParts.Containers(tree));
        }

        context.Plan.Literal("</ul>");
    }

    private static void Plan(HtmlContext context, TreeView tree, string path)
    {
        if (tree.SelectedItem is not null || LocalBindings.HasBinding(tree, TreeView.SelectedItemProperty))
        {
            context.Plan.Fail("a TreeView with a bound ItemsSource and a selected item: the selection is not planned");
            return;
        }

        var types = tree.ItemTemplate is null ? tree.DataTemplates.OfType<TreeDataTemplate>().Select(template => template.DataType).OfType<Type>().ToList() : [];
        ImmutableArray<IPlanNode> body;
        if (types.Count == 0)
        {
            body = Case(context, [.. All(tree)]);
        }
        else
        {
            var cases = ImmutableArray.CreateBuilder<TypeCase>();
            foreach (var type in types)
            {
                cases.Add(new TypeCase(type, Case(context, [.. All(tree).Where(item => type.IsInstanceOfType(item.DataContext))])));
            }

            body = [new TypeSwitchNode(cases.ToImmutable())];
        }

        var source = SlotSpec.Bound(path);
        context.Plan.Add(ItemIdentity.Keyed(context.Plan, new TreeNode(Slots.Getter(source), body) { Source = source }, ItemIdentity.Path(tree, All(tree).FirstOrDefault()?.DataContext)));
    }

    /// <summary>
    /// The body of the items of one template: a branch and a leaf, each written when the item has children or has none,
    /// each learned from an item of its kind, or from another item of the template when the compiled tree has none.
    /// </summary>
    private static ImmutableArray<IPlanNode> Case(HtmlContext context, List<TreeViewItem> items)
    {
        if (items.Count == 0)
        {
            context.Plan.Fail("a TreeView with a bound ItemsSource and an item template no item of the compiled tree takes, which the plan cannot learn");
            return [];
        }

        var children = Children(context, items[0]);
        if (children is null)
        {
            return Learned(context, items[0], null);
        }

        var branch = Learned(context, items.Find(item => item.ItemCount > 0) ?? items[0], children);
        var leaf = Learned(context, items.Find(item => item.ItemCount == 0) ?? items[0], null);
        var test = SlotSpec.Bound(children) with { Kind = "test", Argument = "items" };
        var none = test with { Argument = "no-items" };
        return [new ConditionNode(Slots.Getter(test), branch) { Source = test }, new ConditionNode(Slots.Getter(none), leaf) { Source = none }];
    }

    /// <summary>The path of an item's children, from the binding its <c>TreeDataTemplate</c> gives its <c>ItemsSource</c>, or nothing when it has none.</summary>
    private static string? Children(HtmlContext context, TreeViewItem item)
    {
        context.Plan.Begin(item.DataContext);
        var path = LocalBindings.HasBinding(item, ItemsControl.ItemsSourceProperty) ? context.Plan.BoundPath(item, ItemsControl.ItemsSourceProperty) : null;
        context.Plan.End();
        return path;
    }

    /// <summary>Writes an item as the tree's body learns it: as a branch whose children are at the path, or as a leaf.</summary>
    private static ImmutableArray<IPlanNode> Learned(HtmlContext context, TreeViewItem item, string? children)
    {
        context.Plan.Begin(item.DataContext);
        WriteItem(context, item, children ?? Leaf);
        return context.Plan.End();
    }

    private const string Leaf = "";

    private static IEnumerable<TreeViewItem> All(ItemsControl owner)
    {
        foreach (var item in ItemParts.Containers(owner).OfType<TreeViewItem>())
        {
            yield return item;
            foreach (var inner in All(item))
            {
                yield return inner;
            }
        }
    }

    private static void WriteBound(HtmlContext context, TreeView tree)
    {
        var mark = context.Fields.EnterList();
        WriteItems(context, ItemParts.Containers(tree), ItemIdentity.Getter(ItemIdentity.Path(tree, All(tree).FirstOrDefault()?.DataContext)), true);
        context.Fields.LeaveList(mark);
    }

    private static void WriteItems(HtmlContext context, IReadOnlyList<Control> containers, ValueGetter? key = null, bool listed = false)
    {
        var outer = context.Fields.ItemKey;
        foreach (var item in containers.OfType<TreeViewItem>().Where(item => item.IsVisible))
        {
            if (listed)
            {
                context.EnterListItem(outer, key, item.DataContext);
            }

            WriteItem(context, item, null, key, listed);
        }

        if (listed)
        {
            context.EnterItem(outer);
        }
    }

    /// <summary>Writes an item: as Avalonia shows it, or, while a tree is planned, as a branch whose children are at the path (or a leaf, for an empty path) indented by the depth it is written at.</summary>
    private static void WriteItem(HtmlContext context, TreeViewItem item, string? planned = null, ValueGetter? key = null, bool listed = false)
    {
        var row = RowClass(context, item);
        var header = ItemLooks.Part<ContentPresenter>(item, "PART_HeaderPresenter");
        if (LocalBindings.IsBound(item, TreeViewItem.IsSelectedProperty))
        {
            context.Plan.Fail("TreeViewItem.IsSelected is bound");
        }

        var classes = item.IsSelected ? CssValues.Join(row, "sel") : row;
        var children = planned is null ? ItemParts.Containers(item) : [];
        if (planned is Leaf || (planned is null && children.Count == 0))
        {
            context.Plan.Literal($"<li><div class=\"{classes}\" style=\"padding-left:");
            Indent(context, item, planned is not null);
            context.Plan.Literal("\"><span class=\"tvs\"></span>");
            ItemParts.Content(context, header, ParentKind.Row);
            context.Plan.Literal("</div></li>");
            return;
        }

        context.Plan.Literal("<li><details");
        if (context.Plan.Planning && context.Plan.BoundPath(item, TreeViewItem.IsExpandedProperty) is { } open)
        {
            context.Plan.Add(new ConditionNode(PropertyPaths.Getter(open), [new LiteralNode(" open")]) { Source = SlotSpec.Bound(open) });
        }
        else if (item.IsExpanded)
        {
            context.Plan.Literal(" open");
        }

        context.Plan.Literal($"><summary class=\"{classes}\" style=\"padding-left:");
        Indent(context, item, planned is not null);
        context.Plan.Literal($"\">{InputWriters.IconSvg("chevron-right", 12, 2, "chev")}");
        ItemParts.Content(context, header, ParentKind.Row);
        context.Plan.Literal("</summary><ul class=\"tv\">");
        if (planned is null)
        {
            WriteItems(context, children, key, listed);
        }
        else
        {
            var source = SlotSpec.Bound(planned);
            context.Plan.Add(new BranchNode(Slots.Getter(source)) { Source = source });
        }

        context.Plan.Literal("</ul></details></li>");
    }

    /// <summary>The indent of an item's row: its level's, or, while a tree is planned, the depth it is written at on each render.</summary>
    private static void Indent(HtmlContext context, TreeViewItem item, bool planned)
    {
        var indent = item.TryFindResource("TreeViewItemIndent", item.ActualThemeVariant, out var value) && value is double size ? size : 16;
        if (planned)
        {
            context.Plan.Add(new DepthNode(indent, Offset));
        }
        else
        {
            context.Plan.Literal(DepthNode.Pixels(item.Level, indent, Offset));
        }
    }

    private const double Offset = 12;

    private static string RowClass(HtmlContext context, TreeViewItem item)
    {
        var style = new StringBuilder("display:flex;flex-direction:row;align-items:center;gap:12px;cursor:pointer;");
        BoxStyles.Font(style, item.FontSize, item.FontWeight, null);
        style.Append(item.MinHeight > 0 ? $"min-height:{CssValues.Px(item.MinHeight)};" : string.Empty);
        context.Sheet.AddRule("tvs", ".tvs{width:12px;flex-shrink:0}\n");
        if (ItemLooks.Part<Border>(item, "PART_LayoutRoot") is not { } root || ItemLooks.Part<ContentPresenter>(item, "PART_HeaderPresenter") is not { } header)
        {
            return context.Sheet.Class(null, style);
        }

        style.Append(root.CornerRadius != default ? $"border-radius:{CssValues.Px(root.CornerRadius.TopLeft)};" : string.Empty);
        return context.Sheet.Class(ItemLooks.Class(context, item, root, header, ItemLooks.Marked, hoverOnPart: true), style);
    }
}
