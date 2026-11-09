using System.Buffers;
using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>What a part of a slot renderer's control is: a value it reads, a field it writes, or a command it writes.</summary>
public enum SlotPartKind
{
    /// <summary>A value the renderer reads.</summary>
    Read,

    /// <summary>A number field, which also reads its value.</summary>
    Number,

    /// <summary>A text field, which also reads its value.</summary>
    Text,

    /// <summary>A command the renderer's buttons run.</summary>
    Command,

    /// <summary>A choice field: one name over several options (radios, buttons), each written with the value it chooses.</summary>
    Choice,

    /// <summary>A field whose posted value the renderer decodes into the values of other fields it declares.</summary>
    Composite,
}

/// <summary>
/// A part of a control written by a slot renderer, under the key the renderer names it by: a property of the control or
/// of an object it holds (a child in one of its styled properties) that the renderer reads, posts as a field, or runs as
/// a command. A bound one is followed by the plan; one set in the XAML is left to the argument the control records.
/// </summary>
public sealed record SlotPart(SlotPartKind Kind, string Key, object Target, string Property)
{
    /// <summary>The smallest number a number field takes; a posted number below it is clamped to it.</summary>
    public decimal Minimum { get; init; } = decimal.MinValue;

    /// <summary>The largest number a number field takes; a posted number above it is clamped to it.</summary>
    public decimal Maximum { get; init; } = decimal.MaxValue;

    /// <summary>The keys of the fields a composite field's posted value is decoded into, in the order its decoder writes them.</summary>
    public ImmutableArray<string> Members { get; init; } = [];

    /// <summary>Whether the part is a field, posted and written back to its property (or, for a composite, its members').</summary>
    public bool IsField => Kind is SlotPartKind.Number or SlotPartKind.Text or SlotPartKind.Choice or SlotPartKind.Composite;

    /// <summary>Whether the renderer reads the part's value.</summary>
    public bool Reads => Kind is not (SlotPartKind.Command or SlotPartKind.Composite);

    /// <summary>A value the renderer reads under <paramref name="key"/>.</summary>
    public static SlotPart Read(string key, object target, string property) => new(SlotPartKind.Read, key, target, property);

    /// <summary>A number field under <paramref name="key"/>, between <paramref name="minimum"/> and <paramref name="maximum"/>.</summary>
    public static SlotPart Number(string key, object target, string property, decimal minimum, decimal maximum) =>
        new(SlotPartKind.Number, key, target, property) { Minimum = minimum, Maximum = maximum };

    /// <summary>A text field under <paramref name="key"/>.</summary>
    public static SlotPart Text(string key, object target, string property) => new(SlotPartKind.Text, key, target, property);

    /// <summary>A command under <paramref name="key"/>, run with the parameter each button the renderer writes names.</summary>
    public static SlotPart Command(string key, object target, string property) => new(SlotPartKind.Command, key, target, property);

    /// <summary>
    /// A choice field under <paramref name="key"/>, written with <see cref="SlotOutput.WriteFieldOption"/> on each option:
    /// a post sets the text property to the value of the option it names, and only to a value an option was written with.
    /// </summary>
    public static SlotPart Choice(string key, object target, string property) => new(SlotPartKind.Choice, key, target, property);

    /// <summary>
    /// A composite field under <paramref name="key"/>, written with <see cref="SlotOutput.WriteField"/>: its posted value is
    /// decoded by the renderer's <see cref="ISlotRenderer.Decode"/> into one value per member, the number or text fields
    /// declared under <paramref name="members"/>, each then bound as that field is (one input moving several properties).
    /// </summary>
    public static SlotPart Composite(string key, params ImmutableArray<string> members) =>
        new(SlotPartKind.Composite, key, NoTarget, string.Empty) { Members = members };

    /// <summary>The target of a part that has no property of its own (a composite field).</summary>
    public static object NoTarget { get; } = new();
}

/// <summary>How a slot renderer's output finds the values of its parts and the ids of its fields and commands.</summary>
public interface ISlotParts
{
    /// <summary>The value of the part read under <paramref name="key"/>, when it is bound.</summary>
    bool TryRead(string key, object? scope, out object? value);

