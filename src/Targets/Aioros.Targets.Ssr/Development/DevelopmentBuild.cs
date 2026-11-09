using System.Collections.Immutable;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aioros.Events;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Aioros.Targets.Ssr.Development;

/// <summary>
/// <c>POST /_aioros/dev/build</c>: the errors of a C# edit that does not compile, posted by <c>aioros dev</c> from the
/// loopback address with the token it gave the app, published with their source lines so the page shows them over itself.
/// </summary>
public static class DevelopmentBuild
{
    public const string Path = "/_aioros/dev/build";

    public const string TokenHeader = "X-Aioros-Development-Token";

    public static async Task ReceiveAsync(HttpContext http, IEventBus bus, SsrOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        if (!Allowed(http, options.DevelopmentToken))
        {
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var report = await JsonSerializer.DeserializeAsync(http.Request.Body, BuildContext.Default.PostedBuild, http.RequestAborted);
        var root = SourceLines.Root(environment.ContentRootPath);
        var errors = ImmutableArray.CreateBuilder<ReportedError>();
        foreach (var error in report?.Errors ?? [])
        {
            errors.Add(new ReportedError(SourceLines.Relative(root, error.File), error.Line, error.Column, error.Code, error.Message, await SourceLines.AroundAsync(error.File, error.Line, http.RequestAborted)));
        }

        await bus.PublishAsync(new BuildReported(errors.ToImmutable()), http.RequestAborted);
        http.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static bool Allowed(HttpContext http, string? token) =>
        token is { Length: > 0 }
        && http.Connection.RemoteIpAddress is { } from && IPAddress.IsLoopback(from)
        && http.Request.Headers[TokenHeader].ToString() is { Length: > 0 } given
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(token));
}

public sealed record PostedError(string File, int Line, int Column, string Code, string Message);

public sealed record PostedBuild(ImmutableArray<PostedError> Errors);

public sealed record ReportedError(string File, int Line, int Column, string Code, string Message, ImmutableArray<FaultLine> Source);

/// <summary>The errors of the last C# edit that did not compile; none once an edit applies.</summary>
public sealed record BuildReported(ImmutableArray<ReportedError> Errors);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PostedBuild))]
[JsonSerializable(typeof(BuildReported))]
public sealed partial class BuildContext : JsonSerializerContext;
