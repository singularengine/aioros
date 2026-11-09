using System.Buffers;
using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>Reads a member of the bound data by the type a value has at run time and the member's name: the app's generated accessors in production, the development engine's fallback otherwise.</summary>
public delegate Func<object, object?> MemberLookup(Type owner, string member);

/// <summary>Writes a control's HTML for one render from its bound value, as UTF-8, without Avalonia.</summary>
public delegate void SlotWrite(object? value, IBufferWriter<byte> output);

/// <summary>Writes a control's HTML for one render through its output: its value, the parts it reads, and the fields and commands it writes.</summary>
public delegate void SlotRender(ref SlotOutput output);

/// <summary>Creates the renderer of one control from the argument it recorded at build time and the lookup that reads members of its data.</summary>
public delegate SlotRender SlotRendererFactory(string argument, MemberLookup members);

/// <summary>
/// Decodes the value posted to a composite field under <paramref name="key"/> into one text per member field, from the
/// members' current values; false refuses the post, which then changes nothing.
/// </summary>
public delegate bool SlotDecode(string key, string posted, ReadOnlySpan<object?> current, Span<string?> decoded);

/// <summary>
/// A library's writer of its controls' HTML on the web: created once per plan from the argument its control recorded at
/// build time, then run on every render with the value the control's binding reads.
/// </summary>
public interface ISlotWriter
{
    /// <summary>The writer of one control, from the argument it recorded at build time (parsed once) and the lookup that reads members of its data.</summary>
    static abstract SlotWrite Create(string argument, MemberLookup members);
}

/// <summary>
/// A library's renderer of its controls' HTML on the web, for a control that reads more than its value: created once per
/// plan from the argument its control recorded at build time, then run on every render with an output that holds the
/// value, the bound parts its control declares (<see cref="SlotPart"/>), and writes the names of its fields and commands.
/// </summary>
public interface ISlotRenderer
{
    /// <summary>The renderer of one control, from the argument it recorded at build time (parsed once) and the lookup that reads members of its data.</summary>
    static abstract SlotRender Create(string argument, MemberLookup members);

    /// <summary>
    /// Decodes the value posted to the composite field declared under <paramref name="key"/> (<see cref="SlotPart.Composite"/>)
    /// into one text per member, in the order the part names them, from the members' current values: a brush's slide posts
    /// its new start and moves the end by the same amount. Refuses every post by default.
    /// </summary>
    static virtual bool Decode(string key, string posted, ReadOnlySpan<object?> current, Span<string?> decoded) => false;
}

/// <summary>Declares a slot writer of a library, which the app's source generator hands to the runtime by name.</summary>
/// <typeparam name="T">The writer.</typeparam>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class SlotWriterAttribute<T> : Attribute
    where T : ISlotWriter
{
    /// <summary>The writer's type.</summary>
    public Type Writer => typeof(T);
}

/// <summary>Declares a slot renderer of a library, which the app's source generator hands to the runtime by name.</summary>
/// <typeparam name="T">The renderer.</typeparam>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class SlotRendererAttribute<T> : Attribute
    where T : ISlotRenderer
{
    /// <summary>The renderer's type.</summary>
    public Type Renderer => typeof(T);
}

/// <summary>A slot writer or renderer by the full name of its type, as a plan names it, with the function that creates it.</summary>
public sealed record SlotWriterEntry(string Name, SlotRendererFactory Create)
{
    /// <summary>The renderer's decoder of its composite fields' posts; none for a writer.</summary>
    public SlotDecode? Decode { get; init; }
}

/// <summary>
/// What a control written by a slot writer records at build time: its writer, the writer's argument, the name of the
/// property whose bound value it writes from (none when it needs no value), and the parts a renderer reads or writes.
/// </summary>
public sealed record WrittenSlot(string Writer, SlotRendererFactory Create, string Argument, string? Property)
{
    /// <summary>The bound values beside the property's, and the fields and commands, the renderer reads or writes.</summary>
    public ImmutableArray<SlotPart> Parts { get; init; } = [];

    /// <summary>The renderer's decoder of its composite fields' posts; none for a writer.</summary>
    public SlotDecode? Decode { get; init; }

    /// <summary>The slot of the writer <typeparamref name="T"/>, named by its type's full name.</summary>
    public static WrittenSlot Of<T>(string argument, string? property)
        where T : ISlotWriter =>
        new(typeof(T).FullName!, SlotWriters.Rendering<T>(), argument, property);

    /// <summary>The slot of the renderer <typeparamref name="T"/>, named by its type's full name, with the parts it reads or writes.</summary>
    public static WrittenSlot Rendered<T>(string argument, string? property, params ImmutableArray<SlotPart> parts)
        where T : ISlotRenderer =>
        new(typeof(T).FullName!, T.Create, argument, property) { Parts = parts, Decode = SlotWriters.Decoder<T>() };
}

/// <summary>A control whose HTML on the web is written by a slot writer from its bound value, instead of being traced from its render.</summary>
public interface IWrittenBySlot
{
    /// <summary>The slot that writes the control, or null to write it as any other control.</summary>
    WrittenSlot? WrittenSlot { get; }
}

/// <summary>The slot writers the app's generated code registers, by name.</summary>
public static class SlotWriters
{
    public const string Kind = "writer";

    private static ImmutableDictionary<string, SlotWriterEntry> _entries = ImmutableDictionary<string, SlotWriterEntry>.Empty.WithComparers(StringComparer.Ordinal);

    public static void Register(ImmutableArray<SlotWriterEntry> entries)
    {
        if (entries.IsDefaultOrEmpty)
        {
            return;
        }

        ImmutableInterlocked.Update(ref _entries, static (all, added) => all.SetItems(added.Select(entry => KeyValuePair.Create(entry.Name, entry))), entries);
    }

    public static SlotWriterEntry? Find(string name) => Volatile.Read(ref _entries).GetValueOrDefault(name);

    /// <summary>The writer <typeparamref name="T"/> as a renderer that writes the value alone.</summary>
    public static SlotRendererFactory Rendering<T>()
        where T : ISlotWriter =>
        static (argument, members) =>
        {
            var write = T.Create(argument, members);
            return (ref SlotOutput output) => write(output.Value, output.Buffer);
        };

    /// <summary>The decoder of the renderer <typeparamref name="T"/>'s composite fields.</summary>
    public static SlotDecode Decoder<T>()
        where T : ISlotRenderer =>
        static (key, posted, current, decoded) => T.Decode(key, posted, current, decoded);

    /// <summary>The spec a writer node records: the writer's name and its argument.</summary>
    public static SlotSpec Spec(string name, string argument) => new(Kind, null, name) { Values = [argument] };

    /// <summary>The writer a writer node's spec names, created from its argument, or an error naming a writer no entry has.</summary>
    public static SlotRender Create(SlotSpec spec, MemberLookup members)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var name = spec.Argument ?? string.Empty;
        var entry = Find(name) ?? throw new System.Text.Json.JsonException(
            $"The slot writer '{name}' has no registered entry: its library declares it with SlotWriterAttribute<T> or SlotRendererAttribute<T>, and the app's generated code registers it.");
        return entry.Create(spec.Values.IsEmpty ? string.Empty : spec.Values[0], members);
    }
}
