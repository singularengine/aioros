using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// A library control written by its own slot writer (<see cref="IWrittenBySlot"/>): its box, then the writer's output,
/// as a writer node reading the bound value and the bound parts it declares per render, or, when nothing it reads is
/// bound, as literals around the ids of the fields and commands it writes. A writer that writes fields or commands is
/// marked as a slot region (<see cref="SlotRegionNode"/>), which a post it alone changed is answered with.
/// </summary>
internal static class WrittenSlots
{
    public static bool Handles(Control control) => control is IWrittenBySlot { WrittenSlot: not null };

    public static void Write(HtmlContext context, Control control, ParentKind parent)
    {
        var slot = ((IWrittenBySlot)control).WrittenSlot!;
        var style = context.Box(control, parent);
        var name = global::Avalonia.Automation.AutomationProperties.GetName(control);
        var label = string.IsNullOrEmpty(name) ? string.Empty : $" role=\"img\" aria-label=\"{CssValues.Encode(name)}\"";
        var region = slot.Parts.Any(static part => part.IsField || part.Kind == SlotPartKind.Command);
        context.Plan.Literal($"<div{label}");
        if (region)
        {
            context.Plan.Add(SlotRegionNode.Mark);
        }

        context.Plan.Literal($" class=\"{context.Classes(control, null, style)}\">");
        if (region)
        {
            context.Plan.Add(SlotRegionNode.Open);
        }

        var property = slot.Property is null ? null : AvaloniaPropertyRegistry.Instance.FindRegistered(control, slot.Property);
        if (slot.Property is not null && property is null)
        {
            context.Plan.Fail($"{control.GetType().Name} is written by the slot writer {slot.Writer} from '{slot.Property}', which is not one of its properties");
        }

        var parts = Parts(context, control, slot);
        var writer = slot.Create(slot.Argument, Slots.Resolver.Paths.Lookup);
        var bound = (property is not null && LocalBindings.IsBound(control, property)) || parts.Any(static part => part.IsBound);
        if (!context.Plan.Planning || !bound || !Planned(context, control, property, slot, parts, writer))
        {
            Literal(context, control, property, slot, parts, writer);
        }

        if (region)
        {
            context.Plan.Add(SlotRegionNode.Close);
        }

        context.Plan.Literal("</div>");
    }

    private static ImmutableArray<Part> Parts(HtmlContext context, Control control, WrittenSlot slot)
    {
        var parts = ImmutableArray.CreateBuilder<Part>(slot.Parts.Length);
        foreach (var declared in slot.Parts.Where(static part => part.Kind != SlotPartKind.Composite))
        {
            if (declared.Target is AvaloniaObject target && AvaloniaPropertyRegistry.Instance.FindRegistered(target, declared.Property) is { } property)
            {
                parts.Add(new Part(declared, target, property));
            }
            else
            {
                context.Plan.Fail($"{control.GetType().Name} is written by the slot writer {slot.Writer} with the part '{declared.Key}' on '{declared.Property}', which is not a property of {declared.Target.GetType().Name}");
            }
        }

        foreach (var composite in slot.Parts.Where(static part => part.Kind == SlotPartKind.Composite))
        {
            var members = composite.Members.Select(key => parts.FirstOrDefault(part => part.Declared.Key == key && part.Declared.Kind is SlotPartKind.Number or SlotPartKind.Text)).ToList();
            if (members.Count == 0 || members.Contains(null))
            {
                context.Plan.Fail($"{control.GetType().Name} is written by the slot writer {slot.Writer} with the composite field '{composite.Key}', whose members ({string.Join(", ", composite.Members)}) are not all number or text fields it declares");
                continue;
            }

            parts.Add(new Part(composite, null, null) { Members = [.. members!] });
        }

        return parts.ToImmutable();
    }

    private static bool Planned(HtmlContext context, Control control, AvaloniaProperty? property, WrittenSlot slot, ImmutableArray<Part> parts, SlotRender writer)
    {
        var plan = context.Plan;
        Slot? main = null;
        if (property is not null)
        {
            if (!LocalBindings.IsBound(control, property))
            {
                plan.Fail($"{control.GetType().Name}.{property.Name} is not bound, and its slot writer {slot.Writer} also reads bound parts, so the plan cannot hold its value");
                return false;
            }

            if (plan.PathOf(control, property) is not { } path)
            {
                plan.Fail($"{control.GetType().Name}.{property.Name} is bound through a path the plan cannot follow, and its slot writer reads it");
                return false;
            }

            main = Slots.Path(path);
        }

        var written = ImmutableArray.CreateBuilder<WriterPart>(parts.Length);
        foreach (var part in parts)
        {
            if (Written(plan, part, slot.Writer) is not { } node)
            {
                return false;
            }

            written.Add(node);
        }

        if (SlotWriters.Find(slot.Writer) is null)
        {
            SlotWriters.Register([new SlotWriterEntry(slot.Writer, slot.Create) { Decode = slot.Decode }]);
        }

        plan.Add(new WriterNode(main?.Getter ?? WriterNode.NoValue, writer)
        {
            Source = main?.Spec,
            WriterSpec = SlotWriters.Spec(slot.Writer, slot.Argument),
            Parts = written.MoveToImmutable(),
        });
        return true;
    }

