using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Aioros.Runtime.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Functions;

/// <summary>
/// What a server function answers: its result as JSON, or its failure as a problem (RFC 9457): a validation error with
/// its members, a missing item, a refused access, or a failure the log keeps and the client sees only by its title.
/// </summary>
internal static class FunctionAnswers
{
    private const string ValidationType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ValidationTitle = "One or more validation errors occurred.";

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, string, Exception?> Failed =
        Microsoft.Extensions.Logging.LoggerMessage.Define<string, string>(Microsoft.Extensions.Logging.LogLevel.Error, new Microsoft.Extensions.Logging.EventId(1, "ServerFunctionFailed"), "The server function {Group}/{Name} failed");

    public static Task FailAsync(HttpContext http, ServerFunctionGroup functions, ServerFunctionRoute route, Exception error)
    {
        switch (error)
        {
            case ValidationException validation:
                return ProblemAsync(http, StatusCodes.Status400BadRequest, writer =>
                {
                    writer.WriteString("type", ValidationType);
                    writer.WriteString("title", ValidationTitle);
                    writer.WriteNumber("status", StatusCodes.Status400BadRequest);
                    var message = validation.ValidationResult.ErrorMessage ?? validation.Message;
                    writer.WriteString("detail", message);
                    var members = validation.ValidationResult.MemberNames.ToList();
                    if (members.Count > 0)
                    {
                        writer.WriteStartObject("errors");
                        foreach (var member in members)
                        {
                            writer.WriteStartArray(member);
                            writer.WriteStringValue(message);
                            writer.WriteEndArray();
                        }

                        writer.WriteEndObject();
                    }
                });
            case KeyNotFoundException missing:
                return TitledAsync(http, StatusCodes.Status404NotFound, "Not Found", missing.Message);
            case UnauthorizedAccessException refused when http.User.Identity?.IsAuthenticated == true:
                return TitledAsync(http, StatusCodes.Status403Forbidden, "Forbidden", refused.Message);
            case UnauthorizedAccessException refused:
                return TitledAsync(http, StatusCodes.Status401Unauthorized, "Unauthorized", refused.Message);
            default:
                if (http.RequestServices.GetService<Microsoft.Extensions.Logging.ILoggerFactory>() is { } logs)
                {
                    Failed(logs.CreateLogger("Aioros.ServerFunctions"), functions.Group, route.Name, error);
                }

                return TitledAsync(http, StatusCodes.Status500InternalServerError, ServerFunctionFailures.FailedTitle, null);
        }
    }

    private static Task TitledAsync(HttpContext http, int status, string title, string? detail) =>
        ProblemAsync(http, status, writer =>
        {
            writer.WriteString("title", title);
            writer.WriteNumber("status", status);
            if (detail is not null)
            {
                writer.WriteString("detail", detail);
            }
        });

    private static Task ProblemAsync(HttpContext http, int status, Action<Utf8JsonWriter> members)
    {
        http.Response.StatusCode = status;
        http.Response.ContentType = "application/problem+json";
        return WriteAsync(http, writer =>
        {
            writer.WriteStartObject();
            members(writer);
            writer.WriteEndObject();
        });
    }

    public static async Task WriteAsync(HttpContext http, Action<Utf8JsonWriter> write)
    {
        using (var writer = new Utf8JsonWriter(http.Response.BodyWriter))
        {
            write(writer);
        }

        await http.Response.BodyWriter.FlushAsync(http.RequestAborted);
    }
}
