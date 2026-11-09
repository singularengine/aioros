using System.Collections;
using System.Collections.Immutable;
using System.Text;

namespace Aioros.Runtime.Rendering;

public sealed class LiteralNode(string text) : IPlanNode
{
    private byte[]? _utf8;

    public string Text { get; } = text;

    /// <summary>Writes the text's UTF-8 bytes, encoded on the first write: a plan built only for its fields never encodes them.</summary>
    public void Write(ref HtmlOutput output, RenderScope scope) => output.WriteUtf8(Utf8);

    /// <summary>The text's UTF-8 bytes.</summary>
    internal byte[] Utf8 => Volatile.Read(ref _utf8) ?? Encoded();

    private byte[] Encoded()
    {
        var utf8 = Encoding.UTF8.GetBytes(Text);
        Volatile.Write(ref _utf8, utf8);
        return utf8;
    }
}

public sealed record ValueNode(ValueGetter Get, ValueFormatter? Format = null, bool Raw = false) : IPlanNode
{
    public SlotSpec? Source { get; init; }

    public SlotSpec? FormatSpec { get; init; }

    /// <summary>The format without a string, tried before <see cref="Format"/>.</summary>
    public ValueSpanFormatter? SpanFormat { get; init; }

    /// <summary>The raw markup as UTF-8 bytes, tried before the other formats when the slot is raw.</summary>
    public ValueUtf8Formatter? Utf8Format { get; init; }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        var value = Get(scope.Current);
        if (Raw && Utf8Format is { } utf8)
        {
            output.WriteUtf8(utf8(value));
            return;
        }

        Span<char> formatted = stackalloc char[PlanValues.SpanLength];
        if ((Format is null ? PlanValues.TryFormat(value, formatted, out var written) : SpanFormat is { } span && span(value, formatted, out written)))
        {
            Write(ref output, formatted[..written]);
            return;
        }

        Write(ref output, Format is null ? PlanValues.Format(value) : Format(value));
    }

    private void Write(ref HtmlOutput output, scoped ReadOnlySpan<char> text)
    {
        if (Raw)
        {
            output.WriteRaw(text);
        }
        else
        {
            output.WriteText(text);
        }
    }
}

public sealed record ConditionNode(ValueGetter Get, ImmutableArray<IPlanNode> Body) : IPlanNode
{
    public SlotSpec? Source { get; init; }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        if (Get(scope.Current) is true)
        {
            PlanValues.WriteAll(ref output, Body.AsSpan(), scope);
        }
    }
}

public sealed record RepeatNode(ValueGetter Get, ImmutableArray<IPlanNode> Body) : IPlanNode
{
    public SlotSpec? Source { get; init; }

    /// <summary>What identifies an item, whose key the names of its fields carry; none when the list's items have no identity.</summary>
    public SlotSpec? ItemKey { get; init; }

    public ValueGetter? ItemKeyGetter { get; init; }

    /// <summary>Whether this is a bound list of the page, whose items count in its region's shape.</summary>
    public bool Listed { get; init; }

    /// <summary>The key of an item inside the key around the list.</summary>
    public UInt128? KeyOf(UInt128? outer, object? item) => ItemKeyGetter is { } get ? ItemKeys.Combine(outer, get(item)) : outer;

    /// <summary>
    /// The item's bodies by position when its look depends on it (a library's container state set by index, a
    /// <c>:nth-child</c> style): the first, a middle, the last and an only item, in that order; empty when every item has
    /// <see cref="Body"/>. A position the plan did not learn (an empty body) is a <see cref="PlanMissException"/>.
    /// </summary>
    public ImmutableArray<ImmutableArray<IPlanNode>> Positions { get; init; } = [];

    public bool Positional => !Positions.IsDefaultOrEmpty;