    /// <summary>Reserves the field under <paramref name="key"/> and writes its id.</summary>
    void WriteFieldId(ref HtmlOutput output, string key, object? scope);

    /// <summary>Reserves a button of the command under <paramref name="key"/> with its parameter and writes its id.</summary>
    void WriteCommandId(ref HtmlOutput output, string key, string parameter, object? scope);

    /// <summary>
    /// Writes the id of the choice field under <paramref name="key"/> for an option with <paramref name="value"/>: reserved
    /// for the first option (<paramref name="reserved"/> is <c>-1</c>), the same for the others; returns the id.
    /// </summary>
    int WriteOptionId(ref HtmlOutput output, string key, string value, int reserved, object? scope);
}

/// <summary>
/// What a slot renderer writes through on one render: the control's value, the bound parts it declares, the buffer its
/// markup goes to, and the names of its fields and commands, which the framework's forms post and bind.
/// </summary>
public ref struct SlotOutput
{
    private HtmlOutput _html;
    private readonly ISlotParts? _parts;
    private readonly object? _scope;
    private List<(string Key, int Id)>? _choices;

    public SlotOutput(HtmlOutput html, object? value, ISlotParts? parts = null, object? scope = null)
    {
        _html = html;
        Value = value;
        _parts = parts;
        _scope = scope;
    }

    /// <summary>The value of the control's property the slot writes from.</summary>
    public readonly object? Value { get; }

    /// <summary>The output once the renderer has written, with the ids its fields reserved.</summary>
    public readonly HtmlOutput Html => _html;

    /// <summary>Where the renderer writes its markup, as UTF-8.</summary>
    public readonly IBufferWriter<byte> Buffer => _html.Buffer;

    /// <summary>The value of the part declared under <paramref name="key"/> when it is bound; false when it is not, and the renderer writes it from its argument.</summary>
    public readonly bool TryRead(string key, out object? value)
    {
        value = null;
        return _parts is not null && _parts.TryRead(key, _scope, out value);
    }

    /// <summary>Writes the attributes of the field declared under <paramref name="key"/>: its name in the form, and the mark that posts the form when it changes.</summary>
    public void WriteField(string key)
    {
        _html.WriteUtf8(" name=\"f"u8);
        Parts(key).WriteFieldId(ref _html, key, _scope);
        _html.WriteUtf8("\" data-change"u8);
    }

    /// <summary>
    /// Writes the attributes of an option of the choice field declared under <paramref name="key"/>: the field's name, the
    /// same on every option of one render, the option's <paramref name="value"/>, and the mark that posts the form when it
    /// is chosen (a radio's change, a submit button's click).
    /// </summary>
    public void WriteFieldOption(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _html.WriteUtf8(" name=\"f"u8);
        var reserved = -1;
        foreach (var (chosen, id) in _choices ?? [])
        {
            reserved = chosen == key ? id : reserved;
        }

        var written = Parts(key).WriteOptionId(ref _html, key, value, reserved, _scope);
        if (reserved < 0)
        {
            (_choices ??= []).Add((key, written));
        }

        _html.WriteUtf8("\" value=\""u8);
        _html.WriteText(value);
        _html.WriteUtf8("\" data-change"u8);
    }

    /// <summary>Writes the attributes of a submit button that runs the command declared under <paramref name="key"/> with <paramref name="parameter"/>.</summary>
    public void WriteCommand(string key, string parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        _html.WriteUtf8(" type=\"submit\" name=\"_btn\" value=\""u8);
        Parts(key).WriteCommandId(ref _html, key, parameter, _scope);
        _html.WriteUtf8("|"u8);
        _html.WriteText(parameter);
        _html.WriteUtf8("\""u8);
    }

    /// <summary>
    /// Whether the page still lacks the rule set under <paramref name="key"/>: a renderer asks before building rules it
    /// contributes with <see cref="WriteStyle"/>, so rules shared by several renderers are built once per page. The
    /// renderer's markup must not depend on the answer, which differs between the plan and the engine.
    /// </summary>
    public readonly bool NeedsStyle(string key) => _html.NeedsStyle(key);

    /// <summary>
    /// Contributes a CSS rule set to the whole page under <paramref name="key"/> (letters, digits, <c>-</c>, <c>_</c>,
    /// <c>.</c>), written once per page in its head however many renderers contribute it, the first contribution winning:
    /// rules shared by several renderers of a page (a chart library's synchronized tooltips) are not repeated in each.
    /// </summary>
    public readonly void WriteStyle(string key, string css) => _html.AddStyle(key, css);

    /// <summary>The error of a field or a command written under a key its control does not declare as one.</summary>
    public static InvalidOperationException Undeclared(string key, string kind) =>
        new($"The slot writer wrote the {kind} '{key}', which its control does not declare as one (SlotPart.{kind switch { "command" => "Command", "choice field" => "Choice", _ => "Number, SlotPart.Text or SlotPart.Composite" }}).");

    private readonly ISlotParts Parts(string key) => _parts ?? throw Undeclared(key, "field or command");
}

