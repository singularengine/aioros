using System.Collections;
using System.Collections.Immutable;
using System.Text;

namespace Aioros.Runtime.Rendering;

public delegate bool ChoicePredicate(object? scope, int index, object? item);

public sealed record ChoiceMarkup(string Open, string SelectedOpen, string? Input, string Close)
{
    public byte[] OpenUtf8 { get; } = Encoding.UTF8.GetBytes(Open);

    public byte[] SelectedOpenUtf8 { get; } = Encoding.UTF8.GetBytes(SelectedOpen);

    public byte[]? InputUtf8 { get; } = Input is null ? null : Encoding.UTF8.GetBytes(Input);

    public byte[] CloseUtf8 { get; } = Encoding.UTF8.GetBytes(Close);

    /// <summary>The input is written in the selected item alone, as a hidden field naming its index (a tab's), not as a checked input in every item.</summary>
    public bool InputOnSelected { get; init; }
}

public sealed record ChoiceNode(ValueGetter? Items, ChoicePredicate IsSelected, ChoiceMarkup Markup, ImmutableArray<ImmutableArray<IPlanNode>> Bodies) : IPlanNode
{
    public SlotSpec? ItemsSource { get; init; }

    public SlotSpec? Selection { get; init; }

    public SlotSpec? Field { get; init; }

    /// <summary>What identifies an item of a bound list, whose key the names of the fields in it carry.</summary>
    public SlotSpec? ItemKey { get; init; }

    public ValueGetter? ItemKeyGetter { get; init; }

    /// <summary>Whether this is a bound list whose items count in its region's shape.</summary>
    public bool Listed { get; init; }

    /// <summary>The item's markup by position when its look depends on it (a <c>:nth-child</c> style): the first, a middle, the last and an only item, in that order; empty when every item has <see cref="ChoiceNode.Markup"/>.</summary>
    public ImmutableArray<ChoiceMarkup> Positions { get; init; } = [];

    /// <summary>The markup of the item at <paramref name="index"/> of <paramref name="count"/>.</summary>
    public ChoiceMarkup MarkupAt(int index, int count) =>
        index < Each.Length ? Each[index] : Positions.IsDefaultOrEmpty ? Markup : Positions[count == 1 ? 3 : index == 0 ? 0 : index == count - 1 ? 2 : 1];

    /// <summary>The markup of each of a fixed list's items, when they differ (a tab's own look, a disabled item); empty otherwise.</summary>
    public ImmutableArray<ChoiceMarkup> Each { get; init; } = [];

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        var group = Markup.InputUtf8 is null ? -1 : output.ReserveField(Field, scope.Current);
        if (Items is null)
        {
            for (var index = 0; index < Bodies.Length; index++)
            {
                WriteItem(ref output, scope, scope, group, (index, Bodies.Length), null, Bodies[index]);
            }

            return;
        }

        if (Items(scope.Current) is not IEnumerable items || Bodies.IsEmpty)
        {
            return;
        }

        var all = Positions.IsDefaultOrEmpty ? null : items.Cast<object?>().ToList();
        var position = 0;
        var mark = Listed ? output.EnterList() : default;
        foreach (var item in all ?? items)
        {
            WriteItem(ref output, scope, scope.With(item), group, (position++, all?.Count ?? 0), item, Bodies[0]);
        }

        if (Listed)
        {
            output.LeaveList(mark);
        }
    }

    private void WriteItem(ref HtmlOutput output, RenderScope outer, RenderScope inner, int group, (int Index, int Count) at, object? item, ImmutableArray<IPlanNode> body)
    {
        var index = at.Index;
        var markup = MarkupAt(index, at.Count);
        var selected = IsSelected(outer.Current, index, item);
        output.WriteUtf8(selected ? markup.SelectedOpenUtf8 : markup.OpenUtf8);
        if (markup.InputUtf8 is { } input && (selected || !markup.InputOnSelected))
        {
            output.WriteUtf8(input);
            output.WriteUtf8(" name=\"f"u8);
            output.WriteFieldId(group);
            output.WriteUtf8("\" value=\""u8);
            output.WriteNumber(index);
            output.WriteUtf8(selected && !markup.InputOnSelected ? "\" checked>"u8 : "\">"u8);
        }

        var around = output.ItemKey;
        if (Listed)
        {
            output.ItemKey = ItemKeyGetter is { } key ? ItemKeys.Combine(around, key(item)) : around;
            output.ListItem(ItemKeyGetter is null ? null : output.ItemKey);
        }

        PlanValues.WriteAll(ref output, body.AsSpan(), inner);
        output.ItemKey = around;
        output.WriteUtf8(markup.CloseUtf8);
    }
}