    /// <summary>A part as its writer node holds it: its bound read's path, its field's description, its command's; none when its read cannot be followed.</summary>
    private static WriterPart? Written(PlanRecorder plan, Part part, string writer)
    {
        var declared = part.Declared;
        var node = new WriterPart(declared.Key) { IsField = declared.IsField, IsCommand = declared.Kind == SlotPartKind.Command, IsChoice = declared.Kind == SlotPartKind.Choice };
        if (declared.Reads && part.IsBound)
        {
            if (plan.PathOf(part.Target!, part.Property!) is not { } read)
            {
                plan.Fail($"{part.Target!.GetType().Name}.{part.Property!.Name} is bound through a path the plan cannot follow, and its slot writer reads it");
                return null;
            }

            var source = Slots.Path(read);
            node = node with { Read = source.Spec, Get = source.Getter };
        }

        return node with { Field = node.IsField ? FieldSpec(plan, part, writer) : null, Command = node.IsCommand ? CommandSpec(plan, part) : null };
    }

    /// <summary>
    /// The description a post binds a field through: the path its two-way binding writes, as a number, text or choice
    /// field, or a composite's members' descriptions; none when one of them is not bound both ways.
    /// </summary>
    private static SlotSpec? FieldSpec(PlanRecorder plan, Part part, string writer)
    {
        if (part.Declared.Kind == SlotPartKind.Composite)
        {
            var members = part.Members.Select(member => FieldSpec(plan, member, writer)).ToList();
            return members.Contains(null) ? null : CompositeFields.Spec(writer, part.Declared.Key, members!);
        }

        return plan.PathOf(part.Target!, part.Property!) is { } path && LocalBindings.Of(part.Target!, part.Property!) is { } binding && BindingModes.WritesToSource(binding)
            ? part.Declared.Kind switch
            {
                SlotPartKind.Number => new SlotSpec("number", path) { Values = [Invariant(part.Declared.Minimum), Invariant(part.Declared.Maximum), "clip"] },
                SlotPartKind.Choice => new SlotSpec("choice", path),
                _ => new SlotSpec("text", path),
            }
            : null;
    }

    /// <summary>The description a post runs a command through: its path, as a button's.</summary>
    private static SlotSpec? CommandSpec(PlanRecorder plan, Part part) =>
        (part.Target is Control control ? CommandPaths.Of(plan, control, part.Property!) : plan.PathOf(part.Target!, part.Property!)) is { } path ? new SlotSpec("command", path) : null;

    /// <summary>
    /// Writes the writer's output for the values the control holds now: literals around the ids of the fields and commands
    /// it writes, each noted as a field of the page that a post binds into its part or runs its command through; the other
    /// options of a choice field write the id of its first; the rule sets it contributes to the page are kept as nodes, contributed again on each render.
    /// </summary>
    private static void Literal(HtmlContext context, Control control, AvaloniaProperty? property, WrittenSlot slot, ImmutableArray<Part> parts, SlotRender writer)
    {
        using var buffer = new PooledBuffer(16 * 1024);
        var recording = new Recording(parts, buffer, slot.Decode);
        var styles = new PageStyles();
        var output = new SlotOutput(new HtmlOutput(buffer) { Styles = styles }, property is null ? null : control.GetValue(property), recording);
        writer(ref output);
        foreach (var (key, css) in styles.All)
        {
            context.Plan.Add(new PageStyleNode(key, css));
        }

        var start = 0;
        var owner = context.Fields.Count.ToString(CultureInfo.InvariantCulture);
        foreach (var mark in recording.Marks)
        {
            context.Plan.Literal(Encoding.UTF8.GetString(buffer.WrittenSpan[start..mark.Offset]));
            var key = mark.Choice is { } choice ? $"slot{owner}:{choice}" : null;
            if (mark.Field is { } field)
            {
                var button = field.Command is not null;
                context.Fields.Add(control, field, button);
                context.Plan.Id(null, key, button);
            }
            else
            {
                context.Plan.Add(new SameIdNode(key));
            }

            start = mark.Offset;
        }

        context.Plan.Literal(Encoding.UTF8.GetString(buffer.WrittenSpan[start..]));
    }

    private static string Invariant(decimal number) => number.ToString(CultureInfo.InvariantCulture);

    /// <summary>A declared part, with the property it is on, or, for a composite field, the member fields it is decoded into.</summary>
    private sealed record Part(SlotPart Declared, AvaloniaObject? Target, AvaloniaProperty? Property)
    {
        public ImmutableArray<Part> Members { get; init; } = [];