    /// <summary>The body of the item at <paramref name="index"/> of <paramref name="count"/>.</summary>
    public ImmutableArray<IPlanNode> BodyAt(int index, int count)
    {
        if (!Positional)
        {
            return Body;
        }

        var body = Positions[count == 1 ? 3 : index == 0 ? 0 : index == count - 1 ? 2 : 1];
        return body.IsDefault ? throw new PlanMissException($"a list's item at position {index + 1} of {count}, a position the plan did not learn") : body;
    }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        var outerKey = output.ItemKey;
        var mark = Listed ? output.EnterList() : default;
        switch (Get(scope.Current))
        {
            case IList list when list.Count > 0:
                var around = scope.Around();
                for (var index = 0; index < list.Count; index++)
                {
                    Enter(ref output, outerKey, list[index]);
                    PlanValues.WriteAll(ref output, BodyAt(index, list.Count).AsSpan(), around.Item(scope, list[index]));
                }

                break;
            case IEnumerable items and not IList:
                var all = Positional ? items.Cast<object?>().ToList() : null;
                var at = 0;
                foreach (var item in all ?? items)
                {
                    Enter(ref output, outerKey, item);
                    PlanValues.WriteAll(ref output, (all is null ? Body : BodyAt(at++, all.Count)).AsSpan(), scope.With(item));
                }

                break;
        }

        output.ItemKey = outerKey;
        if (Listed)
        {
            output.LeaveList(mark);
        }
    }

    /// <summary>Starts an item: its key for the names written in it, and its place in the region's shape.</summary>
    public void Enter(ref HtmlOutput output, UInt128? outerKey, object? item)
    {
        output.ItemKey = KeyOf(outerKey, item);
        if (Listed)
        {
            output.ListItem(ItemKeyGetter is null ? null : output.ItemKey);
        }
    }
}

/// <summary>Writes the key of the list item being written where a name goes, for a name the plan writes as text.</summary>
public sealed record ItemKeySuffixNode : IPlanNode
{
    public static ItemKeySuffixNode Instance { get; } = new();

    public void Write(ref HtmlOutput output, RenderScope scope) => output.WriteRaw(ItemKeys.Suffix(output.ItemKey));
}

/// <summary>Writes the mark of its region's lists: as the render finds it on a plan, or as the engine worked it out while it wrote.</summary>
public sealed record ShapeNode(string? Mark = null) : IPlanNode
{
    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        if (Mark is null)
        {
            output.WriteShape();
        }
        else if (Mark.Length > 0)
        {
            output.WriteShape(Mark);
        }
    }
}

/// <summary>Sets the key the fields written after it carry: what the engine writes around each list item, whose key it knows as it writes.</summary>
public sealed record ItemKeyNode(UInt128? Key) : IPlanNode
{
    public void Write(ref HtmlOutput output, RenderScope scope) => output.ItemKey = Key;
}

/// <summary>
/// A <c>&lt;select&gt;</c>'s options, each keyed by its index: the selected one is the bound value, matched by its text
/// among fixed options or as Avalonia matches it (<see cref="object.Equals(object, object)"/>) among bound items, or the
/// one at <paramref name="Selected"/> when the selection is not bound. A placeholder is a first option that is disabled
/// and hidden, selected while no item is, so a post never picks the first item; fixed options show their labels (what
/// the item template writes), and a fixed item may be a disabled option or a separator (<c>&lt;hr&gt;</c>).
/// </summary>
public sealed record SelectNode(ValueGetter? Get, int Selected, ImmutableArray<string> Options) : IPlanNode
{
    public const string Disabled = "d";

    public const string Separator = "hr";

    public SlotSpec? Source { get; init; }

    public ValueGetter? Items { get; init; }

    public SlotSpec? ItemsSource { get; init; }

    /// <summary>The selected index, when the control's <c>SelectedIndex</c> is bound: the option at that index is the selected one.</summary>
    public ValueGetter? Index { get; init; }

    public SlotSpec? IndexSource { get; init; }

    public string? Placeholder { get; init; }

    /// <summary>What each fixed option shows, when it differs from its text (its item template's text).</summary>
    public ImmutableArray<string> Labels { get; init; } = [];

    /// <summary>What kind each fixed item is: an option (empty), a disabled option or a separator.</summary>
    public ImmutableArray<string> Kinds { get; init; } = [];

    /// <summary>What each bound item shows (the member its template or display binding reads), when not its text.</summary>
    public ValueGetter? Text { get; init; }

