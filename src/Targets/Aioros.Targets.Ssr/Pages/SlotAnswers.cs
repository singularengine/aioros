using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.IO.Hashing;
using System.Text;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// Answers a post that changed only what one slot renderer shows with that renderer's region alone (a Turbo Frame,
/// LiveView's diff of one component). Every answer of a page with slot regions carries, per region, a hash of the body
/// outside it (<c>aioros-slots</c> in the head, <see cref="HashesHeader"/> on dynamics and region answers), which the
/// framework's script posts back; a post whose field or button sits in a region is answered with that region's content
/// when the body rendered after the post hashes the same outside it as the page the browser shows, and with the whole
/// page otherwise, so nothing outside the region is ever left stale.
/// </summary>
public static class SlotAnswers
{
    /// <summary>The response header naming the region, by its place among the page's slot regions, that the answer's body replaces.</summary>
    public const string Header = "X-Aioros-Slot";

    /// <summary>The header with each region's hash of the body outside it: on answers, and on the script's posts with the hashes it holds.</summary>
    public const string HashesHeader = "X-Aioros-Slots";

    public const string Meta = "aioros-slots";

    /// <summary>
    /// The render's nonce and each slot region's hash of the body outside it, as <c>{nonce}:{hash}.{hash}</c>, or nothing
    /// for a body without slot regions; the nonce is left out of what is hashed.
    /// </summary>
    internal static string? Hashes(ReadOnlySpan<byte> body, SlotRegionLog? regions) =>
        regions is { Regions.Count: > 0 } log ? log.Nonce + ":" + Hashes(body, log.Regions, log.Nonces) : null;

    /// <summary>Renders the body after a post, noting its slot regions; nothing when the plan cannot write these models.</summary>
    internal static RenderedBody? Render(HttpContext http, CachedPage entry, LoadedPage loaded)
    {
        try
        {
            return RenderedBody.Render(http, entry, loaded);
        }
        catch (PlanMissException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the region the post's field or button sits in, when the body rendered after the post is the same outside it as
    /// the page the script holds (the posted hashes), preceded by the keyed rule sets the page does not hold in that version
    /// (<see cref="KeyedStyles.Header"/>); <see langword="false"/> when it is not, so the whole page is answered.
    /// </summary>
    internal static async ValueTask<bool> WriteAsync(HttpContext http, RenderedBody rendered, ImmutableDictionary<string, string> form, string known)
    {
        var body = rendered.Body.WrittenMemory;
        var regions = rendered.Regions?.Regions ?? [];
        if (Trigger(body.Span, regions, form) is not { } region || rendered.Hashes is not { } hashes)
        {
            return false;
        }

        var holds = known[(known.IndexOf(':', StringComparison.Ordinal) + 1)..].Split('.');
        var fresh = hashes[(hashes.IndexOf(':', StringComparison.Ordinal) + 1)..].Split('.');
        if (holds.Length != fresh.Length || holds[region] != fresh[region])
        {
            return false;
        }

        http.Response.ContentType = "text/html; charset=utf-8";
        http.Response.Headers[Header] = region.ToString(CultureInfo.InvariantCulture);
        http.Response.Headers[HashesHeader] = hashes;
        var held = KeyedStyles.Held(http);
        KeyedStyles.Write(http.Response.BodyWriter, rendered.Styles, held.Contains);
        await http.Response.BodyWriter.WriteAsync(body[regions[region]], http.RequestAborted);
        return true;
    }

    /// <summary>The region holding the field the post changed (<c>_chg</c>) or the button it pressed, as the body writes its name.</summary>
    private static int? Trigger(ReadOnlySpan<byte> body, List<Range> regions, ImmutableDictionary<string, string> form)
    {
        var name = form.TryGetValue(PlanForms.ButtonField, out var button) && button.Contains('|', StringComparison.Ordinal)
            ? $" name=\"{PlanForms.ButtonField}\" value=\"{button[..(button.IndexOf('|', StringComparison.Ordinal) + 1)]}"
            : form.TryGetValue(PlanForms.ChangeField, out var changed) && changed.Length > 0 ? $" name=\"f{changed}\"" : null;
        if (name is null)
        {
            return null;
        }

        var needle = Encoding.UTF8.GetBytes(name);
        for (var index = 0; index < regions.Count; index++)
        {
            if (body[regions[index]].IndexOf(needle) >= 0)
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>
    /// Hashes the body's pieces between and inside its regions once, the nonce left out, then, per region, the sequence of
    /// those hashes with the region's own left out: the body outside each region in one pass over the bytes.
    /// </summary>
    private static string Hashes(ReadOnlySpan<byte> body, List<Range> regions, List<Range> nonces)
    {
        var pieces = new ulong[(2 * regions.Count) + 1];
        var start = 0;
        for (var index = 0; index < regions.Count; index++)
        {
            var (offset, length) = regions[index].GetOffsetAndLength(body.Length);
            pieces[2 * index] = Hash(body, start, offset, nonces);
            pieces[(2 * index) + 1] = XxHash3.HashToUInt64(body.Slice(offset, length));
            start = offset + length;
        }

        pieces[^1] = Hash(body, start, body.Length, nonces);
        var text = new StringBuilder(regions.Count * 17);
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        for (var index = 0; index < regions.Count; index++)
        {
            var hash = new XxHash3();
            for (var piece = 0; piece < pieces.Length; piece++)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(bytes, piece == (2 * index) + 1 ? 0 : pieces[piece]);
                hash.Append(bytes);
            }

            text.Append(index == 0 ? string.Empty : ".").Append(hash.GetCurrentHashAsUInt64().ToString("x16", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    /// <summary>The hash of the body's bytes from <paramref name="start"/> to <paramref name="end"/>, without the nonces among them.</summary>
    private static ulong Hash(ReadOnlySpan<byte> body, int start, int end, List<Range> nonces)
    {
        var hash = new XxHash3();
        foreach (var nonce in nonces)
        {
            var (offset, length) = nonce.GetOffsetAndLength(body.Length);
            if (offset < start || offset + length > end)
            {
                continue;
            }

            hash.Append(body[start..offset]);
            start = offset + length;
        }

        hash.Append(body[start..end]);
        return hash.GetCurrentHashAsUInt64();
    }
}

/// <summary>A page's body rendered from its plan, with its slot regions when it has some, written once for every use an answer makes of it.</summary>
internal sealed class RenderedBody : IDisposable
{
    private RenderedBody(PooledBuffer body, SlotRegionLog? regions, PageStyles styles)
    {
        Body = body;
        Regions = regions;
        Styles = styles;
        Hashes = SlotAnswers.Hashes(body.WrittenSpan, regions);
    }

    public PooledBuffer Body { get; }

    public SlotRegionLog? Regions { get; }

    /// <summary>The rule sets the body's slot renderers contributed to the page.</summary>
    public PageStyles Styles { get; }

    public string? Hashes { get; }

    /// <summary>Renders the entry's body for these models, noting its slot regions when its plan writes some.</summary>
    public static RenderedBody Render(HttpContext http, CachedPage entry, LoadedPage loaded)
    {
        var body = new PooledBuffer();
        try
        {
            var regions = entry.Page.Plan.HasSlotRegions ? new SlotRegionLog(body) : null;
            var styles = new PageStyles();
            entry.Page.Plan.RenderBody(body, loaded.Models, http.Request.Path.Value + http.Request.QueryString.Value, regions, styles);
            return new RenderedBody(body, regions, styles);
        }
        catch
        {
            body.Dispose();
            throw;
        }
    }

    public void Dispose() => Body.Dispose();
}
