using System.Collections;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;

namespace Aioros.Avalonia.Html;

/// <summary>
/// The controls a render writes as form fields, in the order their ids are reserved, each with the token that names it in
/// the form: its id in the page, or its view's slot and its id inside that view, numbered as <see cref="HtmlOutput"/> does.
/// </summary>
internal sealed class FormFields : IReadOnlyList<Control>
{
    private readonly List<Control> _controls = [];
    private readonly List<object?> _contexts = [];
    private readonly List<System.Windows.Input.ICommand?> _commands = [];
    private readonly List<SlotField?> _slots = [];
    private readonly List<KeyBinding?> _keys = [];
    private readonly List<(int View, string? Item, int Id, bool Button, UInt128? Key, bool Listed)> _tokens = [];
    private readonly List<bool> _inert = [];
    private readonly Stack<(int View, string? Item, int Next, int Buttons, UInt128? Key, RegionShape Shape)> _outer = new();
    private readonly Dictionary<ViewTarget, string> _shapes = [];
    private RegionShape _shape = new(ItemKeys.Basis, false, 0, -1);
    private int _view = -1;
    private string? _item;
    private int _next;
    private int _buttons;

    /// <summary>The key of the list item being written, which the fields added now carry.</summary>
    public UInt128? ItemKey { get; set; }

    public int Count => _controls.Count;

    /// <summary>Whether the fields added now are inert: written from a trace of a closed overlay, a post neither binds nor presses them.</summary>
    public bool Inert { get; set; }

    /// <summary>Whether the field at this index is inert (<see cref="Inert"/>).</summary>
    public bool IsInert(int index) => _inert[index];

    public Control this[int index] => _controls[index];

    public void Add(Control control, SlotField? slot = null, bool button = false, KeyBinding? key = null)
    {
        _controls.Add(control);
        _slots.Add(slot);
        _keys.Add(key);
        _contexts.Add(control.DataContext);
        _commands.Add((control as ICommandSource)?.Command);
        if (!button && _shape.Depth > 0 && _shape.First < 0)
        {
            _shape = _shape with { First = _next };
        }

        _tokens.Add((_view, _item, button ? _buttons++ : _next++, button, ItemKey, _shape.Depth > 0));
        _inert.Add(Inert);
    }

    /// <summary>Where the fields stand, for a write whose fields are dropped again (<see cref="Rewind"/>).</summary>
    public (int Count, int Next, int Buttons, RegionShape Shape) Mark() => (_controls.Count, _next, _buttons, _shape);

    /// <summary>Drops the fields added since the mark, giving their numbers back.</summary>
    public void Rewind((int Count, int Next, int Buttons, RegionShape Shape) mark)
    {
        var dropped = _controls.Count - mark.Count;
        _controls.RemoveRange(mark.Count, dropped);
        _contexts.RemoveRange(mark.Count, dropped);
        _slots.RemoveRange(mark.Count, dropped);
        _keys.RemoveRange(mark.Count, dropped);
        _tokens.RemoveRange(mark.Count, dropped);
        _inert.RemoveRange(mark.Count, dropped);
        (_next, _buttons, _shape) = (mark.Next, mark.Buttons, mark.Shape);
    }

    /// <summary>Whether the field at this index is a button, numbered apart from the fields and named only by a post's <c>_btn</c>.</summary>
    public bool IsButton(int index) => _tokens[index].Button;

    /// <summary>
    /// Gives the field at this index back the data context it was written with: a control written more than once (the
    /// items of a menu shared by every row of a list) keeps the last one, and a flyout's controls lose theirs when it closes.
    /// </summary>
    public void Restore(int index)
    {
        if (_contexts[index] is not null && !ReferenceEquals(_controls[index].DataContext, _contexts[index]))
        {
            _controls[index].SetValue(StyledElement.DataContextProperty, _contexts[index], BindingPriority.Style);
        }
    }

    /// <summary>
    /// The command a post runs for the button at this index: its own, or the one it was written with when its binding no
    /// longer resolves (a binding through <c>$parent</c> in an item template of a flyout's content, which leaves the tree
    /// once written), as Avalonia runs it in the open flyout.
    /// </summary>
    public System.Windows.Input.ICommand? Command(int index) => (_controls[index] as ICommandSource)?.Command ?? _commands[index];

