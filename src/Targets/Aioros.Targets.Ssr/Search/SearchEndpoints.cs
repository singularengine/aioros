using System.Text;
using System.Xml;
using Aioros.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Search;

public static class SearchEndpoints
{
    public const string SitemapPath = "/sitemap.xml";
    public const string RobotsPath = "/robots.txt";

    public static string Origin(HttpContext http) => http.Request.Scheme + "://" + http.Request.Host.Value;

    public static async Task SitemapAsync(HttpContext http, IEnumerable<PageDefinition> pages)
    {
        var origin = Origin(http);
        var locations = pages
            .Where(page => !page.NativeOnly && !page.Route.Contains('{', StringComparison.Ordinal) && !page.Route.StartsWith("/_", StringComparison.Ordinal))
            .Select(page => origin + page.Route)
            .ToList();
        foreach (var source in http.RequestServices.GetServices<ISitemapSource>())
        {
            await foreach (var url in source.UrlsAsync(http.RequestAborted))
            {
                locations.Add(Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme.StartsWith("http", StringComparison.Ordinal) ? url : origin + url);
            }
        }

        var xml = new StringBuilder();
        await using (var writer = XmlWriter.Create(xml, new XmlWriterSettings { Async = true, OmitXmlDeclaration = false }))
        {
            await writer.WriteStartDocumentAsync();
            await writer.WriteStartElementAsync(null, "urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
            foreach (var location in locations)
            {
                await writer.WriteStartElementAsync(null, "url", null);
                await writer.WriteElementStringAsync(null, "loc", null, location);
                await writer.WriteEndElementAsync();
            }

            await writer.WriteEndElementAsync();
            await writer.WriteEndDocumentAsync();
        }

        http.Response.ContentType = "application/xml; charset=utf-8";
        await http.Response.WriteAsync(xml.ToString().Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"", StringComparison.Ordinal), http.RequestAborted);
    }

    public const string FaviconPath = "/favicon.ico";

    public static IResult Favicon() => Results.NoContent();

    public static Task RobotsAsync(HttpContext http)
    {
        http.Response.ContentType = "text/plain; charset=utf-8";
        return http.Response.WriteAsync($"User-agent: *\nAllow: /\nDisallow: /_aioros/\nSitemap: {Origin(http)}{SitemapPath}\n", http.RequestAborted);
    }
}
