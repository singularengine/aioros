using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Aioros.Runtime.Rendering;

/// <summary>A page's plan for a theme and, in an app that localizes, a culture (<see cref="PageCultures.Key"/>).</summary>
public sealed record PlannedPage(string Route, string Theme, CompiledPage Page)
{
    /// <summary>The key of the culture the page was planned in, empty for an app that does not localize.</summary>
    public string Culture { get; init; } = string.Empty;

    /// <summary>The fields whose posts the engine answers, as the browser names them, since the plan answered them otherwise when the page was verified.</summary>
    public ImmutableArray<string> EngineFields { get; init; } = [];

    /// <summary>The fields of local inputs the guarantee does not cover whose domains the page's verification walked, the plan rendering each value as the engine does.</summary>
    public ImmutableArray<string> ProvenFields { get; init; } = [];
}

public sealed record PlansFileSkip(string Route, string Theme, string Reason);

public sealed record PlansFileWrite(string Json, ImmutableArray<PlansFileSkip> Skipped);

public static class PlansFile
{
    public const string Name = "aioros.plans.json";

    /// <summary>The bytes read from a plans file at a time; the buffer grows to the largest page when one is larger.</summary>
    private const int ChunkSize = 1024 * 1024;

    public static PlansFileWrite Write(IEnumerable<PlannedPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var skipped = ImmutableArray.CreateBuilder<PlansFileSkip>();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var page in pages)
            {
                if (WritePage(writer, page) is { } reason)
                {
                    skipped.Add(new PlansFileSkip(page.Route, page.Theme, reason));
                }
            }

