using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Caching;

/// <summary>A rendered answer about to be written: how it may be kept, where, and what it is.</summary>
public readonly record struct PageAnswer(PageKind Kind, PageStore? Store, PageCache? Pages, CachedPage Plan, LoadedPage Loaded, PageVariant Variant, string? Signature, string Vary);

/// <summary>Describes, keeps and revalidates a rendered page answer, and writes the plan's dynamics.</summary>
public static class PageValidation
{
    private static readonly JsonWriterOptions DynamicsJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Describes an answer by its kind: a post's or an error page's is not stored; any other gets the tag of its bytes, is
    /// kept when its page may be, and is answered <c>304</c> (true) when the request's <c>If-None-Match</c> names the tag.
    /// </summary>
    public static bool Validated(HttpContext http, PageAnswer answer, ReadOnlySpan<byte> start, ReadOnlySpan<byte> content, ReadOnlySpan<byte> tail)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (answer.Kind == PageKind.Unstored)
        {
            PageFreshness.Describe(http.Response, answer.Kind, null);
            return false;
        }

        var hash = PageFreshness.Hash(start, content, tail);
        if (answer.Store is { } store && answer.Plan.Page.Reusable && answer.Pages is { } pages)
        {
            var bytes = new byte[start.Length + content.Length + tail.Length];
            start.CopyTo(bytes);
            content.CopyTo(bytes.AsSpan(start.Length));
            tail.CopyTo(bytes.AsSpan(start.Length + content.Length));
            pages.Store(store, ReadKeys(answer.Loaded), answer.Plan, answer.Variant, answer.Signature, new PageCache.Answer(bytes, hash, http.Response.ContentType ?? string.Empty, answer.Vary));
        }

        var tag = PageFreshness.Tag(http.Request, hash);
        PageFreshness.Describe(http.Response, answer.Kind, tag);
        if (!PageFreshness.NotModified(http.Request, tag))
        {
            return false;
        }

        PageFreshness.AnswerNotModified(http.Response);
        return true;
    }

    /// <summary>Writes the plan dynamics' answer, <c>{"t":title,"d":dynamics}</c>.</summary>
    public static void WriteDynamics(PooledBuffer json, string title, ReadOnlySpan<byte> dynamics, string? styles = null)
    {
        using var writer = new Utf8JsonWriter(json, DynamicsJson);
        writer.WriteStartObject();
        writer.WriteString("t", title);
        writer.WritePropertyName("d");
        writer.WriteRawValue(dynamics, skipInputValidation: true);
        if (styles is not null)
        {
            writer.WriteString("s", styles);
        }

        writer.WriteEndObject();
    }

    private static ImmutableArray<string> ReadKeys(LoadedPage loaded) =>
        loaded.Keys.IsDefaultOrEmpty ? [] : [.. loaded.Keys.Where(static keys => !keys.IsDefaultOrEmpty).SelectMany(static keys => keys).Distinct(StringComparer.Ordinal)];
}
