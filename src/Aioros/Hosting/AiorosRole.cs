namespace Aioros.Hosting;

/// <summary>What the running host is, decided when the app starts.</summary>
public enum AiorosRole
{
    /// <summary>The website: it serves pages, loaders and server functions, and owns the server's services.</summary>
    Server,

    /// <summary>The desktop app running the server's services in its own process, as in development.</summary>
    InProcess,

    /// <summary>The installed app working against a remote server (<c>Aioros:Server</c>): loaders and server functions run there.</summary>
    Client,
}