            writer.WriteEndArray();
        }

        return new PlansFileWrite(Encoding.UTF8.GetString(stream.ToArray()), skipped.ToImmutable());
    }

    /// <summary>Reads the pages of a plans file page by page from its UTF-8 bytes, sharing the literals and stylesheets pages have alike.</summary>
    public static ImmutableArray<PlannedPage> Read(ReadOnlyMemory<byte> utf8, ISlotResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var reader = new PlanReader(resolver);
        var pages = ImmutableArray.CreateBuilder<PlannedPage>();
        var (state, closed) = (default(JsonReaderState), false);
        Pages(utf8.Span, final: true, ref state, ref closed, reader, pages);
        return Closed(pages, closed);
    }

    public static ImmutableArray<PlannedPage> Read(string json, ISlotResolver resolver) => Read(Encoding.UTF8.GetBytes(json), resolver);

    /// <summary>Reads the pages of a plans file from a stream, holding no more of it at a time than its largest page.</summary>
    public static async Task<ImmutableArray<PlannedPage>> ReadAsync(Stream utf8, ISlotResolver resolver, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utf8);
        ArgumentNullException.ThrowIfNull(resolver);
        var reader = new PlanReader(resolver);
        var pages = ImmutableArray.CreateBuilder<PlannedPage>();
        var (state, closed) = (default(JsonReaderState), false);
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        try
        {
            var (filled, final) = (0, false);
            while (!final && !closed)
            {
                if (filled == buffer.Length)
                {
                    var larger = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    buffer.AsSpan(0, filled).CopyTo(larger);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = larger;
                }

                var read = await utf8.ReadAsync(buffer.AsMemory(filled), cancellationToken);
                (filled, final) = (filled + read, read == 0);
                var consumed = Pages(buffer.AsSpan(0, filled), final, ref state, ref closed, reader, pages);
                buffer.AsSpan(consumed, filled - consumed).CopyTo(buffer);
                filled -= consumed;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return Closed(pages, closed);
    }

    private static ImmutableArray<PlannedPage> Closed(ImmutableArray<PlannedPage>.Builder pages, bool closed) =>
        closed ? pages.ToImmutable() : throw new JsonException("A plans file is an array of pages, and this one ends before its last page does.");

    /// <summary>Reads the whole pages the bytes hold, and returns how many bytes they took, the rest waiting for more.</summary>
    private static int Pages(ReadOnlySpan<byte> utf8, bool final, ref JsonReaderState state, ref bool closed, PlanReader reader, ImmutableArray<PlannedPage>.Builder pages)
    {
        var json = new Utf8JsonReader(utf8, final, state);
        var consumed = 0;
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.StartObject && json.CurrentDepth == 1)
            {
                var page = json;
                if (!json.TrySkip())
                {
                    break;
                }

                using var document = JsonDocument.ParseValue(ref page);
                pages.Add(ReadPage(document.RootElement, reader));
            }
            else if (json.CurrentDepth != 0 || json.TokenType is not (JsonTokenType.StartArray or JsonTokenType.EndArray))
            {
                throw new JsonException("A plans file is an array of pages.");
            }
            else
            {
                closed = json.TokenType == JsonTokenType.EndArray;
            }

            (consumed, state) = ((int)json.BytesConsumed, json.CurrentState);
        }

        return consumed;
    }

    private static string? WritePage(Utf8JsonWriter writer, PlannedPage page)
    {
        var head = PlanSerializer.TryWrite(page.Page.Plan.Head);
        var body = PlanSerializer.TryWrite(page.Page.Plan.Body);
        var tail = PlanSerializer.TryWrite(page.Page.Plan.Tail);
        if ((head.Failure ?? body.Failure ?? tail.Failure) is { } failure)
        {
            return failure;
        }

        writer.WriteStartObject();
        writer.WriteString("route", page.Route);
        writer.WriteString("theme", page.Theme);
        if (page.Culture.Length > 0)
        {
            writer.WriteString("culture", page.Culture);
        }

        writer.WriteString("css", page.Page.Css);
        writer.WriteString("background", page.Page.Background);
        writer.WriteString("foreground", page.Page.Foreground);
        if (page.Page.Uploads)
        {
            writer.WriteBoolean("uploads", true);
        }

        WriteFields(writer, "engine", page.EngineFields);
        WriteFields(writer, "proven", page.ProvenFields);

        writer.WritePropertyName("head");
        writer.WriteRawValue(head.Json!);
        writer.WritePropertyName("body");
        writer.WriteRawValue(body.Json!);
        writer.WritePropertyName("tail");
        writer.WriteRawValue(tail.Json!);
        writer.WriteEndObject();
        return null;
    }

    private static PlannedPage ReadPage(JsonElement element, PlanReader reader)
    {
        var plan = new RenderPlan(
            reader.Nodes(element.GetProperty("head")),
            reader.Nodes(element.GetProperty("body")),
            reader.Nodes(element.GetProperty("tail")))
        {
            ReadsOuter = ReadsOuter(element),
        };
        var page = new CompiledPage(plan, reader.Style(Text(element, "css")), Text(element, "background"), Text(element, "foreground"), true)
        {
            Uploads = element.TryGetProperty("uploads", out var uploads) && uploads.ValueKind == JsonValueKind.True,
        };
        return new PlannedPage(Text(element, "route"), Text(element, "theme"), page)
        {
            Culture = element.TryGetProperty("culture", out var culture) ? culture.GetString() ?? string.Empty : string.Empty,
            EngineFields = Fields(element, "engine"),
            ProvenFields = Fields(element, "proven"),
        };
    }

    private static void WriteFields(Utf8JsonWriter writer, string name, ImmutableArray<string> fields)
    {
        if (fields.IsDefaultOrEmpty)
        {
            return;
        }

        writer.WriteStartArray(name);
        foreach (var field in fields)
        {
            writer.WriteStringValue(field);
        }

        writer.WriteEndArray();
    }

    private static ImmutableArray<string> Fields(JsonElement element, string name) =>
        element.TryGetProperty(name, out var fields) ? [.. fields.EnumerateArray().Select(static field => field.GetString() ?? string.Empty)] : [];

    /// <summary>Whether any part of the page's plan names a <c>^</c>, the mark of a path that reads an outer scope; a <c>^</c> anywhere else only keeps the scopes.</summary>
    private static bool ReadsOuter(JsonElement element) => Names(element, "head") || Names(element, "body") || Names(element, "tail");

    private static bool Names(JsonElement element, string part) => JsonMarshal.GetRawUtf8Value(element.GetProperty(part)) is var json
        && (json.Contains((byte)'^') || json.IndexOf("\\u005E"u8) >= 0 || json.IndexOf("\\u005e"u8) >= 0);

    private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString() ?? string.Empty;
}
