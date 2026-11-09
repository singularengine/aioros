using System.Collections.Immutable;
using System.Text.Json;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Access;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aioros.Targets.Ssr.Data;

/// <summary>
/// <c>GET /_data{route}</c>: the loader results of a page and its layouts in one request, for the installed app, as
/// Remix's single fetch (<c>.data</c>) and SvelteKit's <c>__data.json</c>. The loaders run as for the page, under the
/// same access rules, answered as status codes rather than sign-in redirects.
/// </summary>
public static class PageDataEndpoints
{
    private const string NoStore = "no-store";

    private static readonly JsonWriterOptions Options = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly Action<ILogger, string, Exception?> Failed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, "PageDataFailed"), "The loaders of {Route} failed for a data request");

    /// <summary>Maps the data request of every page, and a <c>404</c> for any other path under <c>/_data</c>.</summary>
    public static IEndpointRouteBuilder MapPageData(this IEndpointRouteBuilder endpoints, IEnumerable<PageDefinition> pages, PageDefinition? notFound = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pages);
        foreach (var page in pages)
        {
            endpoints.MapGet(RemoteLoaderResults.Prefix + page.Route, (HttpContext http) => ServeAsync(http, page, notFound));
        }

        endpoints.MapGet(RemoteLoaderResults.Prefix + "/{**path}", (HttpContext http) => NotFoundAsync(http, notFound));
        return endpoints;
    }

    public static async Task ServeAsync(HttpContext http, PageDefinition page, PageDefinition? notFound = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(page);
        http.Response.Headers.CacheControl = NoStore;
        if (http.Request.Headers[ServerFunctionGroup.ContractHeader] != page.DataContract)
        {
            http.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            http.Response.Headers[ServerFunctionGroup.ContractHeader] = page.DataContract;
            return;
        }

        if (!page.Access.IsEmpty && await AccessRules.RefusedAsync(http, [.. page.Access.Select(static access => (access.Policy, access.Roles))]) is { } refused)
        {
            http.Response.StatusCode = refused;
            return;
        }

        if (http.Request.Headers[RemoteLoaderResults.ViewHeader] is { Count: > 0 } named)
        {
            await ServeViewAsync(http, page, named.ToString());
            return;
        }

        PageResults results;
        try
        {
            results = await PageLoader.LoadResultsAsync(http.RequestServices, page, Request(http), http.RequestAborted);
            Writable(page);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Log(http, page, error);
            await ProblemAsync(http);
            return;
        }

        await WriteAsync(http, page, results, notFound);
    }

    /// <summary>
    /// One view instance's results alone, for the installed app after a command in that view: only its loader runs, behind
    /// its error boundary, and the answer names it in <c>views</c> as the whole page's answer does. A repeated view's item
    /// is named <c>{key}[{item}]</c>, and its loader runs with that item's key alone.
    /// </summary>
    private static async Task ServeViewAsync(HttpContext http, PageDefinition page, string named)
    {
        var (key, item) = ViewName(named);
        var view = Enumerable.Range(0, page.Views.Length).FirstOrDefault(index => page.Views[index].Key == key && page.Views[index].View.Loader is { Write: not null } && (item is null) == (page.Views[index].List is null), -1);
        if (view < 0)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var result = item is null
            ? await PageLoader.LoadViewResultAsync(http.RequestServices, page, Request(http), view, http.RequestAborted)
            : await PageLoader.LoadItemResultAsync(http.RequestServices, page, Request(http), view, item, http.RequestAborted);
        await AnswerAsync(http, StatusCodes.Status200OK, writer =>
        {
            writer.WriteString("type", "data");
            writer.WriteStartArray("segments");
            writer.WriteEndArray();
            writer.WriteStartObject("views");
            PageDataJson.View(writer, page.Views[view], result.Outcome, result.Keys);
            writer.WriteEndObject();
        });
    }

    /// <summary>An instance key, and the item key it ends with in brackets for one item of a repeated view.</summary>
    private static (string Key, string? Item) ViewName(string named)
    {
        var open = named.LastIndexOf('[');
        return open > 0 && named.EndsWith(']') ? (named[..open], Uri.UnescapeDataString(named[(open + 1)..^1])) : (named, null);
    }

    private static void Log(HttpContext http, PageDefinition page, Exception error)
    {
        if (http.RequestServices.GetService<ILoggerFactory>() is { } logs)
        {
            Failed(logs.CreateLogger("Aioros.PageData"), page.Route, error);
        }
    }

    private static async Task NotFoundAsync(HttpContext http, PageDefinition? notFound)
    {
        PageResults? results = null;
        if (notFound is not null)
        {
            try
            {
                results = await PageLoader.LoadResultsAsync(http.RequestServices, notFound, Request(http), http.RequestAborted);
                Writable(notFound);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Log(http, notFound, error);
            }
        }

        await AnswerAsync(http, StatusCodes.Status404NotFound, writer =>
        {
            writer.WriteString("type", "notFound");
            if (notFound is not null && results is { Outcome.Stops: false } loaded)
            {
                writer.WriteString("contract", notFound.DataContract);
                PageDataJson.Segments(writer, notFound, loaded);
            }
        });
    }

    private static PageRequest Request(HttpContext http)
    {
        var path = http.Request.Path.Value ?? RemoteLoaderResults.Prefix;
        var page = path.Length > RemoteLoaderResults.Prefix.Length ? path[RemoteLoaderResults.Prefix.Length..] : "/";
        return PageModels.Request(http) with { Path = page };
    }

    private static void Writable(PageDefinition page)
    {
        if (page.Layouts.Select(static layout => layout.Loader).Append(page.Loader).Concat(page.Views.Select(static view => view.View.Loader)).FirstOrDefault(static loader => loader is { Write: null }) is not null)
        {
            throw new InvalidOperationException($"A loader of {page.Route} has no generated writer for its data; build the app with the Aioros generator.");
        }
    }

    private static Task WriteAsync(HttpContext http, PageDefinition page, PageResults results, PageDefinition? notFound)
    {
        if (results.Outcome.RedirectTo is { } location)
        {
            return AnswerAsync(http, StatusCodes.Status200OK, writer =>
            {
                writer.WriteString("type", "redirect");
                writer.WriteString("location", location);
            });
        }

        if (results.Outcome.NotFound)
        {
            return NotFoundAsync(http, notFound);
        }

        return AnswerAsync(http, StatusCodes.Status200OK, writer =>
        {
            writer.WriteString("type", "data");
            PageDataJson.Segments(writer, page, results);
            PageDataJson.Views(writer, page, results);
        });
    }

    private static async Task AnswerAsync(HttpContext http, int status, Action<Utf8JsonWriter> body, string contentType = "application/json")
    {
        http.Response.StatusCode = status;
        http.Response.ContentType = contentType;
        http.Response.Headers.CacheControl = NoStore;
        using (var writer = new Utf8JsonWriter(http.Response.BodyWriter, Options))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        await http.Response.BodyWriter.FlushAsync(http.RequestAborted);
    }

    private static Task ProblemAsync(HttpContext http) =>
        AnswerAsync(http, StatusCodes.Status500InternalServerError, static writer =>
        {
            writer.WriteString("title", "The page's data could not be loaded.");
            writer.WriteNumber("status", StatusCodes.Status500InternalServerError);
        }, "application/problem+json");
}
