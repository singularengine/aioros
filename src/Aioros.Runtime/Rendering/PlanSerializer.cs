using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Aioros.Runtime.Rendering;

public static class PlanSerializer
{
    public static PlanWrite TryWrite(ImmutableArray<IPlanNode> nodes)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            if (WriteNodes(writer, nodes) is { } failure)
            {
                return new PlanWrite(null, failure);
            }
        }

        return new PlanWrite(Encoding.UTF8.GetString(stream.ToArray()), null);
    }

    public static ImmutableArray<IPlanNode> Read(string json, ISlotResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        using var document = JsonDocument.Parse(json);
        return new PlanReader(resolver).Nodes(document.RootElement);
    }

    private static string? WriteNodes(Utf8JsonWriter writer, ImmutableArray<IPlanNode> nodes)
    {
        writer.WriteStartArray();
        foreach (var node in nodes)
        {
            if (WriteNode(writer, node) is { } failure)
            {
                return failure;
            }
        }

        writer.WriteEndArray();
        return null;
    }

    private static string? WriteNode(Utf8JsonWriter writer, IPlanNode node)
    {
        writer.WriteStartObject();
        var failure = node switch
        {
            LiteralNode literal => Literal(writer, literal),
            SlotRegionNode region => Region(writer, region),
            IdNode id => Id(writer, id),
            StateNode state => State(writer, state),
            StateValueNode value => StateValue(writer, value),
            SameIdNode same => SameId(writer, same),
            PopoverNode popover => Popover(writer, popover),
            TreeNode tree => Tree(writer, tree),
            ShapeNode shape => Shape(writer, shape),
            ItemKeySuffixNode => Suffix(writer),
            BranchNode branch => Branch(writer, branch),
            DepthNode depth => Depth(writer, depth),
            LevelNode levels => Levels(writer, levels),
            LearnedNode learned => Learned(writer, learned),
            CurrentNode current => Current(writer, current),
            TypeSwitchNode types => Types(writer, types),
            ValueNode value => Value(writer, value),
            WriterNode written => Written(writer, written),
            ConditionNode condition => Bodied(writer, "if", condition.Source, condition.Body),
            RepeatNode repeat => Repeat(writer, repeat),
            ScopeNode scope => Scope(writer, scope),
            ViewNode view => View(writer, view),
            SelectNode select => Select(writer, select),
            ChoiceNode choice => Choice(writer, choice),
            SelectorNode selector => Selector(writer, selector),
            PageStyleNode style => Style(writer, style),
            _ => $"{node.GetType().Name} cannot be serialized",
        };
        if (failure is null)
        {
            writer.WriteEndObject();
        }

        return failure;
    }

    private static string? Tree(Utf8JsonWriter writer, TreeNode tree)
    {
        var failure = Bodied(writer, "tree", tree.Source, tree.Body);
        Identity(writer, tree.ItemKey, tree.Listed);
        return failure;
    }

    private static string? Shape(Utf8JsonWriter writer, ShapeNode shape)
    {
        if (shape.Mark is not null)
        {
            return "an engine's shape mark";
        }

        writer.WriteString("t", "shape");
        return null;
    }

    private static string? Suffix(Utf8JsonWriter writer)
    {
        writer.WriteString("t", "item-key");
        return null;
    }

    private static void Identity(Utf8JsonWriter writer, SlotSpec? key, bool listed)
    {
        if (key is not null)
        {
            Spec(writer, "k", key);
        }

        if (listed)
        {
            writer.WriteBoolean("l", true);
        }
    }

    private static string? Branch(Utf8JsonWriter writer, BranchNode branch)
    {
        if (branch.Source is null)
        {
            return "a branch slot without a description";
        }

        writer.WriteString("t", "branch");
        Spec(writer, "s", branch.Source);
        return null;
    }

    private static string? Learned(Utf8JsonWriter writer, LearnedNode learned)
    {
        if (learned.Source is null)
        {
            return "a learned slot without a description";
        }

        writer.WriteString("t", "learn");
        Spec(writer, "s", learned.Source);
        writer.WriteStartArray("ks");
        foreach (var key in learned.Keys)
        {
            writer.WriteStringValue(key);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("bs");
        foreach (var body in learned.Bodies)
        {
            if (WriteNodes(writer, body) is { } failure)
            {
                return failure;
            }
        }

        writer.WriteEndArray();
        return null;
    }

    private static string? Levels(Utf8JsonWriter writer, LevelNode levels)
    {
        writer.WriteString("t", "levels");
        writer.WriteStartArray("bs");
        foreach (var body in levels.Levels)
        {
            if (WriteNodes(writer, body) is { } failure)
            {
                return failure;
            }
        }

        writer.WriteEndArray();
        return null;
    }

    private static string? Depth(Utf8JsonWriter writer, DepthNode depth)
    {
        writer.WriteString("t", "depth");
        writer.WriteNumber("i", depth.Indent);
        writer.WriteNumber("o", depth.Offset);
        return null;
    }

    private static string? Popover(Utf8JsonWriter writer, PopoverNode popover)
    {
        writer.WriteString("t", "pop");
        writer.WriteString("k", popover.Key);
        writer.WriteBoolean("r", popover.Reserve);
        return null;
    }

    private static string? Style(Utf8JsonWriter writer, PageStyleNode style)
    {
        writer.WriteString("t", "style");
        writer.WriteString("k", style.Key);
        writer.WriteString("c", style.Css);
        return null;
    }

    private static string? SameId(Utf8JsonWriter writer, SameIdNode same)
    {
        writer.WriteString("t", "same-id");
        if (same.Key is { } key)
        {
            writer.WriteString("k", key);
        }

        return null;
    }

    private static string? StateValue(Utf8JsonWriter writer, StateValueNode value)
    {
        writer.WriteString("t", "state-value");
        writer.WriteString("n", value.Name);
        writer.WritePropertyName("b");
        return WriteNodes(writer, value.Body);
    }

    private static string? State(Utf8JsonWriter writer, StateNode state)
    {
        writer.WriteString("t", "state");
        writer.WriteNumber("i", state.Index);
        Spec(writer, "fd", state.Field);
        return null;
    }

    private static string? Id(Utf8JsonWriter writer, IdNode id)
    {
        writer.WriteString("t", "id");
        Spec(writer, "fd", id.Field);
        if (id.Key is { } key)
        {
            writer.WriteString("k", key);
        }

        if (id.Button)
        {
            writer.WriteBoolean("b", true);
        }

        return null;
    }

    private static string? Literal(Utf8JsonWriter writer, LiteralNode literal)
    {
        writer.WriteString("t", "lit");
        writer.WriteString("x", literal.Text);
        return null;
    }

    private static string? Region(Utf8JsonWriter writer, SlotRegionNode region)
    {
        writer.WriteString("t", "slot");
        writer.WriteString("p", region.Part switch { SlotRegionPart.Mark => "mark", SlotRegionPart.Open => "open", _ => "close" });
        return null;
    }

    private static string? Types(Utf8JsonWriter writer, TypeSwitchNode types)
    {
        writer.WriteString("t", "type");
        writer.WriteStartArray("c");
        foreach (var item in types.Cases)
        {
            writer.WriteStartObject();
            writer.WriteString("n", item.Type.AssemblyQualifiedName);
            writer.WritePropertyName("b");
            if (WriteNodes(writer, item.Body) is { } failure)
            {
                return failure;
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        return null;
    }

    private static string? Current(Utf8JsonWriter writer, CurrentNode current)
    {
        if (current.Href is null && current.Source is null)
        {
            return "a current link without a description";
        }

        writer.WriteString("t", "cur");
        if (current.Href is { } href)
        {
            writer.WriteString("h", href);
        }

        Spec(writer, "s", current.Source);
        if (current.Exact)
        {
            writer.WriteBoolean("x", true);
        }

        return null;
    }

    private static string? Value(Utf8JsonWriter writer, ValueNode value)
    {
        if (value.Source is null || (value.Format is not null && value.FormatSpec is null))
        {
            return "a value slot without a description";
        }

        writer.WriteString("t", "val");
        Spec(writer, "s", value.Source);
        Spec(writer, "f", value.FormatSpec);
        writer.WriteBoolean("r", value.Raw);
        return null;
    }

    private static string? Written(Utf8JsonWriter writer, WriterNode written)
    {
        if (written.WriterSpec is null || (written.Source is null && !ReferenceEquals(written.Get, WriterNode.NoValue)) || written.Parts.Any(static part => part.Get is not null && part.Read is null))
        {
            return "a writer slot without a description";
        }

        writer.WriteString("t", "write");
        Spec(writer, "s", written.Source);
        Spec(writer, "f", written.WriterSpec);
        if (written.Parts.IsEmpty)
        {
            return null;
        }

        writer.WriteStartArray("ps");
        foreach (var part in written.Parts)
        {
            writer.WriteStartObject();
            writer.WriteString("k", part.Key);
            Spec(writer, "r", part.Read);
            Spec(writer, "fd", part.Field);
            Spec(writer, "c", part.Command);
            if (part.IsField)
            {
                writer.WriteBoolean("fx", true);
            }

            if (part.IsCommand)
            {
                writer.WriteBoolean("cx", true);
            }

            if (part.IsChoice)
            {
                writer.WriteBoolean("chx", true);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        return null;
    }

    private static string? Bodied(Utf8JsonWriter writer, string kind, SlotSpec? source, ImmutableArray<IPlanNode> body)
    {
        if (source is null)
        {
            return $"a {kind} slot without a description";
        }

        writer.WriteString("t", kind);
        Spec(writer, "s", source);
        writer.WritePropertyName("b");
        return WriteNodes(writer, body);
    }

    private static string? Repeat(Utf8JsonWriter writer, RepeatNode repeat)
    {
        var failure = Bodied(writer, "each", repeat.Source, repeat.Body);
        Identity(writer, repeat.ItemKey, repeat.Listed);

        if (failure is not null || !repeat.Positional)
        {
            return failure;
        }

        writer.WriteStartArray("ps");
        foreach (var body in repeat.Positions)
        {
            if (body.IsDefault)
            {
                writer.WriteNullValue();
            }
            else if (WriteNodes(writer, body) is { } broken)
            {
                return broken;
            }
        }

        writer.WriteEndArray();
        return null;
    }

    private static string? Scope(Utf8JsonWriter writer, ScopeNode scope)
    {
        writer.WriteString("t", "scope");
        writer.WriteNumber("m", scope.Model);
        writer.WritePropertyName("b");
        return WriteNodes(writer, scope.Body);
    }

    private static string? View(Utf8JsonWriter writer, ViewNode view)
    {
        writer.WriteString("t", "view");
        writer.WriteNumber("m", view.Model);
        writer.WriteString("k", view.Key);
        if (view.Item is not null)
        {
            writer.WriteString("i", view.Item);
        }

        writer.WritePropertyName("b");
        if (WriteNodes(writer, view.Body) is { } failure)
        {
            return failure;
        }

        writer.WritePropertyName("e");
        return WriteNodes(writer, view.Error);
    }

    private static void Strings(Utf8JsonWriter writer, string name, ImmutableArray<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static string? Select(Utf8JsonWriter writer, SelectNode select)
    {
        if ((select.Get is not null && select.Source is null) || (select.Items is not null && select.ItemsSource is null) || (select.Text is not null && select.TextSource is null) || (select.Index is not null && select.IndexSource is null))
        {
            return "a select slot without a description";
        }

        writer.WriteString("t", "sel");
        Spec(writer, "s", select.Source);
        Spec(writer, "is", select.ItemsSource);
        Spec(writer, "ts", select.TextSource);
        Spec(writer, "x", select.IndexSource);
        writer.WriteNumber("v", select.Selected);
        Strings(writer, "o", select.Options);
        if (select.Placeholder is not null)
        {
            writer.WriteString("p", select.Placeholder);
        }

        if (!select.Labels.IsDefaultOrEmpty)
        {
            Strings(writer, "l", select.Labels);
        }

        if (!select.Kinds.IsDefaultOrEmpty)
        {
            Strings(writer, "kd", select.Kinds);
        }

        return null;
    }

    private static string? Choice(Utf8JsonWriter writer, ChoiceNode choice)
    {
        if (choice.Selection is null || (choice.Items is not null && choice.ItemsSource is null))
        {
            return "a choice slot without a description";
        }

        writer.WriteString("t", "choice");
        Identity(writer, choice.ItemKey, choice.Listed);
        Spec(writer, "s", choice.ItemsSource);
        Spec(writer, "p", choice.Selection);
        Spec(writer, "fd", choice.Field);
        writer.WritePropertyName("m");
        Markup(writer, choice.Markup);
        Markups(writer, "pm", choice.Positions);
        Markups(writer, "im", choice.Each);
        writer.WriteStartArray("bs");
        foreach (var body in choice.Bodies)
        {
            if (WriteNodes(writer, body) is { } failure)
            {
                return failure;
            }
        }

        writer.WriteEndArray();
        return null;
    }

    private static void Markups(Utf8JsonWriter writer, string name, ImmutableArray<ChoiceMarkup> markups)
    {
        if (markups.IsDefaultOrEmpty)
        {
            return;
        }

        writer.WriteStartArray(name);
        foreach (var markup in markups)
        {
            Markup(writer, markup);
        }

        writer.WriteEndArray();
    }

    private static void Markup(Utf8JsonWriter writer, ChoiceMarkup markup)
    {
        writer.WriteStartObject();
        writer.WriteString("o", markup.Open);
        writer.WriteString("so", markup.SelectedOpen);
        writer.WriteString("i", markup.Input);
        writer.WriteString("c", markup.Close);
        if (markup.InputOnSelected)
        {
            writer.WriteBoolean("os", true);
        }

        writer.WriteEndObject();
    }

    private static string? Selector(Utf8JsonWriter writer, SelectorNode selector)
    {
        if (selector.Selector is null)
        {
            return "a template selector without a description";
        }

        writer.WriteString("t", "pick");
        Spec(writer, "s", selector.Selector);
        writer.WriteStartArray("bs");
        foreach (var body in selector.Cases)
        {
            if (WriteNodes(writer, body) is { } failure)
            {
                return failure;
            }
        }

        writer.WriteEndArray();
        return null;
    }

    private static void Spec(Utf8JsonWriter writer, string name, SlotSpec? spec)
    {
        if (spec is null)
        {
            return;
        }

        writer.WriteStartObject(name);
        writer.WriteString("k", spec.Kind);
        writer.WriteString("p", spec.Path);
        writer.WriteString("a", spec.Argument);
        writer.WriteStartArray("v");
        foreach (var value in spec.Values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

public sealed record PlanWrite(string? Json, string? Failure);
