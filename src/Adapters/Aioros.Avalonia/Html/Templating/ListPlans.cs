using System.Collections.Immutable;
using Aioros.Avalonia.Html.Variants;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>A list bound to the model, or shown by a template from its control's bound list, planned as one repeat: from its first item, from stand-ins of its typed templates or of an app's template selector's, or from a stand-in when it is empty.</summary>
internal static class ListPlans
{
    /// <summary>
    /// Whether the list's items are planned as a repeat: its items source is bound to the model, or shown from its templated
    /// control's list that reads the model by an items control of Avalonia's (a library's own may give each item a look from
    /// its code, as Ursa's <c>Anchor</c> marks the item in view), and neither is learned by value with its control
    /// (<see cref="LibraryValues"/>).
    /// </summary>
    public static bool Applies(PlanRecorder plan, ItemsControl items) =>
        LocalBindings.IsBound(items, ItemsControl.ItemsSourceProperty)
            ? !ConstantBindings.IsLearnedConstant(items, ItemsControl.ItemsSourceProperty)
            : !LibraryValues.FromLibrary(items.GetType()) && plan.TemplateShown(items, ItemsControl.ItemsSourceProperty) is { } shown && !ConstantBindings.IsLearnedConstant(shown.Parent, shown.Property)
              && LocalBindings.Of(shown.Parent, shown.Property) is IDescription { Description: { } path } && ElementBindings.IsSimplePath(path);

    public static void Write(PlanRecorder plan, ItemsControl items, Panel panel, Func<Control, ImmutableArray<IPlanNode>> body, Func<Control, ImmutableArray<IPlanNode>> listed)
    {
        var path = plan.BoundPath(items, ItemsControl.ItemsSourceProperty) ?? Shown(plan, items);
        if (path is null)
        {
            plan.Fail("an ItemsControl without a bound ItemsSource");
            return;
        }

        if (ConstantBindings.IsBranch(items))
        {
            var children = SlotSpec.Bound(path);
            plan.Add(new BranchNode(Slots.Getter(children)) { Source = children });
            return;
        }

        if (TemplateSelectors.IsSelector(items.ItemTemplate))
        {
            ChosenItems.Write(plan, items, panel, path, body);
            return;
        }

        if (TypedItems.Templates(items) is { Count: > 1 } templates)
        {
            TypedItems.Write(plan, items, panel, path, templates, body);
            return;
        }

        if (panel.Children.FirstOrDefault() is Control first)
        {
            Learn(plan, items, path, first, body, listed);
            return;
        }

        using var standIn = StandIns.Fill(items) ?? (StandIns.ItemType(plan.PathType(path)) is { } declared ? StandIns.Fill(items, declared) : null);
        if (standIn is null || panel.Children.FirstOrDefault() is not Control learned)
        {
            plan.Fail("an empty list: there is no item to learn the item template from");
            return;
        }

        Learn(plan, items, path, learned, body, listed);
    }

    /// <summary>The path of a list its template shows from its templated control's bound list (<c>ItemsSource="{TemplateBinding Items}"</c>), followed.</summary>
    private static string? Shown(PlanRecorder plan, ItemsControl items)
    {
        if (plan.TemplateShown(items, ItemsControl.ItemsSourceProperty) is not { } shown)
        {
            return null;
        }

        plan.Follow(items, ItemsControl.ItemsSourceProperty);
        return plan.BoundPath(shown.Parent, shown.Property);
    }

    private static void Learn(PlanRecorder plan, ItemsControl items, string path, Control first, Func<Control, ImmutableArray<IPlanNode>> body, Func<Control, ImmutableArray<IPlanNode>> listed)
    {
        if (Hierarchies.Applies(first))
        {
            Hierarchies.Write(plan, items, path, listed);
            return;
        }

        if (ItemPositions.Matter(items) && ItemPositions.Learn(items, first, body) is { IsEmpty: false } positions)
        {
            plan.Add(ItemIdentity.Keyed(plan, new RepeatNode(PropertyPaths.Getter(path), positions[0]) { Source = SlotSpec.Bound(path), Positions = ItemPositions.Same(positions) ? [] : positions }, ItemIdentity.Path(items, first.DataContext)));
            return;
        }

        plan.Add(ItemIdentity.Keyed(plan, new RepeatNode(PropertyPaths.Getter(path), body(first)) { Source = SlotSpec.Bound(path) }, ItemIdentity.Path(items, first.DataContext)));
    }
}
