using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// What a view's model slot holds instead of a view model when the view is not shown: <see cref="Hidden"/> when the
/// visitor may not see it (or its loader stopped it), <see cref="Failed"/> when its loader threw.
/// </summary>
public sealed class ViewState
{
    private ViewState(string name) => Name = name;

    public static ViewState Hidden { get; } = new("hidden");

    public static ViewState Failed { get; } = new("failed");

    public string Name { get; }

    public override string ToString() => Name;
}

/// <summary>
/// A view's region: its body reads the view's own model from its slot, isolated from the host's scopes, inside an element
/// named by the instance key; a failed view writes its error body instead and a hidden one writes nothing, so one view's
/// failure never fails the page (Next.js <c>error.js</c> per segment, React error boundaries). In a repeated view's slot
/// (<see cref="ViewItems"/>), the node writes the model of the item it is rendered for, in a region named
/// <c>{key}[{item}]</c>, its fields named under that item.
/// </summary>
public sealed record ViewNode(int Model, string Key, ImmutableArray<IPlanNode> Body, ImmutableArray<IPlanNode> Error) : IPlanNode
{
    private readonly byte[] _open = System.Text.Encoding.UTF8.GetBytes(Open(Key, failed: false));
    private readonly byte[] _failed = System.Text.Encoding.UTF8.GetBytes(Open(Key, failed: true));
    private readonly byte[] _itemOpen = System.Text.Encoding.UTF8.GetBytes(OpenStart + System.Net.WebUtility.HtmlEncode(Key) + "[");
    private static readonly byte[] ItemEnd = System.Text.Encoding.UTF8.GetBytes("]" + OpenEnd(failed: false));
    private static readonly byte[] ItemFailedEnd = System.Text.Encoding.UTF8.GetBytes("]" + OpenEnd(failed: true));

    /// <summary>The item a literal render wrote this region for, in a repeated view; a plan finds it from the item it renders.</summary>
    public string? Item { get; init; }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        var (model, item) = Of(scope);
        if (ReferenceEquals(model, ViewState.Hidden))
        {
            return;
        }

        var failed = ReferenceEquals(model, ViewState.Failed);
        if (item is null)
        {
            output.WriteUtf8(failed ? _failed : _open);
        }
        else
        {
            output.WriteUtf8(_itemOpen);
            output.WriteRaw(ViewItems.Region(item));
            output.WriteUtf8(failed ? ItemFailedEnd : ItemEnd);
        }

        var outer = output.EnterView(Model, item);
        PlanValues.WriteAll(ref output, (failed ? Error : Body).AsSpan(), new RenderScope(failed ? null : model, scope.Models) { Path = scope.Path, Tracked = scope.Tracked });
        output.LeaveView(outer);
        output.WriteUtf8(CloseUtf8);
    }

    /// <summary>The model this node writes in a scope, and the item it writes it for in a repeated view (hidden when the slot has none for it).</summary>
    private (object? Model, string? Item) Of(RenderScope scope)
    {
        var model = scope.Models[Model];
        if (model is not ViewItems items)
        {
            return (model, null);
        }

        var item = Item ?? items.KeyOf(scope.Current);
        return item is null ? (ViewState.Hidden, null) : (items.Model(item), item);
    }

    /// <summary>
    /// The view in these nodes whose model is the slot (and, in a repeated view, the given item), as the render shows it:
    /// inside conditions that hold, scopes and repeats, with the scope it is written in, or nothing when the render does
    /// not show it.
    /// </summary>
    public static (ViewNode View, RenderScope Scope)? Find(ImmutableArray<IPlanNode> nodes, int slot, RenderScope scope, string? item = null)
    {
        foreach (var node in nodes)
        {
            var found = node switch
            {
                ViewNode view when view.Model == slot => view.Shows(scope, item) ? (view, scope) : null,
                ViewNode view when scope.Models[view.Model] is not ViewState and not ViewItems => Find(view.Body, slot, new RenderScope(scope.Models[view.Model], scope.Models) { Path = scope.Path, Tracked = scope.Tracked }, item),
                ScopeNode inner => Find(inner.Body, slot, scope.With(scope.Models[inner.Model]), item),
                ConditionNode condition when condition.Get(scope.Current) is true => Find(condition.Body, slot, scope, item),
                RepeatNode repeat => FindIn(repeat, slot, scope, item),
                ChoiceNode { Items: null } choice => choice.Bodies.Select(body => Find(body, slot, scope, item)).FirstOrDefault(match => match is not null),
                _ => null,
            };
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private bool Shows(RenderScope scope, string? item)
    {
        var (model, shown) = Of(scope);
        return shown == item && !ReferenceEquals(model, ViewState.Hidden);
    }

    private static (ViewNode, RenderScope)? FindIn(RepeatNode repeat, int slot, RenderScope scope, string? item)
    {
        var items = (repeat.Get(scope.Current) as System.Collections.IEnumerable ?? Array.Empty<object>()).Cast<object?>().ToList();
        for (var index = 0; index < items.Count; index++)
        {
            if (Find(repeat.BodyAt(index, items.Count), slot, scope.With(items[index]), item) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>The opening of a view's region, the same in Avalonia's render and the plan's.</summary>
    public static string Open(string key, bool failed) => OpenStart + System.Net.WebUtility.HtmlEncode(key) + OpenEnd(failed);

    private const string OpenStart = "<div data-aioros-view=\"";

    private static string OpenEnd(bool failed) => failed ? "\" data-view-error role=\"alert\" style=\"display:contents\">" : "\" style=\"display:contents\">";

    public const string Close = "</div>";

    private static ReadOnlySpan<byte> CloseUtf8 => "</div>"u8;
}