    public SlotSpec? TextSource { get; init; }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        var current = Index is null ? Get?.Invoke(scope.Current) : Index(scope.Current);
        var items = Items is null ? null : (Items(scope.Current) as System.Collections.IEnumerable)?.Cast<object?>().ToList();
        var count = items?.Count ?? Options.Length;
        if (Placeholder is not null)
        {
            var chosen = Enumerable.Range(0, count).Any(index => Kind(index, items) != Separator && Chosen(index, items, current));
            output.WriteUtf8(chosen ? "<option value=\"\" disabled hidden>"u8 : "<option value=\"\" disabled hidden selected>"u8);
            output.WriteText(Placeholder);
            output.WriteUtf8("</option>"u8);
        }

        for (var index = 0; index < count; index++)
        {
            if (Kind(index, items) == Separator)
            {
                output.WriteUtf8("<hr>"u8);
            }
            else
            {
                WriteOption(ref output, index, Label(index, items), Chosen(index, items, current), Kind(index, items) == Disabled);
            }
        }
    }

    /// <summary>Whether the option at the index is the selected one: the bound value matched by text or by item, or the index the control selects.</summary>
    private bool Chosen(int index, List<object?>? items, object? current) => (Get, items) switch
    {
        _ when Index is not null => current is int chosen && index == chosen,
        (null, _) => index == Selected,
        (_, null) => string.Equals(Options[index], PlanValues.Format(current), StringComparison.Ordinal),
        _ => Equals(current, items[index]),
    };

    private string Label(int index, List<object?>? items) => items is null
        ? Labels.IsDefaultOrEmpty ? Options[index] : Labels[index]
        : PlanValues.Format(Text is null ? items[index] : Text(items[index]));

    private string Kind(int index, List<object?>? items) => items is not null || Kinds.IsDefaultOrEmpty ? string.Empty : Kinds[index];

    /// <summary>An option keyed by its index among the items, which a post names to select it.</summary>
    private static void WriteOption(ref HtmlOutput output, int index, string text, bool selected, bool disabled)
    {
        output.WriteUtf8("<option value=\""u8);
        output.WriteText(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        output.WriteUtf8(selected ? "\" selected"u8 : "\""u8);
        output.WriteUtf8(disabled ? " disabled>"u8 : ">"u8);
        output.WriteText(text);
        output.WriteUtf8("</option>"u8);
    }
}

public sealed record ScopeNode(int Model, ImmutableArray<IPlanNode> Body) : IPlanNode
{
    public void Write(ref HtmlOutput output, RenderScope scope) =>
        PlanValues.WriteAll(ref output, Body.AsSpan(), scope.With(scope.Models[Model]));
}

public sealed record IdNode : IPlanNode
{
    public static IdNode Instance { get; } = new();

    public SlotSpec? Field { get; init; }

    /// <summary>A key under which the id is remembered, for the other elements of the field (a radio group's members).</summary>
    public string? Key { get; init; }

    /// <summary>Whether the id is a button's (a post names it in <c>_btn</c>), numbered apart from the fields.</summary>
    public bool Button { get; init; }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        var id = Button ? output.ReserveButton(Field, scope.Current) : output.ReserveField(Field, scope.Current);
        if (Key is not null)
        {
            output.Remember(Key, id);
        }

        output.WriteFieldId(id);
    }
}

/// <summary>
/// Records a control's travelling state written in its hidden input <c>_s{n}</c> (<c>a:Web.State</c>): its index, the
/// model path its property is bound to two way, and its scope, so a planned post restores the posted state into the model
/// before it binds the post's fields, as the engine restores it into the control. It writes nothing.
/// </summary>
public sealed record StateNode(int Index, SlotSpec Field) : IPlanNode
{
    public void Write(ref HtmlOutput output, RenderScope scope) => output.RecordState(Index, Field, scope.Current);
}

/// <summary>
/// Writes the id of a field written before, for its other elements: the <see cref="IdNode"/> with the same key (the
/// members of a radio group share the leader's name), or the id reserved last when there is no key (the radios of a
/// three-state check box).
/// </summary>
public sealed record SameIdNode(string? Key = null) : IPlanNode
{
    public static SameIdNode Instance { get; } = new();

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        if (Key is null)
        {
            output.WriteFieldId(output.LastId);
        }
        else
        {
            output.WriteRecalled(Key);
        }
    }
}

