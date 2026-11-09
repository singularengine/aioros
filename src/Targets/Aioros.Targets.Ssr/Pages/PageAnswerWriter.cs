using System.Buffers;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Caching;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Search;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>How an answer may be kept: its kind, and where the page cache keeps it when it may.</summary>
internal readonly record struct AnswerFreshness(PageKind Kind, PageStore? Store = null);

/// <summary>
/// Writes a page's answer from its cached entry: the whole document, the page alone inside unchanged layouts for the
/// navigation script, or the plan's dynamics for a client that holds its template; each validated against the request's
/// tag and kept in the page cache when it may be.
/// </summary>
internal sealed class PageAnswerWriter(PageHandler handler, SsrOptions options)
{
    private const string DocumentVary = DocumentParts.NavigationHeader + ", " + PageHandler.LayoutHeader;

    /// <summary>
    /// Writes the page from its entry; when its plan meets models it cannot write (<see cref="PlanMissException"/>), the
    /// engine renders this request instead, as Next.js bails a static render out to a dynamic one, and nothing is kept.
    /// </summary>
    /// <param name="rendered">The body a post already rendered from the entry's plan and these models, written instead of rendering it again.</param>
    public async Task WriteAsync(HttpContext http, PageDefinition page, string theme, CachedPage entry, LoadedPage loaded, AnswerFreshness freshness = default, bool whole = false, RenderedBody? rendered = null)
    {
        try
        {
            await WriteEntryAsync(http, entry, loaded, freshness, whole, rendered);
        }
        catch (PlanMissException) when (!http.Response.HasStarted)
        {
            await WriteEntryAsync(http, await handler.RenderedAsync(page, loaded.Models, theme, http.RequestAborted), loaded, freshness with { Store = null }, whole, null);
        }
    }

    /// <summary>
    /// Writes the page's dynamics; a page with slot regions carries their hashes, from the body a post rendered, or from
    /// the body the dynamics walk writes as it goes, so the body is never rendered twice.
    /// </summary>
    private async Task WriteDynamicsAsync(HttpContext http, CachedPage entry, LoadedPage loaded, AnswerFreshness freshness, RenderedBody? rendered)
    {
        http.Response.ContentType = "application/json";
        Vary(http, PageHandler.PlanHeader);
        using var dynamics = new PooledBuffer();
        var path = http.Request.Path.Value + http.Request.QueryString.Value;
        var styles = new PageStyles();
        if (!entry.Page.Plan.HasSlotRegions)
        {
            entry.Page.Plan.WriteDynamics(dynamics, loaded.Models, path, styles);
        }
        else if (rendered is not null)
        {
            entry.Page.Plan.WriteDynamics(dynamics, loaded.Models, path, styles, rendered.Regions?.Nonce);
            http.Response.Headers[SlotAnswers.HashesHeader] = rendered.Hashes;
        }
        else
        {
            using var body = new PooledBuffer();
            var regions = entry.Page.Plan.WriteDynamics(dynamics, loaded.Models, path, body, styles);
            http.Response.Headers[SlotAnswers.HashesHeader] = SlotAnswers.Hashes(body.WrittenSpan, regions);
        }

        using var json = new PooledBuffer(dynamics.WrittenSpan.Length + 256);
        PageValidation.WriteDynamics(json, PageLoader.Title(loaded), dynamics.WrittenSpan, KeyedStyles.Elements(styles));

        if (Validated(http, freshness, entry, loaded, PageVariant.Dynamics, null, PageHandler.PlanHeader, json.WrittenSpan, default, default))
        {
            return;
        }

        http.Response.ContentLength = json.WrittenSpan.Length;
        await http.Response.BodyWriter.WriteAsync(json.WrittenMemory, http.RequestAborted);
    }

    /// <summary>Names the request headers an answer varies by, the theme's client hint among them for a visitor who follows the system.</summary>
    private static void Vary(HttpContext http, string vary)
    {
        http.Response.Headers.Vary = vary;
        Themes.ThemeSwitch.AskForHint(http);
        LocalizedRequests.Answer(http);
    }

    private bool Validated(HttpContext http, AnswerFreshness freshness, CachedPage entry, LoadedPage loaded, PageVariant variant, string? signature, string vary, ReadOnlySpan<byte> start, ReadOnlySpan<byte> content, ReadOnlySpan<byte> tail) =>
        PageValidation.Validated(http, new Caching.PageAnswer(freshness.Kind, freshness.Store, handler.Pages, entry, loaded, variant, signature, vary), start, content, tail);

    private AntiforgeryTokenSet? Token(HttpContext http, CachedPage entry) =>
        options.Antiforgery && entry.Page.Plan.HasFields && http.RequestServices.GetService(typeof(IAntiforgery)) is IAntiforgery antiforgery
            ? antiforgery.GetAndStoreTokens(http)
            : null;

