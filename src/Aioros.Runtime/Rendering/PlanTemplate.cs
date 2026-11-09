using System.Buffers;
using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// A plan's body split the way Phoenix LiveView splits a template: the statics between slots (<c>s</c>), with the
/// nested templates of conditions (<c>c</c>) and repeats (<c>r</c>) by slot index. A browser that has it rebuilds a
/// page from its dynamics alone; the fingerprint names it, so it is fetched once and cached. A plan keeps only the
/// fingerprint and the length: its JSON, as large as the body's statics, is written again from the body when asked for.
/// </summary>
public sealed record PlanTemplate(string Fingerprint, int Length)
{
    internal static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static PlanTemplate Of(ImmutableArray<IPlanNode> body)
    {
        using var buffer = new PooledBuffer();
        Write(buffer, body);
        var hash = 14695981039346656037UL;
        foreach (var value in buffer.WrittenSpan)
        {
            hash = (hash ^ value) * 1099511628211UL;
        }

        return new PlanTemplate(hash.ToString("x16", CultureInfo.InvariantCulture), buffer.WrittenSpan.Length);
    }

    /// <summary>Writes the template of a body as JSON.</summary>
    public static void Write(IBufferWriter<byte> writer, ImmutableArray<IPlanNode> body)
    {
        using var json = new Utf8JsonWriter(writer, Options);
        Write(json, body);
    }

    private static void Write(Utf8JsonWriter json, ImmutableArray<IPlanNode> nodes)
    {
        var parts = new Parts();
        parts.Collect(nodes);
        parts.Statics.Add(parts.Current.ToString());
        json.WriteStartObject();
        json.WriteStartArray("s");
        foreach (var text in parts.Statics)
        {
            json.WriteStringValue(text);
        }

        json.WriteEndArray();
        Nested(json, "c", parts.Conditions);
        Nested(json, "r", parts.Repeats);
        json.WriteEndObject();
    }

    private static void Nested(Utf8JsonWriter json, string name, List<(int Slot, ImmutableArray<IPlanNode> Body)> nested)
    {
        if (nested.Count == 0)
        {
            return;
        }

        json.WriteStartObject(name);
        foreach (var (slot, body) in nested)
        {
            json.WritePropertyName(slot.ToString(CultureInfo.InvariantCulture));
            Write(json, body);
        }

        json.WriteEndObject();
    }

    private sealed class Parts
    {
        public List<string> Statics { get; } = [];

        public StringBuilder Current { get; } = new();

        public List<(int, ImmutableArray<IPlanNode>)> Conditions { get; } = [];

        public List<(int, ImmutableArray<IPlanNode>)> Repeats { get; } = [];

        public void Collect(ImmutableArray<IPlanNode> nodes)
        {
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case LiteralNode literal:
                        Current.Append(literal.Text);
                        break;
                    case SlotRegionNode { Part: not SlotRegionPart.Mark } region:
                        Current.Append(region.Text);
                        break;
                    case ScopeNode scope:
                        Collect(scope.Body);
                        break;
                    case ConditionNode condition:
                        Conditions.Add((Slot(), condition.Body));
                        break;
                    case RepeatNode { Positional: false } repeat:
                        Repeats.Add((Slot(), repeat.Body));
                        break;
                    default:
                        Slot();
                        break;
                }
            }
        }

        private int Slot()
        {
            Statics.Add(Current.ToString());
            Current.Clear();
            return Statics.Count - 1;
        }
    }
}

/// <summary>
/// The dynamics of a plan's body for some models, in the order of its template's slots; with a body to write, the body's
/// bytes too, its statics and each slot's markup in order, which is what the browser rebuilds from the template.
/// </summary>
public static class PlanDynamics
{
    public static SlotRegionLog? Write(IBufferWriter<byte> writer, ImmutableArray<IPlanNode> body, ImmutableArray<object?> models, string? path = null, PooledBuffer? html = null, PageStyles? styles = null, string? nonce = null)
    {
        using var scratch = new PooledBuffer(ScratchCapacity);
        var log = html is null && nonce is null ? null : new SlotRegionLog(scratch, nonce);
        var output = new HtmlOutput(scratch) { Regions = log, Styles = styles };
        using var json = new Utf8JsonWriter(writer, PlanTemplate.Options);
        json.WriteStartArray();
        Walk(new Walker(json, scratch, html, log), ref output, body, new RenderScope(models.IsDefaultOrEmpty ? null : models[0], models) { Path = path });
        json.WriteEndArray();
        return log;
    }

