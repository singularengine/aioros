using System.Collections.Immutable;
using System.Text.Json;

namespace Aioros.Runtime.Functions;

/// <summary>Runs one server function on a service instance with arguments read from JSON.</summary>
public delegate Task<object?> ServerFunctionInvoke(object service, JsonElement arguments, CancellationToken cancellationToken);

public sealed record ServerFunctionRoute(string Name, ServerFunctionInvoke Invoke)
{
    /// <summary>The <c>[Authorize]</c> rules of the server implementation's class and method; every one must hold, and none means open.</summary>
    public ImmutableArray<ServerFunctionAccess> Access { get; init; } = [];

    /// <summary>Writes the function's result as JSON with generated code; none for a function returning <c>Task</c>, answered with <c>204</c>.</summary>
    public Action<Utf8JsonWriter, object?>? Write { get; init; }

    /// <summary>
    /// Whether the server implementation declares the function's data keys (<c>[Reads]</c> or <c>[Invalidates]</c>), so
    /// the keys it invalidates are recorded during the call and answered in <see cref="ServerFunctionGroup.RevalidateHeader"/>.
    /// </summary>
    public bool Keyed { get; init; }
}

/// <summary>One <c>[Authorize]</c> rule of a server function: signed in, and the policy and roles when they are set.</summary>
public sealed record ServerFunctionAccess(string? Policy, string? Roles);

/// <summary>A generated description of an interface registered as server functions.</summary>
public sealed record ServerFunctionGroup(Type Interface, string Group, string Contract, ImmutableArray<ServerFunctionRoute> Routes, Func<HttpClient, object> Client)
{
    public const string Prefix = "/_sf/";

    public const string FunctionHeader = "X-Aioros-Fn";

    public const string ContractHeader = "X-Aioros-Contract";

    /// <summary>The keys a successful call invalidated, each escaped as a URI component, separated by commas (SolidStart's <c>X-Revalidate</c>).</summary>
    public const string RevalidateHeader = "X-Aioros-Revalidate";

    /// <summary>Wraps the implementation in the generated in-process caller, which fails as the client would.</summary>
    public Func<object, object>? InProcess { get; init; }

    public static string Path(string group, string name) => string.Join('/', Prefix.TrimEnd('/'), group, name);
}
