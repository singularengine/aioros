using System.Collections.Immutable;
using System.Text.Json;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Targets.Ssr.Data;

/// <summary>
/// The JSON of a data answer: each layout's and the page's segment (its data, title, metadata and read keys) and each
/// view's data, items or state, written by the loaders' generated writers.
/// </summary>
internal static class PageDataJson
{
    public static void Segments(Utf8JsonWriter writer, PageDefinition page, PageResults results)
    {
        writer.WriteStartArray("segments");
        for (var index = 0; index < results.Segments.Length && index < page.FirstView; index++)
        {
            var loader = index < page.Layouts.Length ? page.Layouts[index].Loader : page.Loader;
            if (loader is null)
            {
                writer.WriteNullValue();
                continue;
            }

            Segment(writer, loader, results.Segments[index], KeysAt(results, index));
        }

        writer.WriteEndArray();
    }

    public static void Views(Utf8JsonWriter writer, PageDefinition page, PageResults results)
    {
        if (page.Views.IsEmpty)
        {
            return;
        }

        writer.WriteStartObject("views");
        for (var view = 0; view < page.Views.Length; view++)
        {
            if (page.Views[view].View.Loader is not null)
            {
                View(writer, page.Views[view], results.Segments[page.FirstView + view], KeysAt(results, page.FirstView + view));
            }
        }

        writer.WriteEndObject();
    }

    private static ImmutableArray<string> KeysAt(PageResults results, int slot) => !results.Keys.IsDefault && slot < results.Keys.Length ? results.Keys[slot] : [];

    public static void View(Utf8JsonWriter writer, ViewInstance instance, LoaderOutcome result, ImmutableArray<string> keys)
    {
        writer.WriteStartObject(instance.Key);
        if (result.Data is ViewState state)
        {
            writer.WriteString("state", state.Name);
        }
        else if (result.Data is ItemResults items)
        {
            Items(writer, instance.View.Loader!, items);
            Keys(writer, keys);
        }
        else
        {
            writer.WritePropertyName("data");
            instance.View.Loader!.Write!(writer, result.Data);
            Keys(writer, keys);
        }

        writer.WriteEndObject();
    }

    /// <summary>A repeated view's items in list order, each with its key and its data, or its state when it is not shown.</summary>
    private static void Items(Utf8JsonWriter writer, LoaderDefinition loader, ItemResults items)
    {
        writer.WriteStartArray("items");
        foreach (var item in items.Items)
        {
            writer.WriteStartObject();
            writer.WriteString("key", item.Key);
            if (item.Outcome.Data is ViewState state)
            {
                writer.WriteString("state", state.Name);
            }
            else
            {
                writer.WritePropertyName("data");
                loader.Write!(writer, item.Outcome.Data);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void Keys(Utf8JsonWriter writer, ImmutableArray<string> keys)
    {
        if (keys.IsDefaultOrEmpty)
        {
            return;
        }

        writer.WriteStartArray("keys");
        foreach (var key in keys)
        {
            writer.WriteStringValue(key);
        }

        writer.WriteEndArray();
    }

    private static void Segment(Utf8JsonWriter writer, LoaderDefinition loader, LoaderOutcome segment, ImmutableArray<string> keys)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("data");
        loader.Write!(writer, segment.Data);
        if (segment.Title is { } title)
        {
            writer.WriteString("title", title);
        }

        if (segment.Metadata is { } metadata)
        {
            writer.WriteStartObject("metadata");
            writer.WriteString("description", metadata.Description);
            writer.WriteString("canonical", metadata.Canonical);
            writer.WriteString("image", metadata.Image);
            writer.WriteString("type", metadata.Type);
            writer.WriteBoolean("indexable", metadata.Indexable);
            writer.WriteEndObject();
        }

        Keys(writer, keys);
        writer.WriteEndObject();
    }
}