/// <summary>A part of a writer node: the path of a bound read, the description of a field, the description of a command.</summary>
public sealed record WriterPart(string Key)
{
    public SlotSpec? Read { get; init; }

    public ValueGetter? Get { get; init; }

    public bool IsField { get; init; }

    public SlotSpec? Field { get; init; }

    public bool IsCommand { get; init; }

    public SlotSpec? Command { get; init; }

    /// <summary>Whether the field is a choice, written once per option.</summary>
    public bool IsChoice { get; init; }
}

/// <summary>A control written by a library's slot writer from the value its source reads and its bound parts, straight to the response.</summary>
public sealed record WriterNode(ValueGetter Get, SlotRender Writer) : IPlanNode, ISlotParts
{
    /// <summary>The getter of a node whose writer reads no value.</summary>
    public static ValueGetter NoValue { get; } = static _ => null;

    public SlotSpec? Source { get; init; }

    public SlotSpec? WriterSpec { get; init; }

    public ImmutableArray<WriterPart> Parts { get; init; } = [];

    /// <summary>Whether the writer writes fields or commands.</summary>
    public bool HasFields => Parts.Any(static part => part.IsField || part.IsCommand);

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        var slot = new SlotOutput(output, Get(scope.Current), this, scope.Current);
        Writer(ref slot);
        output = slot.Html;
    }

    bool ISlotParts.TryRead(string key, object? scope, out object? value)
    {
        foreach (var part in Parts)
        {
            if (part.Get is { } get && part.Key == key)
            {
                value = get(scope);
                return true;
            }
        }

        value = null;
        return false;
    }

    void ISlotParts.WriteFieldId(ref HtmlOutput output, string key, object? scope)
    {
        var part = Find(key, static part => part.IsField && !part.IsChoice) ?? throw SlotOutput.Undeclared(key, "field");
        output.WriteFieldId(output.ReserveField(part.Field, scope, rendered: true));
    }

    void ISlotParts.WriteCommandId(ref HtmlOutput output, string key, string parameter, object? scope)
    {
        var part = Find(key, static part => part.IsCommand) ?? throw SlotOutput.Undeclared(key, "command");
        output.WriteFieldId(output.ReserveButton(part.Command is { } command && output.RecordsFields ? command with { Values = [parameter] } : null, scope, rendered: true));
    }

    int ISlotParts.WriteOptionId(ref HtmlOutput output, string key, string value, int reserved, object? scope)
    {
        var part = Find(key, static part => part.IsChoice) ?? throw SlotOutput.Undeclared(key, "choice field");
        var id = reserved >= 0 ? reserved : output.ReserveField(part.Field, scope, rendered: true);
        output.AddOption(id, value);
        output.WriteFieldId(id);
        return id;
    }

    private WriterPart? Find(string key, Func<WriterPart, bool> kind)
    {
        foreach (var part in Parts)
        {
            if (part.Key == key && kind(part))
            {
                return part;
            }
        }

        return null;
    }
}
