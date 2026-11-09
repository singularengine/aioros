using System.ComponentModel.DataAnnotations;

namespace Aioros.Runtime.Functions;

/// <summary>
/// The failures a server function shows its caller, the same in process as through the generated client: validation,
/// not found, refused access and cancellation as thrown; anything else as a failure without its details.
/// </summary>
public static class ServerFunctionFailures
{
    public const string FailedTitle = "The server function failed.";

    /// <summary>Whether an exception of the implementation reaches its caller as it is.</summary>
    public static bool Passes(Exception error) =>
        error is ValidationException or KeyNotFoundException or UnauthorizedAccessException or OperationCanceledException;

    /// <summary>What the caller sees of an exception that does not pass: a failure with the original as its inner exception.</summary>
    public static InvalidOperationException Failed(Exception error) => new(FailedTitle, error);
}
