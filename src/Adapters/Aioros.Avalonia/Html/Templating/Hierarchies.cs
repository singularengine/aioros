using System.Collections.Immutable;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Writers;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// A list whose item containers are lists of their own, bound to each item's children (Ursa's <c>NavMenu</c> of
/// <c>NavMenuItem</c>s): planned as a <see cref="TreeNode"/> to any depth whose items write the body of their level
/// (<see cref="LevelNode"/>), since a container's look follows its level. Each level is learned from a container at that
/// level as every look its item can give it: each <c>bool</c> a style binds to the item (a separator, a hidden item) true
/// and false, with children and without (<c>:empty</c>), each set above its binding when the compiled model has no such
/// item; where the container shows its children, the tree writes them one level deeper (<see cref="BranchNode"/>).
/// </summary>
internal static class Hierarchies
{
    public static bool Applies(Control container) =>
        container is ItemsControl list && LocalBindings.Of(list, ItemsControl.ItemsSourceProperty) is not null;

    public static void Write(PlanRecorder plan, ItemsControl owner, string path, Func<Control, ImmutableArray<IPlanNode>> body)
    {
        var levels = ImmutableArray.CreateBuilder<ImmutableArray<IPlanNode>>();
        for (var level = 0; Container(owner, level) is { } container; level++)
        {
            levels.Add(Looks(plan, container, body));
        }

        plan.Add(new TreeNode(PropertyPaths.Getter(path), [new LevelNode(levels.ToImmutable())]) { Source = SlotSpec.Bound(path) });
    }

    /// <summary>The first container at a depth of the compiled tree, the top level being 0.</summary>
    private static ItemsControl? Container(ItemsControl owner, int depth)
    {
        var containers = ItemParts.Containers(owner).OfType<ItemsControl>();
        return depth == 0 ? containers.FirstOrDefault() : containers.Select(container => Container(container, depth - 1)).FirstOrDefault(found => found is not null);
    }

    private static ImmutableArray<IPlanNode> Looks(PlanRecorder plan, ItemsControl container, Func<Control, ImmutableArray<IPlanNode>> body)
    {
        plan.Begin(container.DataContext);
        var children = plan.BoundPath(container, ItemsControl.ItemsSourceProperty);
        var flags = StyleFlags(container);
        plan.End();
        if (children is null)
        {
            plan.Fail($"{container.GetType().Name}.ItemsSource is bound to a path the plan cannot follow");
            return [];
        }

        using var branch = ConstantBindings.Branch(container);
        return Flagged(container, flags, 0, children, body);
    }

    /// <summary>The <c>bool</c> properties of the container a style binds to a path of its item, which change its look.</summary>
    private static List<(AvaloniaProperty Property, string Path)> StyleFlags(ItemsControl container) =>
        [.. AvaloniaPropertyRegistry.Instance.GetRegistered(container)
            .Where(property => property.PropertyType == typeof(bool))
            .Select(property => (Property: property, Binding: BindingOperations.GetBindingExpressionBase(container, property)))
            .Where(found => found.Binding is not null && BindingFormats.Priority(found.Binding) != BindingPriority.LocalValue
                && (found.Binding as IDescription)?.Description is { Length: > 0 } path && ElementBindings.IsSimplePath(path)
                && !BindingSources.Explicit(found.Binding, container, out _))
            .Select(found => (found.Property, ((IDescription)found.Binding!).Description!))];

    private static ImmutableArray<IPlanNode> Flagged(ItemsControl container, List<(AvaloniaProperty Property, string Path)> flags, int index, string children, Func<Control, ImmutableArray<IPlanNode>> body)
    {
        if (index == flags.Count)
        {
            return Filled(container, children, body);
        }

        var (property, path) = flags[index];
        var fallback = (property.GetMetadata(container.GetType()) as IStyledPropertyMetadata)?.DefaultValue is true;
        var cases = ImmutableArray.CreateBuilder<IPlanNode>();
        foreach (var value in new[] { true, false })
        {
            ImmutableArray<IPlanNode> inner;
            using (LocalBindings.Override(container, property, value))
            {
                Settled.Rebuilt(container);
                inner = Flagged(container, flags, index + 1, children, body);
            }

            var test = Flags.Of(path, negate: false, fallback, complement: !value);
            cases.Add(new ConditionNode(test.Getter, inner) { Source = test.Spec });
        }

        Settled.Rebuilt(container);
        return cases.ToImmutable();
    }

    /// <summary>The container written with children and without, its children list set above its binding for the look the item does not give it.</summary>
    private static ImmutableArray<IPlanNode> Filled(ItemsControl container, string children, Func<Control, ImmutableArray<IPlanNode>> body)
    {
        ImmutableArray<IPlanNode> branch;
        using (container.ItemCount > 0 ? null : StandIns.Fill(container, StandIn(container)))
        {
            Settled.Rebuilt(container);
            branch = body(container);
        }

        Settled.Rebuilt(container);

        ImmutableArray<IPlanNode> leaf;
        using (LocalBindings.Override(container, ItemsControl.ItemsSourceProperty, Array.Empty<object>()))
        {
            Settled.Rebuilt(container);
            leaf = body(container);
        }

        Settled.Rebuilt(container);
        var some = new SlotSpec("test", children, "items");
        var none = some with { Argument = "no-items" };
        return [new ConditionNode(Slots.Getter(some), branch) { Source = some }, new ConditionNode(Slots.Getter(none), leaf) { Source = none }];
    }

    /// <summary>A child for a container whose item has none: a stand-in of its own item's type, as the children of a tree's item are items like it.</summary>
    private static object StandIn(ItemsControl container) => container.DataContext switch
    {
        string => string.Empty,
        { } item => System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(item.GetType()),
        null => new object(),
    };
}
