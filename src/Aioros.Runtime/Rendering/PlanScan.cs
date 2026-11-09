using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

public static class PlanScan
{
    public static bool HasFields(ImmutableArray<IPlanNode> nodes) => Any(nodes, static node => node switch
    {
        IdNode or ChoiceNode => true,
        WriterNode written => written.HasFields,
        _ => false,
    });

    /// <summary>Whether the nodes write a field of a local input the guarantee does not cover (<see cref="Slots.LocalPath.Guaranteed"/>).</summary>
    public static bool HasUnguaranteedLocals(ImmutableArray<IPlanNode> nodes) => Any(nodes, static node => node switch
    {
        IdNode { Field.Path: { } path } => Slots.LocalPaths.TryRead(path, out var local) && !local.Guaranteed,
        ChoiceNode { Selection.Path: { } path } => Slots.LocalPaths.TryRead(path, out var local) && !local.Guaranteed,
        _ => false,
    });

    /// <summary>Whether the nodes write a slot region (<see cref="SlotRegions"/>) anywhere, so its answers may carry one region alone.</summary>
    public static bool HasSlotRegions(ImmutableArray<IPlanNode> nodes) =>
        Any(nodes, static node => node is SlotRegionNode);

    private static bool Any(ImmutableArray<IPlanNode> nodes, Func<IPlanNode, bool> found) => !nodes.IsDefault && nodes.Any(node => Any(node, found));

    private static bool Any(IPlanNode node, Func<IPlanNode, bool> found) => found(node) || node switch
    {
        ConditionNode condition => Any(condition.Body, found),
        RepeatNode repeat => Any(repeat.Body, found) || repeat.Positions.Any(body => Any(body, found)),
        TreeNode tree => Any(tree.Body, found),
        LevelNode levels => levels.Levels.Any(level => Any(level, found)),
        LearnedNode learned => learned.Bodies.Any(body => Any(body, found)),
        ScopeNode scope => Any(scope.Body, found),
        ViewNode view => Any(view.Body, found) || Any(view.Error, found),
        TypeSwitchNode types => types.Cases.Any(item => Any(item.Body, found)),
        SelectorNode selector => selector.Cases.Any(body => Any(body, found)),
        _ => false,
    };
}
