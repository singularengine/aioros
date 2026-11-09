using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Hosting;

internal sealed class StaticFilesFirst : IRouteConstraint
{
    private static readonly StaticFilesFirst Instance = new();

    public static IEndpointConventionBuilder MapGet(IEndpointRouteBuilder endpoints, string route, RequestDelegate handler) =>
        endpoints.Map(Pattern(route), handler).WithMetadata(new HttpMethodMetadata([HttpMethods.Get])).WithDisplayName("HTTP: GET " + route);

    private static RoutePattern Pattern(string route)
    {
        var pattern = RoutePatternFactory.Parse(route);
        return pattern.Parameters.Count == 0
            ? pattern
            : RoutePatternFactory.Parse(route, defaults: null, parameterPolicies: new RouteValueDictionary { [pattern.Parameters[^1].Name] = Instance });
    }

    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (httpContext is null || routeDirection != RouteDirection.IncomingRequest)
        {
            return true;
        }

        var path = httpContext.Request.Path.Value.AsSpan();
        if (!path[(path.LastIndexOf('/') + 1)..].Contains('.'))
        {
            return true;
        }

        var root = httpContext.RequestServices.GetService<IWebHostEnvironment>()?.WebRootFileProvider;
        return root is null || !root.GetFileInfo(httpContext.Request.Path.Value!).Exists;
    }
}