/// <summary>
/// Writes the id of a popover (<c>m1</c>, <c>m2</c>...): a new one, numbered in document order on each render as the
/// writer numbers them in Avalonia's render, so each row of a list gets its own; or, for the popover's other elements
/// (its button's <c>popovertarget</c>, its anchor), the one reserved last under the same key.
/// </summary>
public sealed record PopoverNode(string Key, bool Reserve) : IPlanNode
{
    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        output.WriteUtf8("m"u8);
        output.WriteNumber(Reserve ? output.ReservePopover(Key) : output.RecallPopover(Key));
    }
}

/// <summary>
/// Marks a link to the page being rendered with <c>aria-current="page"</c>: its path equals the request path or is its
/// prefix up to a <c>/</c>, and <c>/</c> only on <c>/</c> (React Router's <c>NavLink</c>, TanStack Router's <c>Link</c>).
/// </summary>
public sealed record CurrentNode(string? Href, ValueGetter? Get = null) : IPlanNode
{
    public SlotSpec? Source { get; init; }

    /// <summary>Marked only on its own path, like React Router's <c>end</c>.</summary>
    public bool Exact { get; init; }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        if (scope.Path is { } path && IsCurrent(path, Href ?? PlanValues.Format(Get?.Invoke(scope.Current)), Exact))
        {
            output.WriteUtf8(" aria-current=\"page\""u8);
        }

        output.WriteUtf8(Exact ? " data-nav data-exact"u8 : " data-nav"u8);
    }

    /// <summary>Whether a link is current for a request: path rule as React Router, query rule as TanStack Router.</summary>
    public static bool IsCurrent(string request, string? href, bool exact = false)
    {
        if (string.IsNullOrEmpty(href) || href[0] != '/')
        {
            return false;
        }

        var hash = href.IndexOf('#', StringComparison.Ordinal);
        var (linkPath, linkQuery) = Split(hash < 0 ? href : href[..hash]);
        var (currentPath, currentQuery) = Split(request);
        var link = Trim(linkPath);
        var current = Trim(currentPath);
        var path = link == "/" || exact
            ? current == link
            : current.StartsWith(link, StringComparison.Ordinal) && (current.Length == link.Length || current[link.Length] == '/');
        if (!path || linkQuery.Length == 0)
        {
            return path;
        }

        var present = Parameters(currentQuery).ToHashSet();
        return Parameters(linkQuery).All(present.Contains);
    }

    private static (string Path, string Query) Split(string url)
    {
        var mark = url.IndexOf('?', StringComparison.Ordinal);
        return mark < 0 ? (url, string.Empty) : (url[..mark], url[(mark + 1)..]);
    }

    private static IEnumerable<(string, string)> Parameters(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(pair =>
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? pair : pair[..equals];
            var value = equals < 0 ? string.Empty : pair[(equals + 1)..];
            return (Decode(name), Decode(value));
        });

    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));

    private static string Trim(string path) => path.Length > 1 && path[^1] == '/' ? path[..^1] : path;
}

/// <summary>One case of a <see cref="TypeSwitchNode"/>: the template for items of a type.</summary>
public sealed record TypeCase(Type Type, ImmutableArray<IPlanNode> Body);

/// <summary>
/// Writes the current item with the first case whose type it is, the way Avalonia picks a <c>DataTemplate</c> by its
/// <c>DataType</c>.
/// </summary>
public sealed record TypeSwitchNode(ImmutableArray<TypeCase> Cases) : IPlanNode
{
    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        foreach (var item in Cases)
        {
            if (item.Type.IsInstanceOfType(scope.Current))
            {
                PlanValues.WriteAll(ref output, item.Body.AsSpan(), scope);
                return;
            }
        }
    }
}

/// <summary>
/// Writes the current item with the template an app's template selector picks for it: the selector, rebuilt with a
/// stand-in for each template it holds, names the case, and an item it does not match or gives none of its templates
/// writes nothing (MAUI's and WinUI's <c>DataTemplateSelector</c> map the template the app's selector returns the same way).
/// </summary>
public sealed record SelectorNode(Func<object?, int> Choose, ImmutableArray<ImmutableArray<IPlanNode>> Cases) : IPlanNode
{
    public SlotSpec? Selector { get; init; }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        var index = Choose(scope.Current);
        if (index >= 0 && index < Cases.Length)
        {
            PlanValues.WriteAll(ref output, Cases[index].AsSpan(), scope);
        }
    }
}
