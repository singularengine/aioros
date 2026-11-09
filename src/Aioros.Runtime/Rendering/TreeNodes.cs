using System.Collections;
using System.Collections.Immutable;
using System.Globalization;

namespace Aioros.Runtime.Rendering;

/// <summary>The tree being written: the body of its items, the scope it was written in, and the depth of the items being written.</summary>
public sealed record TreeFrame(ImmutableArray<IPlanNode> Body, RenderScope Host, RenderScope Around)
{
    public int Depth { get; init; }

    /// <summary>What identifies an item of the tree, when its items have an identity.</summary>
    public ValueGetter? ItemKey { get; init; }

    /// <summary>Whether the tree's items count in its region's shape.</summary>
    public bool Listed { get; init; }
}

/// <summary>
/// A tree of items to any depth (a <c>TreeView</c> with a bound <c>ItemsSource</c>): each item at the top writes the
/// body, and a <see cref="BranchNode"/> in it writes the item's children with the same body one level deeper, as
/// Avalonia's <c>TreeDataTemplate</c> gives each item its children.
/// </summary>
public sealed record TreeNode(ValueGetter Get, ImmutableArray<IPlanNode> Body) : IPlanNode
{
    public SlotSpec? Source { get; init; }

    public SlotSpec? ItemKey { get; init; }

    public ValueGetter? ItemKeyGetter { get; init; }

    public bool Listed { get; init; }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        if (Get(scope.Current) is not IEnumerable items)
        {
            return;
        }

        var outer = output.Tree;
        var frame = new TreeFrame(Body, scope, scope.Around()) { ItemKey = ItemKeyGetter, Listed = Listed };
        var mark = Listed ? output.EnterList() : default;
        output.Tree = frame;
        BranchNode.WriteItems(ref output, frame, items);
        output.Tree = outer;
        if (Listed)
        {
            output.LeaveList(mark);
        }
    }
}

/// <summary>The children of the current item of a <see cref="TreeNode"/>, each written with the tree's body one level deeper.</summary>
public sealed record BranchNode(ValueGetter Get) : IPlanNode
{
    public SlotSpec? Source { get; init; }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        if (output.Tree is not { } frame || Get(scope.Current) is not IEnumerable children)
        {
            return;
        }

        output.Tree = frame with { Depth = frame.Depth + 1 };
        WriteItems(ref output, output.Tree, children);
        output.Tree = frame;
    }

    internal static void WriteItems(ref HtmlOutput output, TreeFrame frame, IEnumerable items)
    {
        var around = output.ItemKey;
        foreach (var item in items)
        {
            if (frame.Listed)
            {
                output.ItemKey = frame.ItemKey is { } key ? ItemKeys.Combine(around, key(item)) : around;
                output.ListItem(frame.ItemKey is null ? null : output.ItemKey);
            }

            PlanValues.WriteAll(ref output, frame.Body.AsSpan(), frame.Around.Item(frame.Host, item));
        }

        output.ItemKey = around;
    }
}

/// <summary>The indent of a tree row at the depth of the item being written: <c>depth × indent + offset</c> pixels, as <c>TreeViewItem.Level</c> indents it.</summary>
public sealed record DepthNode(double Indent, double Offset) : IPlanNode
{
    public void Write(ref HtmlOutput output, RenderScope scope) => output.WriteRaw(Pixels(output.Tree?.Depth ?? 0, Indent, Offset));

    public static string Pixels(int depth, double indent, double offset) =>
        (Math.Round((depth * indent) + offset, 3) + 0.0).ToString(CultureInfo.InvariantCulture) + "px";
}

/// <summary>
/// The body of a tree's item learned for its depth, as an item's look follows its level (Ursa's <c>NavMenuItem.Level</c>
/// sets its indent): the item at depth <c>d</c> writes the <c>d</c>-th body; an item deeper than any depth learned is a
/// <see cref="PlanMissException"/>, since the plan cannot tell how Avalonia shows it.
/// </summary>
public sealed record LevelNode(ImmutableArray<ImmutableArray<IPlanNode>> Levels) : IPlanNode
{
    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        var depth = output.Tree?.Depth ?? 0;
        if (depth >= Levels.Length)
        {
            throw new PlanMissException($"an item at level {(depth + 1).ToString(CultureInfo.InvariantCulture)}, deeper than the {Levels.Length.ToString(CultureInfo.InvariantCulture)} levels the plan learned");
        }

        PlanValues.WriteAll(ref output, Levels[depth].AsSpan(), scope);
    }
}
