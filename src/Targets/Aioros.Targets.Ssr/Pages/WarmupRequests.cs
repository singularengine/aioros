using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Pages;
using Aioros.Routing.Tables;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// The requests warmup makes without a client: a sample URL for each parameterized route, from the app's precompile and
/// sitemap sources, and the post whose button names no field, which warms the post path and changes nothing.
/// </summary>
internal static class WarmupRequests
{
    private static readonly byte[] NoField = Encoding.ASCII.GetBytes(PlanForms.ButtonField + "=" + int.MaxValue.ToString(CultureInfo.InvariantCulture) + "%7C");

    public static async Task<Dictionary<string, PageRequest>> SamplesAsync(List<PageDefinition> pages, IServiceProvider services, CancellationToken cancellationToken)
    {
        var samples = new Dictionary<string, PageRequest>(StringComparer.Ordinal);
        var table = pages.Where(page => !page.Route.StartsWith("/_", StringComparison.Ordinal)).Aggregate(RouteTable.Empty, (routes, page) => routes.Add(page.Route));
        var sources = services.GetServices<IPrecompileSource>().Select(source => (Func<CancellationToken, IAsyncEnumerable<string>>)source.UrlsAsync)
            .Concat(services.GetServices<ISitemapSource>().Select(source => (Func<CancellationToken, IAsyncEnumerable<string>>)source.UrlsAsync));
        foreach (var urls in sources)
        {
            await foreach (var url in urls(cancellationToken))
            {
                var path = Uri.TryCreate(url, UriKind.Absolute, out var absolute) ? absolute.AbsolutePath : url;
                var match = table.Match(path);
                if (match.IsMatch && match.Template.Contains('{', StringComparison.Ordinal))
                {
                    samples.TryAdd(match.Template, new PageRequest(path, match.Values.All, ImmutableDictionary<string, string>.Empty));
                }
            }
        }

        return samples;
    }

    public static async Task<int> PostAsync(PageDefinition page, PageRequest request, IServiceProvider services, PageHandler handler, CancellationToken cancellationToken)
    {
        var http = new DefaultHttpContext { RequestServices = services, RequestAborted = cancellationToken };
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = request.Path;
        foreach (var (key, value) in request.RouteValues)
        {
            http.Request.RouteValues[key] = value;
        }

        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Headers[DocumentParts.NavigationHeader] = "1";
        http.Request.Body = new MemoryStream(NoField, writable: false);
        http.Response.Body = Stream.Null;
        await handler.PostAsync(http, page);
        return http.Response.StatusCode;
    }
}