    /// <summary>The scratch one slot is written into before it is copied as a JSON string: most slots are a few bytes, and it grows for a large one.</summary>
    private const int ScratchCapacity = 4 * 1024;

    private sealed record Walker(Utf8JsonWriter Json, PooledBuffer Scratch, PooledBuffer? Html, SlotRegionLog? Log)
    {
        /// <summary>Starts a slot: the log measures the scratch from where the body is.</summary>
        public void Begin()
        {
            if (Log is not null)
            {
                Log.Base = Html?.WrittenSpan.Length ?? 0;
            }
        }

        /// <summary>Copies the slot's markup into the body, when there is one, and forgets it.</summary>
        public void End()
        {
            Html?.Write(Scratch.WrittenSpan);
            Scratch.ResetWrittenCount();
        }
    }

    private static void Rows(Walker walker, ref HtmlOutput output, RepeatNode repeat, RenderScope scope)
    {
        var json = walker.Json;
        var outerKey = output.ItemKey;
        var mark = repeat.Listed ? output.EnterList() : default;
        json.WriteStartArray();
        switch (repeat.Get(scope.Current))
        {
            case IList list when list.Count > 0:
                var around = scope.Around();
                for (var index = 0; index < list.Count; index++)
                {
                    repeat.Enter(ref output, outerKey, list[index]);
                    json.WriteStartArray();
                    Walk(walker, ref output, repeat.Body, around.Item(scope, list[index]));
                    json.WriteEndArray();
                }

                break;
            case IEnumerable items and not IList:
                foreach (var item in items)
                {
                    repeat.Enter(ref output, outerKey, item);
                    json.WriteStartArray();
                    Walk(walker, ref output, repeat.Body, scope.With(item));
                    json.WriteEndArray();
                }

                break;
        }

        output.ItemKey = outerKey;
        if (repeat.Listed)
        {
            output.LeaveList(mark);
        }

        json.WriteEndArray();
    }

    private static void Walk(Walker walker, ref HtmlOutput output, ImmutableArray<IPlanNode> nodes, RenderScope scope)
    {
        var outer = OuterScopes.Swap(scope.Outer);
        try
        {
            WalkNodes(walker, ref output, nodes, scope);
        }
        finally
        {
            OuterScopes.Swap(outer);
        }
    }

    private static void WalkNodes(Walker walker, ref HtmlOutput output, ImmutableArray<IPlanNode> nodes, RenderScope scope)
    {
        var json = walker.Json;
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LiteralNode literal:
                    walker.Html?.Write(literal.Utf8);
                    break;
                case SlotRegionNode { Part: not SlotRegionPart.Mark } region:
                    walker.Begin();
                    region.Write(ref output, scope);
                    walker.End();
                    break;
                case ScopeNode inner:
                    Walk(walker, ref output, inner.Body, scope.With(scope.Models[inner.Model]));
                    break;
                case ConditionNode condition when condition.Get(scope.Current) is true:
                    json.WriteStartArray();
                    Walk(walker, ref output, condition.Body, scope);
                    json.WriteEndArray();
                    break;
                case ConditionNode:
                    json.WriteNumberValue(0);
                    break;
                case RepeatNode { Positional: false } repeat:
                    Rows(walker, ref output, repeat, scope);
                    break;
                default:
                    walker.Begin();
                    node.Write(ref output, scope);
                    json.WriteStringValue(walker.Scratch.WrittenSpan);
                    walker.End();
                    break;
            }
        }
    }
}
