namespace Aioros.Functions;

/// <summary>
/// Deprecated, read as a fallback until step 1.9: declare server functions where they are registered,
/// <c>builder.AddServerFunction&lt;IOrders, Orders&gt;("orders")</c>, and put <c>[Authorize]</c> on the implementation,
/// so the contract references no framework. Marks a service interface whose implementation always runs on the server.
/// On the web the view models call it in process; in the installed app they call it through a generated HTTP client.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false)]
public sealed class ServerFunctionsAttribute(string group) : Attribute
{
    /// <summary>The stable route segment of the interface: <c>/_sf/{group}/{name}</c>. Renaming the C# type does not change it.</summary>
    public string Group { get; } = group;
}

/// <summary>Gives a server function a stable name instead of its method name in camel case.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ServerFunctionAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>The server runs a newer contract than this app; the app must update before calling it.</summary>
public sealed class ClientOutdatedException : Exception
{
    public ClientOutdatedException()
    {
        ServerContract = string.Empty;
    }

    public ClientOutdatedException(string message)
        : base(message)
    {
        ServerContract = string.Empty;
    }

    public ClientOutdatedException(string message, Exception innerException)
        : base(message, innerException)
    {
        ServerContract = string.Empty;
    }

    public ClientOutdatedException(string message, string serverContract)
        : base(message)
    {
        ServerContract = serverContract;
    }

    public string ServerContract { get; }
}
