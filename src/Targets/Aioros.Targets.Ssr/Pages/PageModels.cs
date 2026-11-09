using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Pages;

public static class PageModels
{
    private static readonly ImmutableDictionary<string, string> NoValues = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase);

    public static ValueTask<LoadedPage> LoadAsync(HttpContext http, PageDefinition page) =>
        LoadAsync(http.RequestServices, page, Request(http), http.RequestAborted);

    public static ValueTask<LoadedPage> LoadAsync(IServiceProvider services, PageDefinition page, PageRequest request, CancellationToken cancellationToken) =>
        PageLoader.LoadAsync(services, page, request, cancellationToken);

    /// <summary>
    /// Loads the page's models on the thread of the engine that renders it, when it creates models on a thread of its own
    /// (<see cref="IModelThread"/>), and otherwise on the caller's; <paramref name="renderer"/> is <see langword="null"/>
    /// when a plan renders the page.
    /// </summary>
    public static ValueTask<LoadedPage> LoadAsync(IServiceProvider services, PageDefinition page, PageRequest request, object? renderer, CancellationToken cancellationToken) =>
        renderer is IModelThread thread
            ? thread.LoadAsync(page, () => LoadAsync(services, page, request, cancellationToken), cancellationToken)
            : LoadAsync(services, page, request, cancellationToken);

    public static IPageModel? PageModel(ImmutableArray<object?> models) => PageLoader.PageModel(models);

    public static string? RedirectTo(HttpContext http, ImmutableArray<object?> models) => PageLoader.RedirectTo(http.RequestServices, models);

    public static string? RedirectTo(IServiceProvider services, ImmutableArray<object?> models) => PageLoader.RedirectTo(services, models);

    public static PageRequest Request(HttpContext http) => new(
        http.Request.Path.Value ?? "/",
        http.Request.RouteValues.Count == 0
            ? NoValues
            : http.Request.RouteValues.ToImmutableDictionary(static pair => pair.Key, static pair => Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, StringComparer.OrdinalIgnoreCase),
        http.Request.QueryString.HasValue
            ? http.Request.Query.ToImmutableDictionary(static pair => pair.Key, static pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase)
            : NoValues)
    {
        User = http.User,
        Cookies = http.Request.Cookies.Count == 0 ? NoValues : http.Request.Cookies.ToImmutableDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
    };
}
