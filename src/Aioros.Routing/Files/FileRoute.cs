using System.Collections.Generic;

namespace Aioros.Routing.Files;

public sealed class FileRoute(string route, string source, IReadOnlyList<string> layouts)
{
    public string Route { get; } = route;

    public string Source { get; } = source;

    public IReadOnlyList<string> Layouts { get; } = layouts;
}

public sealed class FileRouteConflict(string route, string first, string second)
{
    public string Route { get; } = route;

    public string First { get; } = first;

    public string Second { get; } = second;
}

public sealed class FileRouteMap(IReadOnlyList<FileRoute> routes, IReadOnlyList<FileRouteConflict> conflicts, FileRoute? notFound = null, FileRoute? error = null, FileRoute? nativeOnly = null)
{
    public FileRoute? Error { get; } = error;

    public FileRoute? NotFound { get; } = notFound;

    /// <summary>The stand-in the web shows at the route of a page that leaves out the web (<c>Pages/NativeOnly.axaml</c>).</summary>
    public FileRoute? NativeOnly { get; } = nativeOnly;

    public IReadOnlyList<FileRoute> Routes { get; } = routes;

    public IReadOnlyList<FileRouteConflict> Conflicts { get; } = conflicts;
}