    /// <summary>The key binding of the control at this index whose command a post runs, when the button is one (<see cref="Writers.KeyBindingWriters"/>).</summary>
    public KeyBinding? Key(int index) => _keys[index];

    /// <summary>The field or command a slot writer wrote for the control at this index, when it is one.</summary>
    public SlotField? Slot(int index) => _slots[index];

    public bool Contains(Control control) => _controls.Contains(control);

    /// <summary>The view (and item, in a repeated view) the field at this index belongs to; the page's is slot <c>-1</c>.</summary>
    public ViewTarget Target(int index) => new(_tokens[index].View, _tokens[index].Item);

    public string Token(int index) => BareToken(index) + ItemKeys.Suffix(_tokens[index].Key);

    /// <summary>The field's token without its list item's key, the name a post without keys gives it.</summary>
    public string BareToken(int index) => PlanForms.Token(_tokens[index].View, _tokens[index].Id, _tokens[index].Item);

    /// <summary>Whether the field at this index was written in an item of a bound list, so its id moves with the list.</summary>
    public bool Listed(int index) => _tokens[index].Listed;

    /// <summary>The field's id in its region.</summary>
    public int Id(int index) => _tokens[index].Id;

    /// <summary>Starts a bound list, whose items count in the region's shape when they write a field or a button.</summary>
    public ListMark EnterList()
    {
        var mark = new ListMark(_shape.Digest, _next, _buttons);
        _shape = _shape with { Seen = true, Depth = _shape.Depth + 1 };
        return mark;
    }

    /// <summary>Starts an item of a bound list: its place in the shape, by its key when it has an identity.</summary>
    public void ListItem(UInt128? key) => _shape = _shape with { Digest = ItemKeys.Mix(_shape.Digest, key) };

    /// <summary>Ends a bound list; a list none of whose items wrote a field or a button leaves the shape as it was.</summary>
    public void LeaveList(ListMark mark) =>
        _shape = _shape with { Depth = _shape.Depth - 1, Digest = _next == mark.Fields && _buttons == mark.Buttons ? mark.Shape : _shape.Digest };

    /// <summary>The mark of the region's lists as they stand, empty when it wrote no bound list or no field, and notes it for a post.</summary>
    public string Shape()
    {
        if (!_shape.Seen || (_next == 0 && _buttons == 0))
        {
            return string.Empty;
        }

        var mark = ItemKeys.ShapeText(_shape.Digest, _shape.First);
        _shapes[new ViewTarget(_view, _item)] = mark;
        return mark;
    }

    /// <summary>The mark of a region's lists, or nothing when it wrote none.</summary>
    public string? ShapeOf(ViewTarget target) => _shapes.GetValueOrDefault(target);

    /// <summary>Whether a posted token names the field at this index: with its key, or bare.</summary>
    public bool Named(int index, string? token) => token == Token(index) || token == BareToken(index);

    /// <summary>Starts numbering the fields of a view's region (one item's, in a repeated view) from 0.</summary>
    public void EnterView(int view, string? item = null)
    {
        _outer.Push((_view, _item, _next, _buttons, ItemKey, _shape));
        (_view, _item, _next, _buttons, ItemKey, _shape) = (view, item, 0, 0, null, new RegionShape(ItemKeys.Basis, false, 0, -1));
    }

    public void LeaveView() => (_view, _item, _next, _buttons, ItemKey, _shape) = _outer.Pop();

    public IEnumerator<Control> GetEnumerator() => _controls.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// A field or a command button a slot writer wrote for its control: how a post binds the field's value into its part, or
/// the command its button runs and the parameter it was written with.
/// </summary>
internal sealed record SlotField(Action<string?>? Bind, Func<System.Windows.Input.ICommand?>? Command = null, string? Parameter = null)
{
    /// <summary>Notes the values binding the field would overwrite and gives back what puts them back.</summary>
    public Func<Action>? Kept { get; init; }

    /// <summary>Whether the field is a composite, decoded only when a post names it as the field that changed, or names none.</summary>
    public bool Composite { get; init; }
}