    private async Task WriteEntryAsync(HttpContext http, CachedPage entry, LoadedPage loaded, AnswerFreshness freshness, bool whole, RenderedBody? rendered)
    {
        if (entry.Page.Reusable && http.Request.Headers[PageHandler.PlanHeader] == entry.Page.Plan.Template.Fingerprint)
        {
            await WriteDynamicsAsync(http, entry, loaded, freshness, rendered);
            return;
        }

        var head = new HeadMetadata(PageLoader.Title(loaded), PageLoader.Metadata(loaded), SearchEndpoints.Origin(http), http.Request.Path.Value ?? "/");
        var token = Token(http, entry);
        http.Response.ContentType = "text/html; charset=utf-8";
        Vary(http, DocumentVary);
        using var own = rendered is null ? RenderedBody.Render(http, entry, loaded) : null;
        var page = own ?? rendered!;
        var body = page.Body;
        var region = PageRegion.Of(body.WrittenSpan);
        var navigation = http.Request.Headers[DocumentParts.NavigationHeader] == "1";
        var partial = !whole && navigation && region is { } layout && http.Request.Headers[PageHandler.LayoutHeader] == layout.Signature;
        if (partial)
        {
            http.Response.Headers[PageHandler.PartialHeader] = "1";
        }

        using var start = new PooledBuffer();
        WriteStart(start, entry, head, token, navigation ? null : LocalizedRequests.Language(http, options), region?.Signature, page);
        var content = partial ? region!.Value.Page : Range.All;
        var variant = partial ? PageVariant.Partial : navigation ? PageVariant.Navigation : PageVariant.Document;
        if (Validated(http, freshness, entry, loaded, variant, region?.Signature, DocumentVary, start.WrittenSpan, body.WrittenSpan[content], navigation ? DocumentParts.NavigationTail : entry.Parts.Tail))
        {
            return;
        }

        http.Response.ContentLength = start.WrittenSpan.Length + body.WrittenSpan[content].Length + (navigation ? DocumentParts.NavigationTail.Length : entry.Parts.Tail.Length);
        await http.Response.StartAsync(http.RequestAborted);
        var writer = http.Response.BodyWriter;
        writer.Write(start.WrittenSpan);
        writer.Write(body.WrittenSpan[content]);
        writer.Write(navigation ? DocumentParts.NavigationTail : entry.Parts.Tail);
        await writer.FlushAsync(http.RequestAborted);
    }

    /// <summary>
    /// Writes the document up to the body, apart from it, so the response knows its length and starts before the body is
    /// copied into it: the body then goes straight to the connection, where Kestrel would otherwise hold it until the
    /// headers are written and copy it again.
    /// </summary>
    /// <param name="language">The document's language, or null for the navigation script's answer, which has no document start.</param>
    private static void WriteStart(PooledBuffer writer, CachedPage entry, HeadMetadata head, AntiforgeryTokenSet? token, string? language, string? layout, RenderedBody body)
    {
        var slots = body.Hashes;
        var output = new HtmlOutput(writer);
        var navigation = language is null;
        if (language is not null)
        {
            output.WriteUtf8(DocumentParts.DocumentStart);
            output.WriteText(language);
            output.WriteUtf8("\">"u8);
        }

        output.WriteUtf8(navigation ? DocumentParts.NavigationHead : entry.Parts.Head);
        output.WriteText(head.Title);
        if (navigation)
        {
            output.WriteUtf8(entry.Parts.NavigationAfterTitle);
        }
        else
        {
            output.WriteUtf8(entry.Parts.AfterTitle);
            head.Write(ref output);
        }

        if (layout is not null)
        {
            output.WriteUtf8("<meta name=\"aioros-layout\" content=\""u8);
            output.WriteText(layout);
            output.WriteUtf8("\">\n"u8);
        }

        if (slots is not null)
        {
            output.WriteUtf8("<meta name=\""u8);
            output.WriteText(SlotAnswers.Meta);
            output.WriteUtf8("\" content=\""u8);
            output.WriteText(slots);
            output.WriteUtf8("\">\n"u8);
        }

        output.WriteUtf8(entry.Parts.Css);
        KeyedStyles.Write(writer, body.Styles);
        output.WriteUtf8(entry.Parts.Start);
        if (token is { FormFieldName: { } field, RequestToken: { } value })
        {
            output.WriteUtf8("<input type=\"hidden\" name=\""u8);
            output.WriteText(field);
            output.WriteUtf8("\" value=\""u8);
            output.WriteText(value);
            output.WriteUtf8("\">"u8);
        }

        output.WriteUtf8(DocumentParts.BodyOpen);
    }
}
