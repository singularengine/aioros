using System.Buffers;
using System.Text;

namespace Aioros.Runtime.Rendering;

public ref struct HtmlOutput(IBufferWriter<byte> writer, FieldLog? fields = null)
{
    private static readonly SearchValues<char> Escaped = SearchValues.Create("&<>\"");

    private readonly IBufferWriter<byte> _writer = writer;
    private int _nextId;
    private int _nextButton;
    private int _view = -1;
    private string? _item;

    /// <summary>The key of the list item being written, carried by the names of its fields; none outside a keyed list.</summary>
    public UInt128? ItemKey { get; set; }
    private Dictionary<string, (int Id, UInt128? ItemKey)>? _keyed;
    private uint _shape = ItemKeys.Basis;
    private bool _listSeen;
    private int _listDepth;
    private int _firstListed = -1;
    private int _popovers;

    /// <summary>The tree whose items are being written, for their branches and indents.</summary>
    public TreeFrame? Tree { readonly get; set; }
    private Dictionary<string, int>? _popoverIds;

    internal readonly IBufferWriter<byte> Buffer => _writer;

    /// <summary>Where the render notes its slot regions and takes its nonce from; none writes an empty nonce.</summary>
    public SlotRegionLog? Regions { readonly get; init; }

    /// <summary>Writes a slot region's mark, noting where it is in <see cref="Regions"/>.</summary>
    public readonly void WriteSlotRegion(SlotRegionPart part)
    {
        switch (part)
        {
            case SlotRegionPart.Mark:
                WriteUtf8(" data-aioros-slot=\""u8);
                if (Regions is { } log)
                {
                    WriteRaw(log.Nonce);
                    log.NonceAt(log.Nonce.Length);
                }

                WriteUtf8("\""u8);
                break;
            case SlotRegionPart.Open:
                WriteUtf8("<!--s-->"u8);
                Regions?.Opened();
                break;
            default:
                Regions?.Closed();
                WriteUtf8("<!--/s-->"u8);
                break;
        }
    }

    /// <summary>Where the render collects the rule sets its slot renderers contribute to the page; none drops them.</summary>
    public PageStyles? Styles { readonly get; init; }

    /// <summary>Whether the page still lacks a rule set under this key, so a renderer builds it only once per page.</summary>
    public readonly bool NeedsStyle(string key)
    {
        PageStyles.Check(key);
        return Styles is { } styles && !styles.Has(key);
    }

    /// <summary>Contributes a rule set to the page under a key, unless one was contributed under it already.</summary>
    public readonly void AddStyle(string key, string css)
    {
        PageStyles.Check(key);
        Styles?.Add(key, css);
    }

    /// <summary>Whether the render records the fields it reserves, as a post's does.</summary>
    public readonly bool RecordsFields => fields is not null;

    public readonly void WriteUtf8(ReadOnlySpan<byte> utf8) => _writer.Write(utf8);

    public readonly void WriteRaw(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        var span = _writer.GetSpan(Encoding.UTF8.GetMaxByteCount(text.Length));
        _writer.Advance(Encoding.UTF8.GetBytes(text, span));
    }

    public readonly void WriteText(ReadOnlySpan<char> text)
    {
        while (!text.IsEmpty)
        {
            var next = text.IndexOfAny(Escaped);
            if (next < 0)
            {
                WriteRaw(text);
                return;
            }

            WriteRaw(text[..next]);
            WriteUtf8(text[next] switch
            {
                '&' => "&amp;"u8,
                '<' => "&lt;"u8,
                '>' => "&gt;"u8,
                _ => "&quot;"u8,
            });
            text = text[(next + 1)..];
        }
    }

    public void WriteId() => WriteNumber(ReserveId());

    public int ReserveId() => _nextId++;

    /// <summary>The id reserved last.</summary>
    public readonly int LastId => _nextId - 1;

    /// <summary>Remembers an id under a key, for the other elements of the same field.</summary>
    public void Remember(string key, int id) => (_keyed ??= new Dictionary<string, (int Id, UInt128? ItemKey)>(StringComparer.Ordinal))[key] = (id, ItemKey);

    /// <summary>The id last remembered under the key, or the id reserved last when there is none.</summary>
    public readonly int Recall(string key) => _keyed is not null && _keyed.TryGetValue(key, out var kept) ? kept.Id : LastId;

    /// <summary>Writes the field id remembered under the key with its own item's key, as the field was named wherever this reference stands.</summary>
    public readonly void WriteRecalled(string key)
    {
        if (_keyed is not null && _keyed.TryGetValue(key, out var kept))
        {
            WriteFieldId(kept.Id, kept.ItemKey);
        }
        else
        {
            WriteFieldId(LastId);
        }
    }

    /// <summary>Numbers the next popover of the page, from 1 in document order, and remembers it under its key.</summary>
    public int ReservePopover(string key)
    {
        var id = ++_popovers;
        (_popoverIds ??= new Dictionary<string, int>(StringComparer.Ordinal))[key] = id;
        return id;
    }

    /// <summary>The popover id reserved last under the key.</summary>
    public readonly int RecallPopover(string key) => _popoverIds is not null && _popoverIds.TryGetValue(key, out var id) ? id : _popovers;

    public int ReserveField(SlotSpec? field, object? scope, bool rendered = false)
    {
        var id = ReserveId();
        if (_listDepth > 0 && _firstListed < 0)
        {
            _firstListed = id;
        }

        fields?.Record(id, field, scope, _view, _item, rendered, itemKey: ItemKey, listed: _listDepth > 0);
        return id;
    }

    /// <summary>
    /// Reserves the id of a button (anything a post names in <c>_btn</c>), numbered apart from the fields, so the fields'
    /// names stay the same however many buttons a render writes before them.
    /// </summary>
    public int ReserveButton(SlotSpec? field, object? scope, bool rendered = false)
    {
        var id = _nextButton++;
        fields?.Record(id, field, scope, _view, _item, rendered, button: true, itemKey: ItemKey, listed: _listDepth > 0);
        return id;
    }

    /// <summary>Records a travelling state's index, its binding and its scope for a post to restore (<see cref="StateNode"/>).</summary>
    public readonly void RecordState(int index, SlotSpec field, object? scope) => fields?.State(index, field, scope, ItemKey);

    /// <summary>Starts a bound list, whose items count in the region's shape when they write a field or a button.</summary>
    public ListMark EnterList()
    {
        _listSeen = true;
        _listDepth++;
        return new ListMark(_shape, _nextId, _nextButton);
    }

    /// <summary>Counts an item of the list in the region's shape, by its key when it has an identity.</summary>
    public void ListItem(UInt128? key) => _shape = ItemKeys.Mix(_shape, key);

    /// <summary>Ends a bound list; a list none of whose items wrote a field or a button leaves the shape as it was.</summary>
    public void LeaveList(ListMark mark)
    {
        _listDepth--;
        if (_nextId == mark.Fields && _nextButton == mark.Buttons)
        {
            _shape = mark.Shape;
        }
    }

    /// <summary>Writes the mark of the region's lists, when it wrote a bound list and a field: what tells a post whether the lists are still the ones its page wrote.</summary>
    public readonly void WriteShape()
    {
        if (_listSeen && (_nextId > 0 || _nextButton > 0))
        {
            WriteShape(ItemKeys.ShapeText(_shape, _firstListed));
        }
    }

    /// <summary>Writes a region's shape mark and records it for a post.</summary>
    public readonly void WriteShape(string mark)
    {
        var target = new ViewTarget(_view, _item);
        fields?.Shape(target, mark);
        WriteUtf8("<input type=\"hidden\" name=\""u8);
        WriteRaw(PlanForms.ShapeName(target));
        WriteUtf8("\" value=\""u8);
        WriteRaw(mark);
        WriteUtf8("\">"u8);
    }

    /// <summary>Records an option's value for the choice field reserved with this id, the values a post may bind into it.</summary>
    public readonly void AddOption(int id, string value) => fields?.Option(id, _view, _item, value);

    /// <summary>
    /// Starts the field ids of a view's region: they are numbered from 0 under the view's slot, so the region numbers them
    /// the same whether it is written inside the page or alone; returns what <see cref="LeaveView"/> restores.
    /// </summary>
    public FieldIds EnterView(int view, string? item = null)
    {
        var outer = new FieldIds(_nextId, _view, _keyed) { Item = _item, Buttons = _nextButton, ItemKey = ItemKey, Shape = new RegionShape(_shape, _listSeen, _listDepth, _firstListed) };
        (_nextId, _nextButton, _view, _keyed, _item, ItemKey) = (0, 0, view, null, item, null);
        (_shape, _listSeen, _listDepth, _firstListed) = (ItemKeys.Basis, false, 0, -1);
        return outer;
    }

    /// <summary>Goes back to the ids of the region around a view.</summary>
    public void LeaveView(FieldIds outer)
    {
        (_nextId, _nextButton, _view, _keyed, _item, ItemKey) = (outer.Next, outer.Buttons, outer.View, outer.Keyed, outer.Item, outer.ItemKey);
        (_shape, _listSeen, _listDepth, _firstListed) = (outer.Shape.Digest, outer.Shape.Seen, outer.Shape.Depth, outer.Shape.First);
    }

    /// <summary>
    /// Writes a field id as forms name it: the id in the page, <c>{slot}.{id}</c> inside a view, <c>{slot}-{item}.{id}</c>
    /// inside one item of a repeated view.
    /// </summary>
    public readonly void WriteFieldId(int id) => WriteFieldId(id, ItemKey);

    private readonly void WriteFieldId(int id, UInt128? itemKey)
    {
        if (_view >= 0)
        {
            WriteNumber(_view);
            if (_item is not null)
            {
                WriteUtf8("-"u8);
                WriteRaw(ViewItems.Token(_item));
            }

            WriteUtf8("."u8);
        }

        WriteNumber(id);
        if (itemKey is not null)
        {
            WriteRaw(ItemKeys.Suffix(itemKey));
        }
    }

    public readonly void WriteNumber(int value)
    {
        Span<char> digits = stackalloc char[11];
        value.TryFormat(digits, out var written, provider: System.Globalization.CultureInfo.InvariantCulture);
        WriteRaw(digits[..written]);
    }
}

/// <summary>The field ids of a region, kept while a view inside it numbers its own.</summary>
public readonly record struct FieldIds(int Next, int View, Dictionary<string, (int Id, UInt128? ItemKey)>? Keyed)
{
    /// <summary>The item of the repeated view around, when there is one.</summary>
    public string? Item { get; init; }

    /// <summary>The next button id of the region around.</summary>
    public int Buttons { get; init; }

    /// <summary>The key of the list item around, when there is one.</summary>
    public UInt128? ItemKey { get; init; }

    /// <summary>The shape of the lists of the region around.</summary>
    public RegionShape Shape { get; init; }
}

/// <summary>The shape of a region's lists so far: its digest, whether it wrote a bound list, how deep the list being written is, and the id of the first field written in a list.</summary>
public readonly record struct RegionShape(uint Digest, bool Seen, int Depth, int First);

/// <summary>Where a region stood when a bound list began.</summary>
public readonly record struct ListMark(uint Shape, int Fields, int Buttons);
