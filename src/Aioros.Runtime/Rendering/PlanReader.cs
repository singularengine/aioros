using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Aioros.Runtime.Rendering;

internal sealed class PlanReader(ISlotResolver resolver)
{
    private readonly Dictionary<string, LiteralNode> _literals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _styles = new(StringComparer.Ordinal);

    public ImmutableArray<IPlanNode> Nodes(JsonElement array) =>
        [.. array.EnumerateArray().Select(element => Node(element))];

    private IPlanNode Node(JsonElement element) => element.GetProperty("t").GetString() switch
    {
        "lit" => Literal(element.GetProperty("x").GetString() ?? string.Empty),
        "slot" => element.GetProperty("p").GetString() switch
        {
            "mark" => SlotRegionNode.Mark,
            "open" => SlotRegionNode.Open,
            _ => SlotRegionNode.Close,
        },
        "id" => new IdNode { Field = Spec(element, "fd"), Key = element.TryGetProperty("k", out var key) ? key.GetString() : null, Button = element.TryGetProperty("b", out var button) && button.GetBoolean() },
        "style" => new PageStyleNode(element.GetProperty("k").GetString()!, element.GetProperty("c").GetString()!),
        "state-value" => new StateValueNode(element.GetProperty("n").GetString()!, Nodes(element.GetProperty("b"))),
        "state" => new StateNode(element.GetProperty("i").GetInt32(), Spec(element, "fd")!),
        "same-id" => new SameIdNode(element.TryGetProperty("k", out var same) ? same.GetString() : null),
        "pop" => new PopoverNode(element.GetProperty("k").GetString()!, element.GetProperty("r").GetBoolean()),
        "shape" or "item-key" => ListMark(element.GetProperty("t").GetString()),
        "tree" => new TreeNode(resolver.Getter(Spec(element, "s")!), Nodes(element.GetProperty("b"))) { ItemKey = Spec(element, "k"), ItemKeyGetter = Spec(element, "k") is { } key ? resolver.Getter(key) : null, Listed = Listed(element), Source = Spec(element, "s") },
        "branch" => new BranchNode(resolver.Getter(Spec(element, "s")!)) { Source = Spec(element, "s") },
        "learn" => new LearnedNode(
            resolver.Getter(Spec(element, "s")!),
            [.. element.GetProperty("ks").EnumerateArray().Select(key => key.GetString() ?? string.Empty)],
            [.. element.GetProperty("bs").EnumerateArray().Select(body => Nodes(body))]) { Source = Spec(element, "s") },
        "levels" => new LevelNode([.. element.GetProperty("bs").EnumerateArray().Select(body => Nodes(body))]),
        "depth" => new DepthNode(element.GetProperty("i").GetDouble(), element.GetProperty("o").GetDouble()),
        "val" => Value(element),
        "write" => Written(element),
        "cur" => (element.TryGetProperty("h", out var href)
            ? new CurrentNode(href.GetString())
            : new CurrentNode(null, resolver.Getter(Spec(element, "s")!)) { Source = Spec(element, "s") }) with { Exact = element.TryGetProperty("x", out _) },
        "if" => new ConditionNode(resolver.Getter(Spec(element, "s")!), Nodes(element.GetProperty("b"))) { Source = Spec(element, "s") },
        "type" => new TypeSwitchNode([.. element.GetProperty("c").EnumerateArray().Select(item =>
            new TypeCase(resolver.TypeOf(item.GetProperty("n").GetString()!), Nodes(item.GetProperty("b"))))]),
        "each" => Repeat(element),
        "scope" => new ScopeNode(element.GetProperty("m").GetInt32(), Nodes(element.GetProperty("b"))),
        "view" => new ViewNode(element.GetProperty("m").GetInt32(), element.GetProperty("k").GetString() ?? string.Empty, Nodes(element.GetProperty("b")), Nodes(element.GetProperty("e")))
        {
            Item = element.TryGetProperty("i", out var item) ? item.GetString() : null,
        },
        "sel" => Select(element),
        "choice" => Choice(element),
        "pick" => new SelectorNode(resolver.Chooser(Spec(element, "s")!), [.. element.GetProperty("bs").EnumerateArray().Select(body => Nodes(body))]) { Selector = Spec(element, "s") },
        var kind => throw new JsonException($"Unknown plan node '{kind}'."),
    };

    private WriterNode Written(JsonElement element)
    {
        var source = Spec(element, "s");
        var spec = Spec(element, "f")!;
        return new WriterNode(source is null ? WriterNode.NoValue : resolver.Getter(source), resolver.Writer(spec))
        {
            Source = source,
            WriterSpec = spec,
            Parts = element.TryGetProperty("ps", out var parts) ? [.. parts.EnumerateArray().Select(part => Part(part))] : [],
        };
    }