        public bool IsBound => Target is not null && Property is not null && LocalBindings.IsBound(Target, Property);

        /// <summary>Notes the value the part holds now (its members', for a composite field) and gives back what puts it back.</summary>
        public Action Kept()
        {
            if (Target is null || Property is null)
            {
                var members = Members.Select(static member => member.Kept()).ToList();
                return () => members.ForEach(static put => put());
            }

            var (target, property, value) = (Target, Property, Target.GetValue(Property));
            return () => target.SetValue(property, value);
        }

        /// <summary>Binds a posted value into the part as the plan's number, text and choice fields do.</summary>
        public void Bind(string? value)
        {
            if (value is null || Target is null || Property is null)
            {
                return;
            }

            if (Declared.Kind is SlotPartKind.Text or SlotPartKind.Choice)
            {
                Target.SetValue(Property, value);
                return;
            }

            var type = Property.PropertyType;
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (value.Length == 0)
            {
                if (!type.IsValueType || underlying != type)
                {
                    Target.SetValue(Property, null);
                }

                return;
            }

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                var clamped = Runtime.Slots.NumberRanges.Clamp(Math.Clamp(number, Declared.Minimum, Declared.Maximum), underlying);
                Target.SetValue(Property, Convert.ChangeType(clamped, underlying, CultureInfo.InvariantCulture));
            }
        }
    }

    /// <summary>Where the writer wrote a field's or a command's id: the field a post binds or runs (none for a choice's later options) and the choice it belongs to.</summary>
    private sealed record Mark(int Offset, SlotField? Field, string? Choice);

    /// <summary>The parts of a writer run on the values the control holds now, and where its fields and commands are written.</summary>
    private sealed class Recording(ImmutableArray<Part> parts, PooledBuffer buffer, SlotDecode? decode) : ISlotParts
    {
        private readonly Dictionary<string, List<string>> _options = new(StringComparer.Ordinal);

        public List<Mark> Marks { get; } = [];

        public bool TryRead(string key, object? scope, out object? value)
        {
            foreach (var part in parts)
            {
                if (part.Declared.Reads && part.Declared.Key == key && part.IsBound)
                {
                    value = part.Target!.GetValue(part.Property!);
                    return true;
                }
            }

            value = null;
            return false;
        }

        public void WriteFieldId(ref HtmlOutput output, string key, object? scope)
        {
            var part = parts.FirstOrDefault(part => part.Declared.Key == key && part.Declared.IsField && part.Declared.Kind != SlotPartKind.Choice) ?? throw SlotOutput.Undeclared(key, "field");
            Marks.Add(new Mark(buffer.WrittenSpan.Length, new SlotField(part.Declared.Kind == SlotPartKind.Composite ? value => Decode(part, value) : part.Bind) { Kept = part.Kept, Composite = part.Declared.Kind == SlotPartKind.Composite }, null));
        }

        public void WriteCommandId(ref HtmlOutput output, string key, string parameter, object? scope)
        {
            var part = parts.FirstOrDefault(part => part.Declared.Key == key && part.Declared.Kind == SlotPartKind.Command) ?? throw SlotOutput.Undeclared(key, "command");
            Marks.Add(new Mark(buffer.WrittenSpan.Length, new SlotField(null, () => part.Target!.GetValue(part.Property!) as ICommand, parameter), null));
        }

        public int WriteOptionId(ref HtmlOutput output, string key, string value, int reserved, object? scope)
        {
            var part = parts.FirstOrDefault(part => part.Declared.Key == key && part.Declared.Kind == SlotPartKind.Choice) ?? throw SlotOutput.Undeclared(key, "choice field");
            if (reserved < 0)
            {
                var options = _options[key] = [];
                Marks.Add(new Mark(buffer.WrittenSpan.Length, new SlotField(posted => Choose(part, options, posted)) { Kept = part.Kept }, key));
            }
            else
            {
                Marks.Add(new Mark(buffer.WrittenSpan.Length, null, key));
            }

            _options[key].Add(value);
            return 0;
        }

        private static void Choose(Part part, List<string> options, string? posted)
        {
            if (posted is not null && options.Contains(posted))
            {
                part.Bind(posted);
            }
        }

        /// <summary>Decodes a composite field's post into its members' values from their current ones, and binds each.</summary>
        private void Decode(Part part, string? posted)
        {
            if (posted is null || decode is null)
            {
                return;
            }

            var current = part.Members.Select(member => member.Target!.GetValue(member.Property!)).ToArray();
            var decoded = new string?[current.Length];
            if (!decode(part.Declared.Key, posted, current, decoded))
            {
                return;
            }

            for (var index = 0; index < decoded.Length; index++)
            {
                part.Members[index].Bind(decoded[index]);
            }
        }
    }
}
