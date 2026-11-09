using System.Collections.Immutable;
using Aioros.Targets.Ssr.Themes;
using Aioros.Events;
using Aioros.Pages;
using Aioros.Targets.Ssr.Data;
using Aioros.Targets.Ssr.Development;
using Aioros.Targets.Ssr.Live;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Hosting;

public static class PageEndpoints
{
    public static IEndpointRouteBuilder MapAiorosPages(this IEndpointRouteBuilder endpoints, IEnumerable<PageDefinition> pages, PageConventions? conventions = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var applied = pages.Select(page => conventions?.Apply(page) ?? page).ToList();
        foreach (var page in applied)
        {
            if (page.NativeOnly)
            {
                Protect(endpoints.MapGet(page.Route, (HttpContext http) => Handler(http).NativeOnlyAsync(http, page)), page);
                continue;
            }

            Protect(StaticFilesFirst.MapGet(endpoints, page.Route, http => Handler(http).GetAsync(http, page)), page);
            Protect(endpoints.MapPost(page.Route, (HttpContext http) => Handler(http).PostAsync(http, page)).DisableAntiforgery(), page);
            if (page.Live)
            {
                Protect(endpoints.MapGet(page.Route.TrimEnd('/') + "/live", (HttpContext http) => LiveUpdates.StreamAsync(http, page, Handler(http))), page);
            }
        }

        endpoints.MapPageData(applied, endpoints.ServiceProvider.GetService<SsrOptions>()?.NotFound);
        endpoints.MapLiveKeys();
        var routed = pages as IReadOnlyCollection<PageDefinition> ?? [.. pages];
        endpoints.MapGet(Search.SearchEndpoints.SitemapPath, (HttpContext http) => Search.SearchEndpoints.SitemapAsync(http, routed));
        endpoints.MapGet(Search.SearchEndpoints.RobotsPath, Search.SearchEndpoints.RobotsAsync);
        endpoints.MapGet(Search.SearchEndpoints.FaviconPath, Search.SearchEndpoints.Favicon);
        endpoints.MapGet(Documents.Stylesheets.Path + "{name}", (HttpContext http, string name) =>
            Documents.Stylesheets.ServeAsync(http, name, http.RequestServices.GetRequiredService<PlanCache>().Styles));
        MapScripts(endpoints);
        MapTemplates(endpoints, applied);
        endpoints.MapFallback((HttpContext http) => Handler(http).NotFoundAsync(http));
        endpoints.MapGet("/theme/{name}", (HttpContext http, string name) => ThemeSwitch.Switch(http, name));
        if (endpoints.ServiceProvider.GetRequiredService<SsrOptions>().Development)
        {
            endpoints.MapGet(DevelopmentChannel.Path, (HttpContext http) => DevelopmentChannel.StreamAsync(http, http.RequestServices.GetRequiredService<IEventBus>()));
            endpoints.MapPost(DevelopmentBuild.Path, (HttpContext http) => DevelopmentBuild.ReceiveAsync(
                http, http.RequestServices.GetRequiredService<IEventBus>(), http.RequestServices.GetRequiredService<SsrOptions>(), http.RequestServices.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>()));
            var all = pages.ToList();
            endpoints.MapGet(CaptureEndpoint.Path, (HttpContext http) => CaptureEndpoint.HandleAsync(http, all, http.RequestServices.GetRequiredService<Runtime.Rendering.IPageCapture>()));
        }

        return endpoints;
    }

    private static void Protect(IEndpointConventionBuilder endpoint, PageDefinition page)
    {
        if (!page.Access.IsEmpty)
        {
            endpoint.RequireAuthorization([.. page.Access.Select(access => new AuthorizeAttribute { Policy = access.Policy, Roles = access.Roles })]);
        }
    }

    /// <summary>The plans' templates, each answered under the access rules of the pages it belongs to.</summary>
    private static void MapTemplates(IEndpointRouteBuilder endpoints, List<PageDefinition> pages)
    {
        var access = pages.Where(page => !page.NativeOnly).GroupBy(page => page.Route, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.SelectMany(page => page.Access).ToImmutableArray(), StringComparer.Ordinal);
        endpoints.MapGet(PageHandler.TemplatePath + "{fingerprint}", (HttpContext http, string fingerprint) =>
            PageHandler.TemplateAsync(http, http.RequestServices.GetRequiredService<PlanCache>(), fingerprint, access));
    }

    private static PageHandler Handler(HttpContext http) => http.RequestServices.GetRequiredService<PageHandler>();

    /// <summary>The framework's script and the modules some pages load, each served under its hash.</summary>
    private static void MapScripts(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Documents.Scripts.Prefix + "{name}", Documents.Scripts.ServeAsync);
        endpoints.MapGet(Documents.OpenInAppScript.Prefix + "{name}", Documents.OpenInAppScript.ServeAsync);
        endpoints.MapGet(Documents.DraftScript.Prefix + "{name}", Documents.DraftScript.ServeAsync);
        endpoints.MapGet(Documents.MorphScript.Prefix + "{name}", Documents.MorphScript.ServeAsync);
        endpoints.MapGet(Documents.PointerScript.Prefix + "{name}", Documents.PointerScript.ServeAsync);
        endpoints.MapGet(Documents.KeyboardScript.Prefix + "{name}", Documents.KeyboardScript.ServeAsync);
        endpoints.MapGet(Documents.OverlayScript.Prefix + "{name}", Documents.OverlayScript.ServeAsync);
        endpoints.MapGet(Documents.SplitScript.Prefix + "{name}", Documents.SplitScript.ServeAsync);
        endpoints.MapGet(Documents.CarouselScript.Prefix + "{name}", Documents.CarouselScript.ServeAsync);
        endpoints.MapGet(Documents.SwipeScript.Prefix + "{name}", Documents.SwipeScript.ServeAsync);
        endpoints.MapGet(Documents.FilterScript.Prefix + "{name}", Documents.FilterScript.ServeAsync);
        endpoints.MapGet(Documents.FlagScript.Prefix + "{name}", Documents.FlagScript.ServeAsync);
        endpoints.MapGet(Documents.ScrollScript.Prefix + "{name}", Documents.ScrollScript.ServeAsync);
        endpoints.MapGet(Documents.CopyScript.Prefix + "{name}", Documents.CopyScript.ServeAsync);
        endpoints.MapGet(Documents.RevealScript.Prefix + "{name}", Documents.RevealScript.ServeAsync);
        endpoints.MapGet(Documents.AnswerScript.Prefix + "{name}", Documents.AnswerScript.ServeAsync);
        endpoints.MapGet(Documents.ModalScript.Prefix + "{name}", Documents.ModalScript.ServeAsync);
        endpoints.MapGet(Documents.ChoiceScript.Prefix + "{name}", Documents.ChoiceScript.ServeAsync);
        endpoints.MapGet(Documents.RangeScript.Prefix + "{name}", Documents.RangeScript.ServeAsync);
        endpoints.MapGet(Documents.ScrollerScript.Prefix + "{name}", Documents.ScrollerScript.ServeAsync);
    }
}
