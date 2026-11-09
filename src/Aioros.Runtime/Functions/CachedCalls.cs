using System.Security.Claims;
using Aioros.Runtime.Caching;

namespace Aioros.Runtime.Functions;

/// <summary>
/// A generated in-process caller that keeps its <c>[Cached]</c> results in the app's data cache and evicts what its
/// <c>[Invalidates]</c> functions change: its registration hands it the cache.
/// </summary>
public interface ICachedCalls
{
    DataCache? Cache { get; set; }
}

/// <summary>The services of a loader, which know the user the load runs for, so data cached per user is kept apart.</summary>
public interface ICallerIdentity
{
    ClaimsPrincipal User { get; }
}
