using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Caching;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// The built-in stand-in of a native-only page on the web: its title and description, and an invitation to open it in
/// the installed app or to download it. "Open in the app" is the same link as <c>a:Web.OpenInApp</c>: marked
/// <c>data-open-app</c>, it tries the app's scheme at this path and falls back to the download page.
/// </summary>
public static class NativeOnlyStandIn
{
    public const string DownloadMarker = "data-aioros-download";

    /// <summary>The page with only its static titles and metadata, so its head is resolved without running its loaders or creating its models.</summary>
    public static PageDefinition Heads(PageDefinition page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page with
        {
            CreateModel = static _ => null,
            Loader = null,
            Views = [],
            Layouts = [.. page.Layouts.Select(static layout => layout with { CreateModel = static _ => null, Loader = null })],
        };
    }

    public static async Task WriteAsync(HttpContext http, string title, PageMetadata metadata, SsrOptions options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(options);
        var path = (http.Request.Path.Value ?? "/") + http.Request.QueryString.Value;
        using var buffer = new PooledBuffer();
        Write(buffer, title, metadata, path, Search.SearchEndpoints.Origin(http), options, Hosting.LocalizedRequests.Language(http, options));
        http.Response.StatusCode = StatusCodes.Status200OK;
        http.Response.ContentType = "text/html; charset=utf-8";
        http.Response.Headers.CacheControl = PageFreshness.UnstoredControl;
        http.Response.ContentLength = buffer.WrittenSpan.Length;
        await http.Response.BodyWriter.WriteAsync(buffer.WrittenMemory, http.RequestAborted);
    }

    private static void Write(PooledBuffer buffer, string title, PageMetadata metadata, string path, string origin, SsrOptions options, string language)
    {
        var output = new HtmlOutput(buffer);
        output.WriteUtf8("<!DOCTYPE html>\n<html lang=\""u8);
        output.WriteText(language);
        output.WriteUtf8("\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<title>"u8);
        output.WriteText(title);
        output.WriteUtf8("</title>\n"u8);
        output.WriteRaw(Documents.DocumentParts.AppMeta(options.AppScheme));
        output.WriteRaw(Search.AppIcon.Link(options.Icon));
        new Search.HeadMetadata(title, metadata with { Indexable = false }, origin, path).Write(ref output);
        output.WriteUtf8("<style>:root{color-scheme:light dark;font-family:system-ui,sans-serif}main{max-width:36rem;margin:15vh auto;padding:0 1.5rem}main p{line-height:1.5}nav{display:flex;gap:.75rem;margin-top:1.5rem}nav a{padding:.6rem 1rem;border-radius:.5rem;border:1px solid currentColor;text-decoration:none;color:inherit}nav a:first-child{background:#2563eb;border-color:#2563eb;color:#fff}</style>\n</head>\n<body>\n<main>\n<h1>"u8);
        output.WriteText(title);
        output.WriteUtf8("</h1>\n"u8);
        if (metadata.Description is { } description)
        {
            output.WriteUtf8("<p>"u8);
            output.WriteText(description);
            output.WriteUtf8("</p>\n"u8);
        }

        output.WriteUtf8("<p>Available in the desktop app.</p>\n<nav>\n<a href=\""u8);
        output.WriteText(options.Download);
        output.WriteUtf8("\" data-open-app>Open in the app</a>\n<a href=\""u8);
        output.WriteText(options.Download);
        output.WriteUtf8("\" "u8);
        output.WriteRaw(DownloadMarker);
        output.WriteUtf8(">Download</a>\n</nav>\n</main>\n"u8);
        if (options.AppScheme is { Length: > 0 })
        {
            output.WriteUtf8("<script src=\""u8);
            output.WriteRaw(Documents.OpenInAppScript.Path);
            output.WriteUtf8("\" defer></script>\n"u8);
        }

        output.WriteUtf8("</body>\n</html>\n"u8);
    }
}
