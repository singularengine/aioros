using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Markup;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// What identifies an item of a bound list, so a post names the item it is for: the list's <c>a:Web.ItemKey</c>, else the
/// item's <c>Id</c>, else the item itself when it is a text, a number or an enum. A list whose items differ in type (a
/// template selector, several typed templates) takes it only from <c>a:Web.ItemKey</c>; a list without an identity stays
/// positional.
/// </summary>
internal static class ItemIdentity
{
    private const string Convention = "Id";

    /// <summary>The path of an item's identity (empty for the item itself), or null when the list's items have none.</summary>
    public static string? Path(Control list, object? sample)
    {
        if (Web.GetItemKey(list) is { Length: > 0 } written)
        {
            return written;
        }

        if (list is ItemsControl items && (TemplateSelectors.IsSelector(items.ItemTemplate) || TypedItems.Templates(items) is { Count: > 1 }))
        {
            return null;
        }

        return sample switch
        {
            null => null,
            string or Enum or IFormattable => string.Empty,
            _ => ReflectedMembers.Has(sample.GetType(), Convention) ? Convention : null,
        };
    }

    /// <summary>The repeat as a bound list of the page, with this identity.</summary>
    public static RepeatNode Keyed(PlanRecorder plan, RepeatNode repeat, string? path)
    {
        Warn(plan, path, repeat.Source?.Path, repeat.Body);
        return repeat with { Listed = true, ItemKey = Spec(path), ItemKeyGetter = Getter(path) };
    }

    /// <summary>The tree as a bound list of the page, with this identity.</summary>
    public static TreeNode Keyed(PlanRecorder plan, TreeNode tree, string? path)
    {
        Warn(plan, path, tree.Source?.Path, tree.Body);
        return tree with { Listed = true, ItemKey = Spec(path), ItemKeyGetter = Getter(path) };
    }

    /// <summary>The choice as a bound list of the page, its identity learned from its first container, when its items are bound.</summary>
    public static ChoiceNode Keyed(PlanRecorder plan, ChoiceNode choice, Control list, IReadOnlyList<Control> containers)
    {
        if (choice.Items is null)
        {
            return choice;
        }

        var path = Path(list, containers.Count == 0 ? null : containers[0].DataContext);
        Warn(plan, path, choice.ItemsSource?.Path, choice.Bodies.IsDefaultOrEmpty ? [] : choice.Bodies[0]);
        return choice with { Listed = true, ItemKey = Spec(path), ItemKeyGetter = Getter(path) };
    }

    private static SlotSpec? Spec(string? path) => path is null ? null : path.Length == 0 ? new SlotSpec("self") : SlotSpec.Bound(path);

    private static void Warn(PlanRecorder plan, string? path, string? list, ImmutableArray<IPlanNode> body)
    {
        if (path is null && PlanScan.HasFields(body))
        {
            plan.Warn($"AIOROS033: the list bound to '{list}' has fields or buttons in its items and its items have no identity, so a post can only tell that its count changed: give its items an Id or set a:Web.ItemKey on the list");
        }
    }

    /// <summary>What the engine needs of a bound list of this panel, or null when it is not one: the same lists the plan counts.</summary>
    public static ListedPanel? OfPanel(PlanRecorder plan, Control panel, IReadOnlyList<Visual> children)
    {
        if (panel is not Panel || panel.GetVisualParent() is not ItemsPresenter { TemplatedParent: ItemsControl items } || !ListPlans.Applies(plan, items) || Nested(plan, items)
            || (children.Count > 0 && children[0] is Control container && Hierarchies.Applies(container)))
        {
            return null;
        }

        return new ListedPanel(Getter(Path(items, children.Count > 0 && children[0] is Control first ? first.DataContext : null)));
    }

    /// <summary>Whether the list is the children of an item of another bound list (a hierarchy), which the plan writes as one tree.</summary>
    private static bool Nested(PlanRecorder plan, ItemsControl items) =>
        items.GetVisualParent() is Panel panel && panel.GetVisualParent() is ItemsPresenter { TemplatedParent: ItemsControl outer } && ListPlans.Applies(plan, outer);

    /// <summary>The key of the keyed list items a control stands in, outermost first, or null outside any.</summary>
    public static UInt128? Around(Control control)
    {
        var path = control.GetVisualAncestors().Prepend(control).OfType<Control>().Select(Item).OfType<(ValueGetter Key, object? Item)>().Reverse();
        UInt128? key = null;
        foreach (var (get, item) in path)
        {
            key = ItemKeys.Combine(key, get(item));
        }

        return key;
    }

    /// <summary>The keyed list item a control stands in, its innermost one, or null outside any.</summary>
    public static Control? ItemOf(Control control) => control.GetVisualAncestors().Prepend(control).OfType<Control>().FirstOrDefault(container => Item(container) is not null);

    private static (ValueGetter Key, object? Item)? Item(Control container) =>
        container.GetVisualParent() is Panel panel && panel.GetVisualParent() is ItemsPresenter { TemplatedParent: ItemsControl items } && items is not (SelectingItemsControl or TreeView or TreeViewItem or MenuBase or MenuItem)
        && LocalBindings.IsBound(items, ItemsControl.ItemsSourceProperty) && Getter(Path(items, container.DataContext)) is { } key
            ? (key, container.DataContext)
            : null;

    /// <summary>What reads an identity by its path, or null without one.</summary>
    public static ValueGetter? Getter(string? path) => path is null ? null : path.Length == 0 ? static item => item : PropertyPaths.Getter(path);
}

/// <summary>A bound list the engine writes: what reads an item's identity, or nothing when its items have none.</summary>
internal sealed record ListedPanel(ValueGetter? Key);
