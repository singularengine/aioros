using System.Text.Json;
using Aioros.Runtime.Functions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Functions;

public static class ServerFunctionEndpoints
{
    public static IEndpointRouteBuilder MapServerFunctions(this IEndpointRouteBuilder endpoints, IReadOnlyList<ServerFunctionGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var protection = endpoints.ServiceProvider.GetService<CrossSite.CrossOriginProtection>();
        endpoints.MapPost(ServerFunctionGroup.Prefix + "{group}/{name}", (HttpContext http, string group, string name) => DispatchAsync(http, groups, group, name, protection))
            .DisableAntiforgery();
        return endpoints;
    }

    public static async Task DispatchAsync(HttpContext http, IReadOnlyList<ServerFunctionGroup> groups, string group, string name, CrossSite.CrossOriginProtection? protection = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(groups);
        var functions = groups.FirstOrDefault(candidate => candidate.Group == group);
        var route = functions?.Routes.FirstOrDefault(candidate => candidate.Name == name);
        if (functions is null || route is null)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!(protection ?? CrossSite.CrossOriginProtection.Default).Allows(http.Request, ServerFunctionGroup.Prefix + group + "/" + name)
            || http.Request.Headers[ServerFunctionGroup.FunctionHeader] != "1")
        {
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (http.Request.Headers[ServerFunctionGroup.ContractHeader] != functions.Contract)
        {
            http.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            http.Response.Headers[ServerFunctionGroup.ContractHeader] = functions.Contract;
            return;
        }

        if (!route.Access.IsEmpty && await Access.AccessRules.RefusedAsync(http, [.. route.Access.Select(static access => (access.Policy, access.Roles))]) is { } refused)
        {
            http.Response.StatusCode = refused;
            return;
        }

        await RunAsync(http, functions, route);
    }

    private static async Task RunAsync(HttpContext http, ServerFunctionGroup functions, ServerFunctionRoute route)
    {
        using var arguments = http.Request.ContentLength is 0
            ? JsonDocument.Parse("{}")
            : await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted);
        var service = http.RequestServices.GetRequiredService(functions.Interface);
        var changes = route.Keyed ? DataKeys.Begin() : null;
        object? result;
        try
        {
            result = await route.Invoke(service, arguments.RootElement, http.RequestAborted);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await FunctionAnswers.FailAsync(http, functions, route, error);
            return;
        }

        DataKeys.End();
        if (changes is { Invalidated.IsEmpty: false })
        {
            http.Response.Headers[ServerFunctionGroup.RevalidateHeader] = string.Join(',', changes.Invalidated.Order(StringComparer.Ordinal).Select(Uri.EscapeDataString));
        }

        if (route.Write is not { } write)
        {
            http.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        http.Response.StatusCode = StatusCodes.Status200OK;
        http.Response.ContentType = "application/json; charset=utf-8";
        await FunctionAnswers.WriteAsync(http, writer => write(writer, result));
    }
}