    private WriterPart Part(JsonElement element)
    {
        var read = Spec(element, "r");
        return new WriterPart(element.GetProperty("k").GetString() ?? string.Empty)
        {
            Read = read,
            Get = read is null ? null : resolver.Getter(read),
            IsField = element.TryGetProperty("fx", out _),
            Field = Spec(element, "fd"),
            IsCommand = element.TryGetProperty("cx", out _),
            Command = Spec(element, "c"),
            IsChoice = element.TryGetProperty("chx", out _),
        };
    }

    private ValueNode Value(JsonElement element)
    {
        var source = Spec(element, "s")!;
        var format = Spec(element, "f");
        return new ValueNode(resolver.Getter(source), resolver.Formatter(format), element.GetProperty("r").GetBoolean())
        {
            Source = source,
            FormatSpec = format,
            SpanFormat = resolver.SpanFormatter(format),
            Utf8Format = resolver.Utf8Formatter(format),
        };
    }

    private SelectNode Select(JsonElement element)
    {
        var source = Spec(element, "s");
        var options = element.GetProperty("o").EnumerateArray().Select(option => option.GetString() ?? string.Empty).ToImmutableArray();
        var items = Spec(element, "is");
        var text = Spec(element, "ts");
        var index = Spec(element, "x");
        ImmutableArray<string> Strings(string name) => element.TryGetProperty(name, out var values) ? [.. values.EnumerateArray().Select(value => value.GetString() ?? string.Empty)] : [];
        return new SelectNode(source is null ? null : resolver.Getter(source), element.GetProperty("v").GetInt32(), options)
        {
            Source = source,
            Items = items is null ? null : resolver.Getter(items),
            ItemsSource = items,
            Text = text is null ? null : resolver.Getter(text),
            TextSource = text,
            Index = index is null ? null : resolver.Getter(index),
            IndexSource = index,
            Placeholder = element.TryGetProperty("p", out var placeholder) ? placeholder.GetString() : null,
            Labels = Strings("l"),
            Kinds = Strings("kd"),
        };
    }

    private static IPlanNode ListMark(string? kind) => kind == "shape" ? new ShapeNode() : ItemKeySuffixNode.Instance;

    private static bool Listed(JsonElement element) => element.TryGetProperty("l", out var listed) && listed.GetBoolean();

    private RepeatNode Repeat(JsonElement element) =>
        new(resolver.Getter(Spec(element, "s")!), Nodes(element.GetProperty("b")))
        {
            Source = Spec(element, "s"),
            ItemKey = Spec(element, "k"),
            ItemKeyGetter = Spec(element, "k") is { } key ? resolver.Getter(key) : null,
            Listed = Listed(element),
            Positions = element.TryGetProperty("ps", out var positions)
                ? [.. positions.EnumerateArray().Select(body => body.ValueKind == JsonValueKind.Null ? default : Nodes(body))]
                : [],
        };

    private ChoiceNode Choice(JsonElement element)
    {
        var source = Spec(element, "s");
        var selection = Spec(element, "p")!;
        var markup = element.GetProperty("m");
        var bodies = element.GetProperty("bs").EnumerateArray().Select(body => Nodes(body)).ToImmutableArray();
        return new ChoiceNode(
            source is null ? null : resolver.Getter(source),
            resolver.Predicate(selection),
            Markup(markup),
            bodies)
        {
            ItemsSource = source,
            Selection = selection,
            Field = Spec(element, "fd"),
            ItemKey = Spec(element, "k"),
            ItemKeyGetter = Spec(element, "k") is { } key ? resolver.Getter(key) : null,
            Listed = Listed(element),
            Positions = element.TryGetProperty("pm", out var positions) ? [.. positions.EnumerateArray().Select(Markup)] : [],
            Each = element.TryGetProperty("im", out var each) ? [.. each.EnumerateArray().Select(Markup)] : [],
        };
    }

    public string Style(string css)
    {
        ref var style = ref CollectionsMarshal.GetValueRefOrAddDefault(_styles, css, out _);
        return style ??= css;
    }

    private LiteralNode Literal(string text)
    {
        ref var literal = ref CollectionsMarshal.GetValueRefOrAddDefault(_literals, text, out _);
        return literal ??= new LiteralNode(text);
    }

    private static ChoiceMarkup Markup(JsonElement markup) =>
        new(Text(markup, "o"), Text(markup, "so"), markup.GetProperty("i").GetString(), Text(markup, "c")) { InputOnSelected = markup.TryGetProperty("os", out _) };

    private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString() ?? string.Empty;

    private static SlotSpec? Spec(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var spec))
        {
            return null;
        }

        return new SlotSpec(Text(spec, "k"), spec.GetProperty("p").GetString(), spec.GetProperty("a").GetString())
        {
            Values = [.. spec.GetProperty("v").EnumerateArray().Select(value => value.GetString() ?? string.Empty)],
        };
    }
}
