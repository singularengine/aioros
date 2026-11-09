using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Aioros.Targets.Ssr.Development;

/// <summary>
/// Answers a page's error in development with its fault: the innermost exception, the first frame in the app's own sources
/// and the code around it, as JSON for the navigation script (which shows it over the page) or as a document that reloads
/// itself once the code is fixed; other requests keep ASP.NET Core's developer exception page (Next.js's dev overlay).
/// </summary>
public sealed class DevelopmentFaults(IHostEnvironment environment) : IDeveloperPageExceptionFilter
{
    public const string Header = "X-Aioros-Fault";

    public async Task HandleExceptionAsync(ErrorContext errorContext, Func<ErrorContext, Task> next)
    {
        ArgumentNullException.ThrowIfNull(errorContext);
        ArgumentNullException.ThrowIfNull(next);
        var http = errorContext.HttpContext;
        var navigation = http.Request.Headers["X-Aioros-Navigation"] == "1";
        if (!navigation && !http.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            await next(errorContext);
            return;
        }

        var json = JsonSerializer.Serialize(await OfAsync(errorContext.Exception, http.RequestAborted), FaultContext.Default.Fault);
        http.Response.StatusCode = StatusCodes.Status500InternalServerError;
        http.Response.Headers.CacheControl = "no-store";
        if (navigation)
        {
            http.Response.Headers[Header] = "1";
            http.Response.ContentType = "application/json; charset=utf-8";
            await http.Response.WriteAsync(json, http.RequestAborted);
            return;
        }

        http.Response.ContentType = "text/html; charset=utf-8";
        var error = errorContext.Exception.GetBaseException();
        await http.Response.WriteAsync(
            $"<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(error.GetType().Name)}</title></head>"
            + $"<body><p>{WebUtility.HtmlEncode(error.Message)}</p>\n{Documents.Scripts.Development}<script>aiorosFault({json})</script>\n</body></html>",
            http.RequestAborted);
    }

    private async Task<Fault> OfAsync(Exception exception, CancellationToken cancellationToken)
    {
        var error = exception.GetBaseException();
        var root = SourceLines.Root(environment.ContentRootPath);
        var frame = new StackTrace(error, fNeedFileInfo: true).GetFrames()
            .FirstOrDefault(frame => frame.GetFileName() is { } file && Own(Path.GetFullPath(file), root));
        var type = error.GetType().FullName ?? error.GetType().Name;
        if (frame?.GetFileName() is not { } path)
        {
            return new Fault(type, error.Message, null, 0, 0, [], error.ToString()) { At = At(error.StackTrace, environment.ApplicationName) };
        }

        var line = frame.GetFileLineNumber();
        return new Fault(type, error.Message, SourceLines.Relative(root, path), line, frame.GetFileColumnNumber(), await SourceLines.AroundAsync(path, line, cancellationToken), error.ToString());
    }

    /// <summary>The first frame of the app's own code in the stack, for a fault whose frames have no line.</summary>
    private static string? At(string? stack, string application) =>
        application.Length == 0 || stack is null
            ? null
            : stack.Split('\n').Select(static frame => frame.Trim()).Where(static frame => frame.StartsWith("at ", StringComparison.Ordinal))
                .Select(static frame => frame[3..].Split(" in ")[0]).FirstOrDefault(frame => frame.StartsWith(application + ".", StringComparison.Ordinal));

    private static bool Own(string file, string root) =>
        file.StartsWith(root, StringComparison.Ordinal)
        && !file.AsSpan(root.Length).StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        && !file.AsSpan(root.Length).StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}

public sealed record Fault(string Type, string Message, string? File, int Line, int Column, ImmutableArray<FaultLine> Code, string Stack)
{
    /// <summary>The first method of the app's own code in the stack when no frame has a line (a method .NET Hot Reload replaced).</summary>
    public string? At { get; init; }
}

public sealed record FaultLine(int Line, string Text);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Fault))]
public sealed partial class FaultContext : JsonSerializerContext;
